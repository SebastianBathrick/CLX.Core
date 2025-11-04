using System.Collections.Concurrent;
using CLX.Core;
using CLX.Core.Commands;
using Xunit;

namespace CLX.Tests;

public sealed class ParserAndLexerBehaviorTests
{
    [Fact]
    public void Composite_LongestMatch_IsChosen()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        CompositeState.Reset();
        var code = rt.Run([DbRootCommand.NameConst, "migrate", "run", "--dry-run"]);

        Assert.Equal(0, code);
        Assert.Equal("db migrate run", CompositeState.LastExecuted);
        Assert.True(CompositeState.DryRun);
    }

    [Fact]
    public void Composite_Overlapping_Picks_Longest_Then_Allows_Next_Command()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        CompositeState.Reset();
        // First match: "db migrate" (longest before flags), then next command: "db" afterwards
        var code = rt.Run([DbRootCommand.NameConst, "migrate", "--fast", DbRootCommand.NameConst]);

        Assert.Equal(0, code);
        Assert.Equal(new[] { "db migrate", "db" }, CompositeState.ExecutionLog.ToArray());
        Assert.True(CompositeState.Fast);
    }

    [Fact]
    public void Unknown_Subcommand_Part_Is_Error()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([DbRootCommand.NameConst, "unknownpiece"]);

        Assert.Equal(-1, code);
        Assert.Contains("Unexpected argument", err.ToString());
    }

    [Fact]
    public void LongFlag_KebabCase_Valid()
    {
        MatrixCommand.Reset();
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([MatrixCommand.NameConst, "--num", "1", "--items", "a", "b", "--toggle"]);

        Assert.Equal(0, code);
        Assert.True(MatrixCommand.Executed);
        Assert.Equal(new[] { "a", "b" }, MatrixCommand.ItemsValues.ToArray());
        Assert.True(MatrixCommand.TryGetAltItems);
    }

    [Fact]
    public void LongFlag_Invalid_ConsecutiveDashes_IsUnexpected()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([MatrixCommand.NameConst, "--num", "2", "--my--flag"]);

        Assert.Equal(-1, code);
        var msg = err.ToString();
        Assert.True(msg.Contains("Flag '--num'") && msg.Contains("expects"));
    }

    [Fact]
    public void ShortFlag_SingleLetter_AlternateName_Works()
    {
        MatrixCommand.Reset();
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([MatrixCommand.NameConst, "-n", "3", "-i", "x"]);

        Assert.Equal(0, code);
        Assert.True(MatrixCommand.Executed);
        Assert.Equal(new[] { "x" }, MatrixCommand.ItemsValues.ToArray());
        Assert.True(MatrixCommand.TryGetAltItems);
    }

    [Fact]
    public void ShortFlag_Uppercase_IsInvalid()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([MatrixCommand.NameConst, "-N"]);

        Assert.Equal(-1, code);
        Assert.Contains("Unexpected argument", err.ToString());
    }

    [Fact]
    public void Regex_Invalid_Value_Fails()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([MatrixCommand.NameConst, "--num", "abc"]);

        Assert.Equal(-1, code);
        Assert.Contains("Invalid value 'abc'", err.ToString());
    }

    [Fact]
    public void MinValues_TooFew_Fails()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([MatrixCommand.NameConst, "--num", "1", "--items"]);

        Assert.Equal(-1, code);
        Assert.Contains("expects 1..2 values", err.ToString());
    }

    [Fact]
    public void MaxValues_TooMany_Fails()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([MatrixCommand.NameConst, "--num", "1", "--items", "a", "b", "c"]);

        Assert.Equal(-1, code);
        Assert.Contains("expects 1..2 values", err.ToString());
    }

    [Fact]
    public void ToggleFlag_NoValues_Allowed_ButValues_Fails()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var codeOk = rt.Run([MatrixCommand.NameConst, "--num", "1", "--toggle"]);
        Assert.Equal(0, codeOk);

        var codeFail = rt.Run([MatrixCommand.NameConst, "--num", "1", "--toggle", "x"]);
        Assert.Equal(-1, codeFail);
        Assert.Contains("expects 0..0 values", err.ToString());
    }

    [Fact]
    public void Values_Stop_At_Next_Flag_Or_Command()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        // Extra stray value after valid flags should be unexpected
        var code = rt.Run([MatrixCommand.NameConst, "--num", "1", "x"]);

        Assert.Equal(-1, code);
        var msg = err.ToString();
        Assert.True(msg.Contains("Flag '--num'") && msg.Contains("expects"));
    }

    [Fact]
    public void MultiCommand_AllSuccess()
    {
        CountingCommand.Reset();
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([CountingCommand.NameConst, MatrixCommand.NameConst, "--num", "1"]);

        Assert.Equal(0, code);
        Assert.Equal(1, CountingCommand.Count);
        Assert.True(MatrixCommand.Executed);
    }

    [Fact]
    public void Runtime_Reports_Failure_ExitCode()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([FailCodeCommand.NameConst]);

        Assert.Equal(-1, code);
        Assert.Contains("failed with exit code 7", err.ToString());
    }

    [Fact]
    public void Runtime_Captures_Unhandled_Exception()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([ThrowCommand.NameConst]);

        Assert.Equal(-1, code);
        Assert.NotNull(rt.Exception);
        Assert.Contains("Unhandled exception:", err.ToString());
        Assert.Contains("boom", rt.Exception!.Message);
    }

    [Fact]
    public void Execute_Receives_Provided_WorkingDirectory()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var wd = "C:/tmp/testing";
        var code = rt.Run([WDCommand.NameConst], wd);

        Assert.Equal(0, code);
        Assert.Equal(wd, WDCommand.ObservedWorkingDirectory);
    }

    [Fact]
    public void UnsupportedFlag_Reports_Clear_Message()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([NoFlagsCommand.NameConst, "--x"]);

        Assert.Equal(-1, code);
        Assert.Contains("does not accept flags", err.ToString());
    }
}

