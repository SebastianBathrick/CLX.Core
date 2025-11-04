using System.Collections.Concurrent;
using CLX.Core;
using CLX.Core.Commands;
using Xunit;

namespace CLX.Tests;

public sealed class ParserAndLexerBehaviorTests
{
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
        Assert.True(msg.Contains("Flag num") && msg.Contains("must have"));
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
        Assert.Contains("does not match regex", err.ToString());
    }

    [Fact]
    public void MinValues_TooFew_Fails()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([MatrixCommand.NameConst, "--num", "1", "--items"]);

        Assert.Equal(-1, code);
        Assert.Contains("must have 1 to 2 values", err.ToString());
    }

    [Fact]
    public void MaxValues_TooMany_Fails()
    {
        var err = new TestWriter();
        var rt = new ClxRuntime(err);

        var code = rt.Run([MatrixCommand.NameConst, "--num", "1", "--items", "a", "b", "c"]);

        Assert.Equal(-1, code);
        Assert.Contains("must have 1 to 2 values", err.ToString());
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
        Assert.Contains("must have 0 to 0 values", err.ToString());
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
        Assert.True(msg.Contains("Flag num") && msg.Contains("must have"));
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
        Assert.Contains("is not supported for this command", err.ToString());
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


