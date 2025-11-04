using CLX.Core.Context;

namespace CLX.Core.Commands;

interface ICommand
{
    string Name { get; }
    string Description { get; }

    int Execute(ICommandContext context, string workingDirectory = "");
}
