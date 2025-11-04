namespace CLX.Core.Commands;

/// <summary> An <see cref="ITextWriter"/> implementation that discards all output. </summary>
/// <remarks> Exposed via the <see cref="Instance"/> singleton for convenience. </remarks>
public sealed class NullTextWriter : ITextWriter
{
    /// <summary> Gets the singleton instance. </summary>
    public static readonly NullTextWriter Instance = new();

    /// <summary> Initializes a new instance of the <see cref="NullTextWriter"/> class. </summary>
    private NullTextWriter() { }

    /// <summary> Writes text with no effect. </summary>
    /// <param name="text">The text to discard.</param>
    public void Write(string text) { }

    /// <summary> Writes a line of text with no effect. </summary>
    /// <param name="text">The text to discard.</param>
    public void WriteLine(string text) { }
}
