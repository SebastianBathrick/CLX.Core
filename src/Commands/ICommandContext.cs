namespace CLX.Core.Context;

interface ICommandContext
{
    string CommandName { get; }

    IReadOnlyList<IFlagInstance> Flags { get; }

    ITextWriter Output { get; }

    ITextWriter ErrorOutput { get; }

    string WorkingDirectory { get; }


    static bool TryGetFlag<T>(ICommandContext context, string flagName, out T? flagInstance) where T : class, IFlagInstance
    {
        foreach(var flag in context.Flags)
            if(flag.Name == flagName && flag is T castedFlag)
            {
                flagInstance = castedFlag;
                return true;
            }    

        flagInstance = default;
        return false;
    }
}
