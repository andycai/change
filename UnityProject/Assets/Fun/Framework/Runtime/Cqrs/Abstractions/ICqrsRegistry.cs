namespace Fun.Framework.Cqrs
{
    public interface ICqrsRegistry
    {
        void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;

        void Freeze();
    }
}
