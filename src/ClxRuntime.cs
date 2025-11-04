using System.Reflection;
using CLX.Core.Lexing;
using CLX.Core.Commands;
using CLX.Core.Parsing;

namespace CLX.Core;

using ValidCommands = IReadOnlyDictionary<string, ICommand>;
using Contexts = IReadOnlyList<ICommandContext>;

/// <summary> ClxRuntime is a class that manages the execution of CLX commands. It loads command 
/// definitions from the current assembly, uses a lexer and parser to process input arguments, 
/// creates command contexts, and executes each command in sequence. It also handles errors during
/// execution, storing any exceptions that occur and writing error messages through an optional 
/// error writer. </summary>
public sealed partial class ClxRuntime(ITextWriter? errorWriter = null)
{
    ValidCommands? _commands = null;

    readonly ILexer _lexer = new Lexer();
    readonly IParser _parser = new Parser();

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
            if (!_lexer.TryCreateCommandNodes(args, [.. _commands.Keys], out var nodesList, out var errorArg))
            {
                _errorWriter?.WriteLine($"Command line error: Unexpected argument '{errorArg}'");
                return ERROR_EXIT_CODE;
            }

            if (!_parser.TryCreateCommandContexts(nodesList, _commands, out var contextsList, out var parseError))
            {
                _errorWriter?.WriteLine($"Command line error: {parseError}");
                return ERROR_EXIT_CODE;
            }

            var exitCode = ExecuteCommands(_commands, contextsList, workingDirectory, out var failedCmdName);

            if (exitCode == SUCCESS_EXIT_CODE)
                return SUCCESS_EXIT_CODE;

            _errorWriter?.WriteLine($"Command '{failedCmdName}' failed with exit code {exitCode}");
            return ERROR_EXIT_CODE;
        }
        catch (ClxCommandExecuteError parseEx)
        {
            _errorWriter?.WriteLine(parseEx.Message);
            _exception = parseEx;
        }
        catch (Exception ex)
        {
            _errorWriter?.WriteLine($"Unhandled exception: {ex.Message}");
            _exception = ex;
        }

        return ERROR_EXIT_CODE;
    }

    static int ExecuteCommands(ValidCommands commands, Contexts contexts, string workingDirectory, out string failedCommandName)
    {
        failedCommandName = string.Empty;

        foreach (var context in contexts)
        {
            // All the contexts have already been validated so no checking is necessary here
            var command = commands[context.CommandName];
            var exitCode = command.Execute(context, workingDirectory);

            if (exitCode == SUCCESS_EXIT_CODE)
                continue;

            // If a single command fails then do not run anymore commands to avoid unintended user actions
            failedCommandName = context.CommandName;
            return exitCode;
        }

        return SUCCESS_EXIT_CODE; // All commands succeeded
    }

    static ValidCommands LoadCommandsFromAssembly()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        var commandTypes = assembly.GetTypes()
            .Where(t => typeof(ICommand).IsAssignableFrom(t) && !t.IsAbstract);

        var commands = new Dictionary<string, ICommand>();

        foreach (var type in commandTypes)
        {
            var instance = (ICommand)Activator.CreateInstance(type)!;
            commands[instance.Name] = instance;
        }

        return commands.AsReadOnly();
    }
}

partial class ClxRuntime
{
    const int SUCCESS_EXIT_CODE = 0;
    const int ERROR_EXIT_CODE = -1;
}

public sealed class ClxCommandExecuteError : AggregateException
{
    public ClxCommandExecuteError(IEnumerable<Exception> innerExceptions)
        : base($"Command Execute(ICommandContext) exception(s) thrown:\n{string.Join("\n", innerExceptions)}", innerExceptions) { }
}

