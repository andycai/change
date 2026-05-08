namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Handles command messages. Implement as a class to avoid interface boxing on registration.
    /// Registration enforces this at runtime: value-type implementations are rejected with
    /// <see cref="System.InvalidOperationException"/>.
    /// </summary>
    public interface ICommandHandler<TCommand>
        where TCommand : struct, ICommand
    {
        void Handle(in TCommand command);
    }
}
