namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Mutable CQRS bootstrap surface used to register handlers before runtime creation.
    /// After <see cref="Build"/> is called, registrations are no longer valid.
    /// </summary>
    public interface ICqrsBootstrap
    {
        void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;

        void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>;

        void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;

        /// <summary>
        /// Freezes registration and returns the runtime dispatch surface.
        /// Implementations must return the same <see cref="ICqrsRuntime"/> instance on every
        /// subsequent call and must be safe to invoke concurrently.
        /// </summary>
        ICqrsRuntime Build();
    }
}
