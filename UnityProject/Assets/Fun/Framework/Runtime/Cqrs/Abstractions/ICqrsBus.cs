namespace Fun.Framework.Cqrs
{
    public interface ICqrsBus
    {
        void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand;
    }
}
