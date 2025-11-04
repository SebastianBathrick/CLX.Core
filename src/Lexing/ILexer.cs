using CLX.Core.Nodes;

namespace CLX.Core.Lexing;

/// <summary> Produces command/flag/value <see cref="CommandNode"/> trees from raw CLI arguments. </summary>
/// <remarks> Exposes a Try-style API that reports the first unexpected token instead of throwing,
/// enabling graceful error handling in the runtime. </remarks>
interface ILexer
{
    /// <summary> Tries to lex arguments into a sequence of root command nodes. </summary>
    /// <remarks> Returns false if an unexpected token is encountered; <paramref name="errorArg"/>
    /// contains that token when available. </remarks>
    /// <param name="args">The raw command-line arguments.</param>
    /// <param name="cmdNames">The set of valid command names.</param>
    /// <param name="nodes">On success, the created root nodes; otherwise, empty.</param>
    /// <param name="errorArg">On failure, the unexpected argument token; otherwise, empty.</param>
    /// <returns>True if lexing succeeded; otherwise, false.</returns>
    bool TryCreateCommandNodes(string[] args, HashSet<string> cmdNames, out IReadOnlyList<CommandNode> nodes, out string errorArg);
}
