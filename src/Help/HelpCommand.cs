using CLX.Core.Commands;
using System.Reflection;

namespace CLX.Core.Help;

/// <summary> Help command that lists commands and shows details for a specific command path. </summary>
/// <remarks> Prints a top-level index when invoked without arguments and a detailed view with usage,
/// flags, and subcommands when invoked with <for>--for</for>. This command is discovered the same
/// way as user commands so clients can override it if desired. </remarks>
sealed class HelpCommand : ICommand
{
    /// <summary> The command name used to invoke help. </summary>
    public string Name => "help";

    /// <summary> Short user-facing description for listings. </summary>
    public string Description => "Show help for commands and subcommands.";

    /// <summary> The writer used for standard output, not required. </summary>
    public ITextWriter? Output { get; } = null;

    /// <summary> The writer used for error output, not required. </summary>
    public ITextWriter? ErrorOutput { get; } = null;

    /// <summary> Working directory is unused by the help command. </summary>
    public string WorkingDirectory { get; } = string.Empty;

    [Flag("for", AlternateName = "f", MinValues = 0, MaxValues = 16)]
    private int _sink { get; set; }

    /// <summary> Execute the help command to print command listings or details. </summary>
    /// <remarks> If <for>--for</for> is omitted, prints top-level commands. Otherwise prints usage,
    /// description, flags, and any subcommands for the resolved path. </remarks>
    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        var output = context.Output;

        // Resolve commands and build a hierarchy for navigation
        var (_, tree) = ResolveCommandsAndTree();

        // If no --for path is provided then show the index of top-level commands
        if (!TryGetRequestedNode(context, tree, out var node, out var parts))
            return ListTopLevelCommands(output, tree);

        if (node == null)
        {
            output.WriteLine($"Unknown command '{string.Join(' ', parts)}'.");
            return -1;
        }

