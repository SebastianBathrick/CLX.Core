namespace CLX.Core.Exceptions;

public sealed class ClxRuntimeException : AggregateException
{
    public ClxRuntimeException(IEnumerable<Exception> innerExceptions)
        : base($"Command Execute(ICommandContext) exception(s) thrown:\n{string.Join("\n", innerExceptions)}", innerExceptions) { }
}

