using CLX.Core.Nodes;
using CLX.Core.Context;

namespace CLX.Core.Parsing;

interface IParser
{
    /// <summary> Tries to convert nodes into executable command contexts. </summary>
    /// <param name="nodes"> The nodes to parse. </param>
    /// <param name="contexts"> When this method returns, contains the resulting contexts if successful; otherwise, an empty list. </param>
    /// <returns> True if parsing succeeded; otherwise, false. </returns>
    bool TryCreateCommandContexts(IReadOnlyList<INode> nodes, out IReadOnlyList<ICommandContext> contexts, out string errorMessage);
}
