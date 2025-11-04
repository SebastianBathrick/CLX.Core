using CLX.Core.Commands;

namespace CLX.Core.Help;

sealed class CommandTreeNode
{
    public string Name { get; }
    public string FullPath { get; }
    public ICommand? Command { get; }
    public List<CommandTreeNode> Children { get; } = [];

    public CommandTreeNode(string name, string fullPath, ICommand? command)
    {
        Name = name;
        FullPath = fullPath;
        Command = command;
    }
}


