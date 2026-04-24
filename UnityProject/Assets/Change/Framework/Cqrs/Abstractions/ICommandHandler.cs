namespace Change.Framework.Cqrs
{
    public interface ICommandHandler<TCommand>
        where TCommand : struct, ICommand
    {
        void Handle(in TCommand command);
    }
}
