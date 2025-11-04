using CLX.Core.Commands;
using System.Reflection;

namespace CLX.Core.Help;

sealed class CommandTreeBuilder
{
    public static CommandTreeNode BuildTree(IReadOnlyDictionary<string, ICommand> commands)
    {
        var root = new CommandTreeNode(name: string.Empty, fullPath: string.Empty, command: null);

        foreach (var (path, cmd) in commands)
        {
            var parts = path.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            var prefix = new List<string>();

            for (int i = 0; i < parts.Length; i++)
            {
                prefix.Add(parts[i]);
                var childPath = string.Join(' ', prefix);
                var child = current.Children.FirstOrDefault(c => c.Name == parts[i]);
                if (child == null)
                {
                    child = new CommandTreeNode(parts[i], childPath, i == parts.Length - 1 ? cmd : null);
                    current.Children.Add(child);
                }
                else if (i == parts.Length - 1)
                {
                    // End node holds command
                    current.Children.Remove(child);
                    child = new CommandTreeNode(parts[i], childPath, cmd);
                    current.Children.Add(child);
                }
                current = child;
            }
        }

        // Sort children for stable output
        SortRecursive(root);
        return root;
    }

    static void SortRecursive(CommandTreeNode node)
    {
        node.Children.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        foreach (var child in node.Children)
            SortRecursive(child);
    }

    public static bool TryFindNode(CommandTreeNode root, IEnumerable<string> pathParts, out CommandTreeNode? node)
    {
        node = root;
        foreach (var part in pathParts)
        {
            node = node!.Children.FirstOrDefault(c => c.Name == part);
            if (node == null) return false;
        }
        return true;
    }
}


