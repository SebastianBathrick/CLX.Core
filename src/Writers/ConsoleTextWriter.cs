using System;

namespace CLX.Core.Writers;

/// <summary> Console-backed implementation of <see cref="ITextWriter"/>. </summary>
public sealed class ConsoleTextWriter : ITextWriter
{
    /// <summary> Shared instance for convenience. </summary>
    public static readonly ConsoleTextWriter Instance = new();

    private ConsoleTextWriter() { }

    public void Write(string text) => Console.Write(text);
    public void WriteLine(string text) => Console.WriteLine(text);
}


