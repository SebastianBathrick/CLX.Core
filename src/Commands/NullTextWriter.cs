using CLX.Core.Context;

namespace CLX.Core.Parsing;

internal partial class Parser
{
    private sealed class NullTextWriter : ITextWriter
    {
        public static readonly NullTextWriter Instance = new NullTextWriter();
        private NullTextWriter() { }
        public void Write(string text) { }
        public void WriteLine(string text) { }
    }
}