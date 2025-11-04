namespace CLX.Core.Context;

interface IFlagInstance
{
    string Name { get; }
    IReadOnlyList<string> Values { get; }
}
