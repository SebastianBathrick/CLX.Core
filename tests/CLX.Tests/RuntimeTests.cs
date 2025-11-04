using System.Collections.Concurrent;
using CLX.Core;
using CLX.Core.Commands;
using Xunit;

namespace CLX.Tests;

public sealed class RuntimeTests
{
    [Fact]
    public void Run_ReturnsError_OnUnknownCommand()
    {
        var err = new TestTextWriter();
        var runtime = new ClxRuntime(err);

        var code = runtime.Run(["totally-unknown"]);

        Assert.Equal(-1, code);
        Assert.Contains("Unexpected argument", err.ToString());
    }

    [Fact]
    public void Run_ReturnsError_OnInvalidShortFlag()
    {
        var err = new TestTextWriter();
        var runtime = new ClxRuntime(err);

        // Ensure at least one known command exists in the assembly (OkCommand below)
        var code = runtime.Run([OkCommand.CommandName, "-ab"]);

        Assert.Equal(-1, code);
        Assert.Contains("Unexpected argument", err.ToString());
    }

    [Fact]
    public void Run_Succeeds_WithRequiredFlag()
    {
        RequiredFlagCommand.Reset();

        var err = new TestTextWriter();
        var runtime = new ClxRuntime(err);

        var code = runtime.Run([RequiredFlagCommand.CommandName, "--num", "123"]);

        Assert.Equal(0, code);
        Assert.True(RequiredFlagCommand.Executed);
        Assert.Equal(new[] { "123" }, RequiredFlagCommand.ObservedValues.ToArray());
    }

    [Fact]
    public void Run_Fails_WhenRequiredFlagMissing()
    {
        RequiredFlagCommand.Reset();

        var err = new TestTextWriter();
        var runtime = new ClxRuntime(err);

        var code = runtime.Run([RequiredFlagCommand.CommandName]);

        Assert.Equal(-1, code);
        Assert.Contains("Missing required flag", err.ToString());
        Assert.False(RequiredFlagCommand.Executed);
    }

    [Fact]
    public void Run_StopsOnFirstFailure()
    {
        SequenceCommand.Reset();

        var err = new TestTextWriter();
        var runtime = new ClxRuntime(err);

        var code = runtime.Run([
            SequenceCommand.OkName,
            SequenceCommand.FailName,
            SequenceCommand.OkName
        ]);

        Assert.Equal(-1, code);
        Assert.Equal(2, SequenceCommand.InvocationCount); // ok then fail; third should not run
        Assert.Equal(new[] { SequenceCommand.OkName, SequenceCommand.FailName }, SequenceCommand.InvocationOrder.ToArray());
    }
}

sealed class TestTextWriter : ITextWriter
{
    private readonly System.Text.StringBuilder _sb = new();
    public void Write(string text) => _sb.Append(text);
    public void WriteLine(string text) => _sb.AppendLine(text);
    public override string ToString() => _sb.ToString();
}

sealed class OkCommand : ICommand
{
    public const string CommandName = "ok";
    public string Name => CommandName;
    public string Description => "OK";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;
    public int Execute(ICommandContext context, string workingDirectory = "") => 0;
}

sealed class RequiredFlagCommand : ICommand
{
    public const string CommandName = "calc";
    public string Name => CommandName;
    public string Description => "Requires a numeric flag";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;

    [Flag("num", AlternateName = "n", IsRequired = true, MinValues = 1, MaxValues = 1, ValueRegexPattern = "^\\d+$")]
    private int _numSink { get; set; } // property is not used by parser beyond holding the attribute

    public static bool Executed { get; private set; }
    public static IReadOnlyList<string> ObservedValues { get; private set; } = [];
    public static void Reset()
    {
        Executed = false;
        ObservedValues = [];
    }

    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        Executed = true;
        if (ICommandContext.TryGetFlag<IFlagObject>(context, "num", out var flag) && flag is not null)
            ObservedValues = flag.Values;
        return 0;
    }
}

static class SequenceCommand
{
    public const string OkName = "seq-ok";
    public const string FailName = "seq-fail";

    public static int InvocationCount;
    public static ConcurrentQueue<string> InvocationOrder { get; } = new();
    public static void Reset()
    {
        InvocationCount = 0;
        while (InvocationOrder.TryDequeue(out _)) { }
    }
}

sealed class SeqOkCommand : ICommand
{
    public string Name => SequenceCommand.OkName;
    public string Description => "Sequence OK";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;
    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        SequenceCommand.InvocationCount++;
        SequenceCommand.InvocationOrder.Enqueue(Name);
        return 0;
    }
}

sealed class SeqFailCommand : ICommand
{
    public string Name => SequenceCommand.FailName;
    public string Description => "Sequence FAIL";
    public ITextWriter Output { get; } = NullTextWriter.Instance;
    public ITextWriter ErrorOutput { get; } = NullTextWriter.Instance;
    public string WorkingDirectory { get; } = string.Empty;
    public int Execute(ICommandContext context, string workingDirectory = "")
    {
        SequenceCommand.InvocationCount++;
        SequenceCommand.InvocationOrder.Enqueue(Name);
        return 123; // non-zero triggers stop
    }
}


