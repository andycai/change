namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Marks a struct command as self-handling: it executes its own logic via
    /// <see cref="Execute"/> with no external handler registration.
    /// </summary>
    /// <remarks>
    /// Implement as <c>readonly struct</c>. Dispatch is zero-allocation: the bus
    /// invokes <see cref="Execute"/> through a per-type cached constrained call,
    /// avoiding boxing. A command type cannot be both self-handling and registered
    /// with <see cref="ICommandHandler{TCommand}"/>.
    /// </remarks>
    public interface ISelfHandlingCommand : ICommand
    {
        void Execute();
    }
}
