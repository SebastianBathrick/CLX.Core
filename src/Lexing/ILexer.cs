using CLX.Core.Nodes;

namespace CLX.Core.Lexing;

interface ILexer
{
    /// <summary> Tries to lex arguments into a list of trees made up of nodes. </summary>
    /// <param name="args"> The arguments to lex. </param>
    /// <param name="cmdNames"> The names of the commands to lex. </param>
    /// <param name="nodes"> When this method returns, contains the resulting nodes if successful; otherwise, an empty list. </param>
    /// <returns> True if lexing succeeded; otherwise, false. </returns>
    bool TryCreateCommandNodes(string[] args, HashSet<string> cmdNames, out IReadOnlyList<INode> nodes, out string errorArg);
}
