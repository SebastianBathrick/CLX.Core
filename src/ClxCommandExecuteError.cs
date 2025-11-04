using CLX.Core.Commands;

namespace CLX.Core;

public sealed class ClxCommandExecuteError : AggregateException
{
    public ClxCommandExecuteError(IEnumerable<Exception> innerExceptions)
        : base($"Command Execute(ICommandContext) exception(s) thrown:\n{string.Join("\n", innerExceptions)}", innerExceptions) { }
}