sealed class TestWriter : ITextWriter
{
    private readonly System.Text.StringBuilder _sb = new();
    public void Write(string text) => _sb.Append(text);
    public void WriteLine(string text) => _sb.AppendLine(text);
    public override string ToString() => _sb.ToString();
}

static class MatrixState
{
    public static bool Executed;
    public static bool TryGetAltItems;
    public static List<string> ItemsValues = new();
    public static void Reset()
    {
        Executed = false;
        TryGetAltItems = false;
        ItemsValues.Clear();
    }
}

sealed class MatrixCommand : ICommand
{
    public const string NameConst = "matrix";
    public string Name => NameConst;
    public string Description => "Matrix flags";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;

    [Flag("num", AlternateName = "n", IsRequired = true, MinValues = 1, MaxValues = 1, ValueRegexPattern = "^\\d+$")]
    private int _sink1 { get; set; }

    [Flag("items", AlternateName = "i", MinValues = 1, MaxValues = 2)]
    private int _sink2 { get; set; }

    [Flag("toggle", AlternateName = "t", MinValues = 0, MaxValues = 0)]
    private int _sink3 { get; set; }

    public static bool Executed => MatrixState.Executed;
    public static bool TryGetAltItems => MatrixState.TryGetAltItems;
    public static IReadOnlyList<string> ItemsValues => MatrixState.ItemsValues;
    public static void Reset() => MatrixState.Reset();

    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        MatrixState.Executed = true;

        if (ICommandContext.TryGetFlag<IFlagObject>(context, "items", out var items) && items is not null)
            MatrixState.ItemsValues = new List<string>(items.Values);

        // Also try by alternate name
        MatrixState.TryGetAltItems = ICommandContext.TryGetFlag<IFlagObject>(context, "i", out _);

        return 0;
    }
}

sealed class CountingCommand : ICommand
{
    public const string NameConst = "count";
    public static int Count;
    public static void Reset() => Count = 0;

    public string Name => NameConst;
    public string Description => "counts";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;
    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        Count++;
        return 0;
    }
}

sealed class FailCodeCommand : ICommand
{
    public const string NameConst = "fail";
    public string Name => NameConst;
    public string Description => "fails with code 7";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;
    public int Execute(ICommandContext context, string workingDirectory = "") => 7;
}

sealed class ThrowCommand : ICommand
{
    public const string NameConst = "throw";
    public string Name => NameConst;
    public string Description => "throws";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;
    public int Execute(ICommandContext context, string workingDirectory = "")
        => throw new InvalidOperationException("boom");
}

sealed class WDCommand : ICommand
{
    public const string NameConst = "wd-echo";
    public string Name => NameConst;
    public string Description => "wd";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;
    public static string ObservedWorkingDirectory = string.Empty;
    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        ObservedWorkingDirectory = workingDirectory;
        return 0;
    }
}

sealed class NoFlagsCommand : ICommand
{
    public const string NameConst = "noflags";
    public string Name => NameConst;
    public string Description => "no flags";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;
    public int Execute(ICommandContext context, string workingDirectory = "") => 0;
}

static class CompositeState
{
    public static string LastExecuted = string.Empty;
    public static List<string> ExecutionLog { get; } = new();
    public static bool DryRun;
    public static bool Fast;
    public static void Reset()
    {
        LastExecuted = string.Empty;
        ExecutionLog.Clear();
        DryRun = false;
        Fast = false;
    }
}

sealed class DbRootCommand : ICommand
{
    public const string NameConst = "db";
    public string Name => NameConst;
    public string Description => "db root";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;
    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        CompositeState.LastExecuted = Name;
        CompositeState.ExecutionLog.Add(Name);
        return 0;
    }
}

sealed class DbMigrateCommand : ICommand
{
    public const string NameConst = "db migrate";
    public string Name => NameConst;
    public string Description => "db migrate";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;

    [Flag("fast", AlternateName = "f", MinValues = 0, MaxValues = 0)]
    private int _sink { get; set; }

    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        CompositeState.LastExecuted = Name;
        CompositeState.ExecutionLog.Add(Name);
        CompositeState.Fast = ICommandContext.TryGetFlag<IFlagObject>(context, "fast", out _);
        return 0;
    }
}

sealed class DbMigrateRunCommand : ICommand
{
    public const string NameConst = "db migrate run";
    public string Name => NameConst;
    public string Description => "db migrate run";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;

    [Flag("dry-run", AlternateName = "d", MinValues = 0, MaxValues = 0)]
    private int _sink { get; set; }

    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        CompositeState.LastExecuted = Name;
        CompositeState.ExecutionLog.Add(Name);
        CompositeState.DryRun = ICommandContext.TryGetFlag<IFlagObject>(context, "dry-run", out _);
        return 0;
    }
}

sealed class DbOnlyCommand : ICommand
{
    public string Name => DbRootCommand.NameConst;
    public string Description => "db only";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;
    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        CompositeState.LastExecuted = Name;
        CompositeState.ExecutionLog.Add(Name);
        return 0;
    }
}


