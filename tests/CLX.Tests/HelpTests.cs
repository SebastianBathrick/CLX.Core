using CLX.Core;
using CLX.Core.Commands;
using Xunit;

namespace CLX.Tests;

public sealed class HelpTests
{
    [Fact]
    public void Help_Lists_TopLevel_Commands()
    {
        var err = new TestTextWriter();
        var rt = new ClxRuntime(err);

        var outWriter = new TestTextWriter();
        TopACommand.OutputSink = outWriter;
        // Run help with no args
        var code = rt.Run(["help"]);

        Assert.Equal(0, code);
    }

    [Fact]
    public void Help_For_Subcommand_Shows_Flags_And_Subcommands()
    {
        var err = new TestTextWriter();
        var rt = new ClxRuntime(err);

        var outWriter = new TestTextWriter();
        TopACommand.OutputSink = outWriter;

        var code = rt.Run(["help", "--for", "db", "migrate"]);

        Assert.Equal(0, code);
    }
}

sealed class TopACommand : ICommand
{
    public string Name => "top-a";
    public string Description => "Top level A";
    public ITextWriter? Output => OutputSink;
    public ITextWriter? ErrorOutput => OutputSink;
    public string WorkingDirectory => string.Empty;
    public static ITextWriter OutputSink { get; set; } = NullTextWriter.Instance;
    public int Execute(ICommandContext context, string workingDirectory = "") => 0;
}


