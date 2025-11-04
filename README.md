## CLX.Core

A lightweight, easy-to-use class library for building and executing command‑line interfaces.

### Features
- **Greedy subcommands**: space‑separated command paths like `user add`; longest match wins
- **Flags with validation**: `[Flag]` attributes (required, min/max values, alias, regex)
- **Positional arguments**: annotate with `[Argument(index)]`, typed binding and validation
- **Typed conversion**: automatic conversion to common types (see `TypeConversion`)
- **`--` sentinel**: stop option parsing; remaining tokens treated as positional
- **Short/long flags**: `-t` and `--times` with kebab‑case alias validation
- **Built‑in `help`**: usage, flags, positional arguments, and subcommand listings
- **Sequential multi‑command execution**: executes in order, stops on first failure
- **Flexible output**: test‑friendly `ITextWriter`, plus `ConsoleTextWriter` convenience

### Requirements
- .NET 9.0 SDK

---

## Installation (local project reference)
Until a NuGet package is published, reference the project locally.

1) Add this repository (e.g., as a subfolder) next to your app:
```
your-solution/
  CLX.Core/            # this repo
  Your.App/
```

2) Add a project reference from your app to `src/CLX.Core.csproj`:
```bash
dotnet add Your.App/Your.App.csproj reference ../CLX.Core/src/CLX.Core.csproj
```

3) Build your solution:
```bash
dotnet build
```

---

## Quickstart

### 1) Wire up the runtime and output
Configure console output for runtime errors and the built‑in help command.

```csharp
using CLX.Core;
using CLX.Core.Commands;
using CLX.Core.Help;

HelpOptions.OutputWriter = ConsoleTextWriter.Instance;
HelpOptions.ErrorOutputWriter = ConsoleTextWriter.Instance;
return new ClxRuntime(ConsoleTextWriter.Instance).Run(args);
```

### 2) Create a command (positional args + flags)
Implement `ICommand`. Declare positional arguments with `[Argument]` and flags with `[Flag]`. Values are accessed via the `ICommandContext` and bound properties.

```csharp
using CLX.Core.Commands;

sealed class GreetCommand : ICommand
{
    public string Name => "greet";
    public string Description => "Greet a person optionally multiple times";
    public ITextWriter? Output => ConsoleTextWriter.Instance;
    public ITextWriter? ErrorOutput => ConsoleTextWriter.Instance;
    public string WorkingDirectory => string.Empty;

    [Argument(0, Name = "name", IsRequired = true, MinValues = 1, MaxValues = 1)]
    private string NameArg { get; set; } = string.Empty;

    [Flag("times", AlternateName = "t", MinValues = 1, MaxValues = 1, ValueRegexPattern = "^\\d+$")]
    private int _sinkTimes { get; set; }

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
```

Run it:
```bash
your-app greet Alice
your-app greet Bob --times 2
```

### 3) Subcommands
Define subcommands by using a space‑separated `Name` (e.g., `user add`). Flags must come after the full command path.

```csharp
using CLX.Core.Commands;

sealed class UserAddCommand : ICommand
{
    public string Name => "user add";
    public string Description => "Add a new user.";
    public ITextWriter? Output => ConsoleTextWriter.Instance;
    public ITextWriter? ErrorOutput => ConsoleTextWriter.Instance;
    public string WorkingDirectory => string.Empty;

    [Flag("name", AlternateName = "n", MinValues = 1, MaxValues = 1, IsRequired = true)]
    private int _sinkName { get; set; }

    [Flag("role", AlternateName = "r", MinValues = 1, MaxValues = 1)]
    private int _sinkRole { get; set; }

    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        if (!ICommandContext.TryGetFlag<IFlagObject>(context, "name", out var name) || name == null)
            return -1;
        var role = ICommandContext.TryGetFlag<IFlagObject>(context, "role", out var r) && r != null ? r.Values[0] : "user";
        context.Output.WriteLine($"Added {name.Values[0]} as {role}.");
        return 0;
    }
}
```

Run it:
```bash
your-app user add --name Alice --role admin
```

Multi‑command invocation is supported (executed in order, stops on first failure):

```bash
your-app greet Bob --times 1 user add --name Alice
```

---

## Help
CLX ships a built‑in `help` command that lists commands, usage, flags, and positional arguments.

```bash
# Top‑level index
your-app help

# Details for a specific path (quotes recommended for multi-word paths)
your-app help --for "greet"
```

Example output:

```text
Commands:
  add   Sum integer arguments
  echo  Echo all positional arguments
  greet Greet a person optionally multiple times
  help  Show help for commands and subcommands.

Use 'help --for <command path>' for details.

Usage: <tool> greet <name> [flags]

Greet a person optionally multiple times

Flags:
  --times, -t     values: 1  [regex: ^\d+$]

Arguments:
  0: name         values: 1 (required)

Use '--' to stop parsing flags.
```

Hidden commands (`ICommand.Hidden == true`) are omitted from help listings.

---

## API highlights

```csharp
// ICommand: implement to define a command
public interface ICommand
{
    string Name { get; }
    string Description { get; }
    ITextWriter? Output { get; }
    ITextWriter? ErrorOutput { get; }
    string WorkingDirectory { get; }
    string? Summary => null;
    string? ExtendedDescription => null;
    bool Hidden => false;
    int Execute(ICommandContext context, string workingDirectory = "");
}

// ICommandContext: runtime‑validated invocation
public interface ICommandContext
{
    string CommandName { get; }
    IReadOnlyList<IFlagObject> Flags { get; }
    ITextWriter Output { get; }
    ITextWriter ErrorOutput { get; }
    string WorkingDirectory { get; }
    IReadOnlyList<string> Arguments { get; }

    static bool TryGetFlag<T>(ICommandContext context, string flagName, out T? flag) where T : class, IFlagObject;
    static bool TryGetArgument<T>(ICommandContext context, int index, out T value);
    static bool TryGetArguments<T>(ICommandContext context, int startIndex, out IReadOnlyList<T> values);
}

// Flags: annotate properties to declare CLI contract
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class FlagAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public string? AlternateName { get; set; }
    public bool IsRequired { get; set; }
    public int MinValues { get; set; }
    public int MaxValues { get; set; }
    public string? ValueRegexPattern { get; set; }
}

// Positional arguments: annotate properties to declare positional inputs
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ArgumentAttribute(int index) : Attribute
{
    public int Index { get; } = index;
    public string? Name { get; set; }
    public bool IsRequired { get; set; }
    public int MinValues { get; set; }
    public int MaxValues { get; set; }
    public string? ValueRegexPattern { get; set; }
}

// Parsed flag instance
public interface IFlagObject
{
    string Name { get; }
    string? AlternateName { get; }
    IReadOnlyList<string> Values { get; }
}

// Output abstraction
public interface ITextWriter
{
    void Write(string text);
    void WriteLine(string text);
}
```

---

## Building & testing

```bash
# From repo root
dotnet build CLX.Core.sln
dotnet test CLX.Core.sln
```

---

## Limitations (short) & roadmap
- Flags must follow the full command path (no flags between parent/child)
- No global/parent flags; flags are scoped to the matched command
- Only the last positional argument may be variadic (`MaxValues = int.MaxValue`)

Planned: publish a NuGet package and explore global/parent flags.

---

## License
MIT License (see copyright notices).


