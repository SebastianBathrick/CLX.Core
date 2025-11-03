namespace CLX.Lexing;

interface ICommandLexer
{
    IReadOnlyList<ILexerNode> GetNodes(string[] args, HashSet<string> cmdNames, out int endArgsIndex);
}
