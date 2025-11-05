using CLX.Core.Commands;
using CLX.Core.Pipeline;
using CLX.Core.Writers;
using System.Reflection;

namespace CLX.Core;

using Contexts = IReadOnlyList<ICommandContext>;
using ValidCommands = IReadOnlyDictionary<string, ICommand>;

/// <summary> ClxRuntime is a class that manages the execution of CLX commands. It loads command 
/// definitions from the current assembly, uses a lexer and parser to process input arguments, 
/// creates command contexts, and executes each command in sequence. It also handles errors during
/// execution, storing any exceptions that occur and writing error messages through an optional 
/// error writer. </summary>
public sealed partial class ClxRuntime(ITextWriter? errorWriter = null)
{
    ValidCommands? _commands = null;

    readonly Lexer _lexer = new();
    readonly Parser _parser = new();
    readonly FlagValueBinder _valueBinder = new();

    string _errorMessage = string.Empty;
    readonly ITextWriter? _errorWriter = errorWriter;

    Exception? _exception = null;

    /// <summary> An exception thrown during a call to <see cref="Run(string[])"/> or null. </summary>
    public Exception? Exception => _exception;

    /// <summary> Reads in arguments as commands and runs each sequentially. Returns 0 if successful, -1 if an error occurs. </summary>
    /// <param name="args"> The arguments to run. </param>
    /// <param name="workingDirectory"> The working directory to use for the commands. </param>
    /// <returns> 0 if successful, -1 if an error occurs. </returns>
    public int Run(string[] args, string workingDirectory = "")
    {
        try
        {
            // Load all of the client's commands from the assembly during the first run
            _commands ??= LoadCommandsFromAssembly();

            // Try to create command nodes; return error if lexing fails
            if (!_lexer.TryCreateCommandNodes(args, [.. _commands.Keys], out var nodesList))
            {
                _errorWriter?.WriteLine($"Command line error: {_lexer.ErrorMessage}");
                return ERROR_EXIT_CODE;
            }

            if (!_parser.TryCreateCommandContexts(nodesList, _commands, out var contextsList))
            {
                _errorWriter?.WriteLine($"Command line error: {_parser.ErrorMessage}");
                return ERROR_EXIT_CODE;
            }

            return ExecuteCommands(_commands, contextsList, workingDirectory);
        }
        catch (Exception ex)
        {
            _errorWriter?.WriteLine($"Unhandled exception: {ex.Message}");
            _exception = ex;
        }

        return ERROR_EXIT_CODE;
    }

    int ExecuteCommands(ValidCommands commands, Contexts contexts, string workingDirectory)
    {
        foreach (var context in contexts)
        {
            // All the contexts have already been validated so no checking is necessary here
            var command = commands[context.CommandName];

            if (!_valueBinder.TryBind(command, context))
            {
                _errorWriter?.WriteLine(_valueBinder.ErrorMessage);
                return ERROR_EXIT_CODE;
            }

            var exitCode = command.Execute(context, workingDirectory);

            if (exitCode == SUCCESS_EXIT_CODE)
                continue;

            _errorWriter?.WriteLine($"Command '{context.CommandName}' failed with exit code {exitCode}");
            return exitCode;
        }

        return SUCCESS_EXIT_CODE; // All commands succeeded
    }

    static ValidCommands LoadCommandsFromAssembly()
    {
        // Discover ICommand implementations across all loaded assemblies to support test hosts
        // and multi-assembly applications.
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        var commands = new Dictionary<string, ICommand>();

        foreach (var asm in assemblies)
        {
            Type[] types;
            try 
            { 
                types = asm.GetTypes(); 
            }
            catch (ReflectionTypeLoadException ex) 
            { 
                types = ex.Types.Where(t => t != null).Cast<Type>().ToArray(); 
            }

            foreach (var type in types)
            {
                if (type == null || type.IsAbstract)
                    continue;

                if (!typeof(ICommand).IsAssignableFrom(type))
                    continue;

                var instance = (ICommand)Activator.CreateInstance(type)!;
                commands[instance.Name] = instance;
            }
        }

        return commands.AsReadOnly();
    }
}

partial class ClxRuntime
{
    const int SUCCESS_EXIT_CODE = 0;
    const int ERROR_EXIT_CODE = -1;
}

