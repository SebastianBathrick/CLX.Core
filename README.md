## CLX.Core

A lightweight, easy-to-use class library for building and executing command‑line interfaces.

### Features
- Greedy subcommands: space‑separated command paths like `user add`; longest match wins
- Strong flag validation via `[Flag]` attributes (required, arity, alias, regex)
- Sequential multi‑command execution (stops on first non‑zero exit code)
- Built‑in `help` command with hierarchical subcommand listings
- Test‑friendly output via `ITextWriter`

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

### 1) Wire up the runtime
Create or update your app entry point to run commands using `ClxRuntime`.

```csharp
using CLX.Core;

return new ClxRuntime().Run(args);
```

### 2) Create a command
Implement `ICommand`. Define flags using `[Flag]` attributes on properties (the values are retrieved from the execution context).

```csharp
using CLX.Core.Commands;

sealed class HelloCommand : ICommand
{
    public string Name => "hello";
    public string Description => "Print a greeting.";
    public ITextWriter? Output { get; } = null;
    public ITextWriter? ErrorOutput { get; } = null;
    public string WorkingDirectory { get; } = string.Empty;

    [Flag("name", AlternateName = "n", MinValues = 1, MaxValues = 1, IsRequired = true)]
    private int _sinkName { get; set; }

    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        if (!ICommandContext.TryGetFlag<IFlagObject>(context, "name", out var nameFlag) || nameFlag == null)
            return -1;

        var name = nameFlag.Values[0];
        context.Output.WriteLine($"Hello, {name}!");
        return 0;
    }
}
```

Run it:
```bash
your-app hello --name Alice
```

### 3) Subcommands
Define subcommands by using a space‑separated `Name` (e.g., `user add`). Flags must come after the full path.

```csharp
using CLX.Core.Commands;

sealed class UserAddCommand : ICommand
{
    public string Name => "user add";
    public string Description => "Add a new user.";
    public ITextWriter? Output { get; } = null;
    public ITextWriter? ErrorOutput { get; } = null;
    public string WorkingDirectory { get; } = string.Empty;

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
your-app hello --name Bob user add --name Alice
```

---

## Help
CLX ships a built‑in `help` command. When present, it discovers commands and displays a hierarchy.

```bash
# Top‑level index
your-app help

# Details for a path with flags and subcommands
your-app help --for user
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

    static bool TryGetFlag<T>(ICommandContext context, string flagName, out T? flag) where T : class, IFlagObject;
}

// Flags: annotate properties to declare CLI contract
[AttributeUsage(AttributeTargets.Property)]
public class FlagAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public string? AlternateName { get; set; }
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
- No positional arguments (all inputs are via flags)

Planned: publish a NuGet package and explore global flags/positional args support.

---

## License
MIT License (see copyright notices).


