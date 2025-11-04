using CLX.Core.Context;

namespace CLX.Core.Parsing;

internal partial class Parser
{
    private sealed class FlagInstance : IFlagInstance
    {
        public FlagInstance(string name, IReadOnlyList<string> values)
        {
            Name = name;
            Values = values;
        }

        public string Name { get; }
        public IReadOnlyList<string> Values { get; }
    }
}