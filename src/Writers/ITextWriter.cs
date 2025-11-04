namespace CLX.Core.Writers;

/// <summary> Abstraction over text output for commands. </summary>
/// <remarks> Enables testing and redirection of output and error streams. </remarks>
public interface ITextWriter
{
    /// <summary> Writes text without a newline. </summary>
    /// <param name="text">The text to write.</param>
    void Write(string text);

    /// <summary> Writes text followed by a newline. </summary>
    /// <param name="text">The text to write.</param>
    void WriteLine(string text);
}
