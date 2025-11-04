using CLX.Core.Nodes;
using CLX.Core.Commands;

namespace CLX.Core.Parsing;

using ValidCommands = IReadOnlyDictionary<string, ICommand>;
using Contexts = IReadOnlyList<ICommandContext>;

/// <summary> Converts lexical <see cref="INode"/> trees into executable
/// <see cref="ICommandContext"/>s. </summary>
/// <remarks> Provides a Try-style API that reports failure without throwing and returns a
/// descriptive error message. </remarks>
interface IParser
{
    /// <summary> Tries to create command contexts from a list of root nodes. </summary>
    /// <remarks> Returns false if any node has an unexpected shape; no exceptions are thrown. </remarks>
    /// <param name="nodes">The root nodes produced by the lexer.</param>
    /// <param name="commands">The set of valid commands to match against.</param>
    /// <param name="contexts">On success, the created contexts; otherwise, an empty list.</param>
    /// <param name="errorMessage">On failure, a human-readable explanation.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    bool TryCreateCommandContexts(IReadOnlyList<CommandNode> nodes, ValidCommands commands, out Contexts contexts, out string errorMessage);
}
