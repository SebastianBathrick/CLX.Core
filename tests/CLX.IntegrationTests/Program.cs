using CLX.Core;
using CLX.Core.Commands;

internal class Program
{
    private static void Main(string[] args)
    {
        // Simple REPL for trying commands interactively
        var runtime = new ClxRuntime(new ConsoleTextWriter());

        Console.WriteLine("CLX interactive shell. Type 'exit' or 'quit' to leave.");
        Console.WriteLine("Examples: help | echo hello world | add 1 2 3 | greet Bob --times 2 | showargs path -- -n --x");

        while (true)
        {
            Console.Write("clx> ");
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cmd = line.Trim();
            if (cmd.Equals("exit", StringComparison.OrdinalIgnoreCase) || cmd.Equals("quit", StringComparison.OrdinalIgnoreCase))
                break;

            var argv = Tokenize(cmd);
            if (argv.Length == 0) continue;

            var code = runtime.Run(argv);
            if (code != 0)
                Console.WriteLine($"(exit {code})");
        }
    }

    // Minimal tokenizer that honors simple double-quoted segments and the -- sentinel
    static string[] Tokenize(string input)
    {
        var list = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (current.Length > 0) { list.Add(current.ToString()); current.Clear(); }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0) list.Add(current.ToString());
        return list.ToArray();
    }
}

// Text writer that proxies to Console for convenience in this interactive app
sealed class ConsoleTextWriter : ITextWriter
{
    public void Write(string text) => Console.Write(text);
    public void WriteLine(string text) => Console.WriteLine(text);
}

// Sample commands demonstrating arguments, variadics, flags, and composite names
sealed class EchoCommand : ICommand
{
    public string Name => "echo";
    public string Description => "Echo all positional arguments";
    public ITextWriter? Output => null;
    public ITextWriter? ErrorOutput => null;
    public string WorkingDirectory => string.Empty;

    [Argument(0, Name = "words", MinValues = 1, MaxValues = int.MaxValue)]
    private List<string> Words { get; set; } = new();

    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        context.Output.WriteLine(string.Join(' ', Words));
        return 0;
    }
}

sealed class AddCommand : ICommand
{
    public string Name => "add";
    public string Description => "Sum integer arguments";
    public ITextWriter? Output => null;
    public ITextWriter? ErrorOutput => null;
    public string WorkingDirectory => string.Empty;

    [Argument(0, Name = "numbers", MinValues = 1, MaxValues = int.MaxValue)]
    private List<int> Numbers { get; set; } = new();

    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        var sum = 0;
        foreach (var n in Numbers) sum += n;
        context.Output.WriteLine(sum.ToString());
        return 0;
    }
}

sealed class GreetCommand : ICommand
{
    public string Name => "greet";
    public string Description => "Greet a person optionally multiple times";
    public ITextWriter? Output => null;
    public ITextWriter? ErrorOutput => null;
    public string WorkingDirectory => string.Empty;

    [Argument(0, Name = "name", IsRequired = true, MinValues = 1, MaxValues = 1)]
    private string NameArg { get; set; } = string.Empty;

    [Flag("times", AlternateName = "t", MinValues = 1, MaxValues = 1, ValueRegexPattern = "^\\d+$")]
    private int _sink { get; set; }

    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        var times = 1;
        if (ICommandContext.TryGetFlag<IFlagObject>(context, "times", out var f) && f != null)
            _ = int.TryParse(f.Values[0], out times);

        for (int i = 0; i < times; i++)
            context.Output.WriteLine($"Hello, {NameArg}!");
        return 0;
    }
}

sealed class ShowArgsCommand : ICommand
{
    public string Name => "showargs";
    public string Description => "Show a path and the rest arguments (demonstrates --)";
    public ITextWriter? Output => null;
    public ITextWriter? ErrorOutput => null;
    public string WorkingDirectory => string.Empty;

    [Argument(0, Name = "path", IsRequired = true, MinValues = 1, MaxValues = 1)]
    private string Path { get; set; } = string.Empty;

    [Argument(1, Name = "rest", MinValues = 0, MaxValues = int.MaxValue)]
    private List<string> Rest { get; set; } = new();

    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        context.Output.WriteLine($"path = {Path}");
        context.Output.WriteLine($"rest = [{string.Join(", ", Rest)}]");
        return 0;
    }
}