        WriteCommandDetails(output, node);
        return 0;
    }

    /// <summary> Write the top-level command listing (names and summaries). </summary>
    /// <remarks> Hidden commands are omitted from listings. </remarks>
    private static int ListTopLevelCommands(ITextWriter output, CommandTreeNode tree)
    {
        output.WriteLine("Commands:");

        foreach (var child in tree.Children)
        {
            if (child.Command is { } cmd && cmd.Hidden) 
                continue;

            var description = GetCommandDescription(child.Command);
            
            output.WriteLine($"  {child.Name}\t{description}");
        }

        output.WriteLine("\nUse 'help --for <command path>' for details.");
        return 0;
    }

    /// <summary> Resolve discovered commands and build a command tree for navigation. </summary>
    private static (IReadOnlyDictionary<string, ICommand> Commands, CommandTreeNode Tree)
        ResolveCommandsAndTree()
    {
        var commands = LoadAllCommands();
        var tree = CommandTreeBuilder.BuildTree(commands);
        return (commands, tree);
    }

    /// <summary> Try to resolve the requested node from <for>--for</for> flag. </summary>
    /// <remarks> Returns false when the flag is absent to indicate the caller should display the
    /// index listing. Returns a null node when the path is present but not found. </remarks>
    private static bool TryGetRequestedNode(
        ICommandContext context,
        CommandTreeNode tree,
        out CommandTreeNode? node,
        out IReadOnlyList<string> parts)
    {
        parts = Array.Empty<string>();
        node = null;

        if (!ICommandContext.TryGetFlag<IFlagObject>(context, "for", out var forFlag) ||
            forFlag == null || forFlag.Values.Count == 0)
            return false;

        parts = forFlag.Values;
        if (!CommandTreeBuilder.TryFindNode(tree, parts, out node))
            node = null;
        return true;
    }

    /// <summary> Write detailed help for a resolved command node: usage, description, flags, and
    /// subcommands. </summary>
    private static void WriteCommandDetails(ITextWriter output, CommandTreeNode node)
    {
        WriteUsage(output, node);

        if (node.Command != null)
        {
            var desc = node.Command.ExtendedDescription ?? node.Command.Summary ??
                       node.Command.Description;
            if (!string.IsNullOrWhiteSpace(desc))
                output.WriteLine(desc);

            WriteFlags(output, node.Command);
            WriteArguments(output, node.Command);
            output.WriteLine("\nUse '--' to stop parsing flags.");
        }

        WriteSubcommands(output, node);
    }

    /// <summary> Write the usage line for a command path. </summary>
    private static void WriteUsage(ITextWriter output, CommandTreeNode node)
    {
        var argsUsage = string.Empty;
        if (node.Command != null)
        {
            var args = GetArgumentAttributes(node.Command);
            if (args != null && args.Count > 0)
            {
                var parts = new List<string>();
                foreach (var a in args.OrderBy(a => a.Index))
                {
                    var name = string.IsNullOrWhiteSpace(a.Name) ? $"arg{a.Index}" : a.Name!;
                    var isVariadic = a.MaxValues == int.MaxValue;
                    var isRequired = a.IsRequired || a.MinValues > 0;
                    if (isVariadic)
                        parts.Add(isRequired ? $"<{name}...>" : $"[{name}...]");
                    else
                        parts.Add(isRequired ? $"<{name}>" : $"[{name}]");
                }
                argsUsage = " " + string.Join(' ', parts);
            }
        }
        output.WriteLine($"Usage: <tool> {node.FullPath}{argsUsage} [flags]\n");
    }

    /// <summary> Write a table of flags for the given command, if any. </summary>
    private static void WriteFlags(ITextWriter output, ICommand command)
    {
        var flags = GetFlagAttributes(command);
        if (flags?.Count > 0)
        {
            output.WriteLine("\nFlags:");
            foreach (var fa in flags)
                output.WriteLine(FormatFlagLine(fa));
        }
    }

    /// <summary> Write a table of positional arguments for the given command, if any. </summary>
    private static void WriteArguments(ITextWriter output, ICommand command)
    {
        var args = GetArgumentAttributes(command);
        if (args?.Count > 0)
        {
            output.WriteLine("\nArguments:");
            foreach (var aa in args.OrderBy(a => a.Index))
                output.WriteLine(FormatArgumentLine(aa));
        }
    }

    /// <summary> Format a single flag line with alias, arity, requirement and regex hints. </summary>
    private static string FormatFlagLine(FlagAttribute fa)
    {
        var alias = string.IsNullOrEmpty(fa.AlternateName) ? string.Empty : $", -{fa.AlternateName}";
        var values = fa.MinValues == fa.MaxValues ? fa.MinValues.ToString() :
                     $"{fa.MinValues}..{fa.MaxValues}";
        var req = fa.IsRequired ? "(required)" : string.Empty;
        var regex = string.IsNullOrEmpty(fa.ValueRegexPattern) ? string.Empty :
                    $" [regex: {fa.ValueRegexPattern}]";
        return $"  --{fa.Name}{alias}\tvalues: {values} {req}{regex}";
    }

    /// <summary> Write immediate child subcommands of the given node, if any. </summary>
    private static void WriteSubcommands(ITextWriter output, CommandTreeNode node)
    {
        if (node.Children.Count == 0)
            return;

        output.WriteLine("\nSubcommands:");
        foreach (var child in node.Children)
        {
            if (child.Command is { } cmd && cmd.Hidden)
                continue;
            var description = GetCommandDescription(child.Command);
            output.WriteLine($"  {child.Name}\t{description}");
        }
    }

    /// <summary> Get the preferred description text for a command. </summary>
    private static string GetCommandDescription(ICommand? command)
        => command?.Summary ?? command?.Description ?? string.Empty;

    private static IReadOnlyDictionary<string, ICommand> LoadAllCommands()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var dict = new Dictionary<string, ICommand>(StringComparer.Ordinal);

        foreach (var asm in assemblies)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).Cast<Type>().ToArray(); }

            foreach (var t in types)
            {
                if (t == null || t.IsAbstract) 
                    continue;

                if (!typeof(ICommand).IsAssignableFrom(t)) 
                    continue;

                var cmd = (ICommand)Activator.CreateInstance(t)!;
                if (cmd.Hidden) 
                    continue;

                dict[cmd.Name] = cmd;
            }
        }

        return dict;
    }

    private static IReadOnlyList<FlagAttribute>? GetFlagAttributes(ICommand cmd)
    {
        var type = cmd.GetType();
        var list = new List<FlagAttribute>();
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var attr in prop.GetCustomAttributes(typeof(FlagAttribute), inherit: true))
                if (attr is FlagAttribute fa)
                    list.Add(fa);
        }
        return list.Count > 0 ? list.AsReadOnly() : null;
    }

    private static IReadOnlyList<ArgumentAttribute>? GetArgumentAttributes(ICommand cmd)
    {
        var type = cmd.GetType();
        var list = new List<ArgumentAttribute>();
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var attr in prop.GetCustomAttributes(typeof(ArgumentAttribute), inherit: true))
                if (attr is ArgumentAttribute aa)
                    list.Add(aa);
        }
        if (list.Count == 0) return null;
        list.Sort((a, b) => a.Index.CompareTo(b.Index));
        return list.AsReadOnly();
    }

    private static string FormatArgumentLine(ArgumentAttribute aa)
    {
        var name = string.IsNullOrWhiteSpace(aa.Name) ? $"arg{aa.Index}" : aa.Name!;
        var values = aa.MinValues == aa.MaxValues ? aa.MinValues.ToString() : $"{aa.MinValues}..{aa.MaxValues}";
        var req = aa.IsRequired || aa.MinValues > 0 ? "(required)" : string.Empty;
        var regex = string.IsNullOrEmpty(aa.ValueRegexPattern) ? string.Empty : $" [regex: {aa.ValueRegexPattern}]";
        return $"  {aa.Index}: {name}\tvalues: {values} {req}{regex}";
    }
}


