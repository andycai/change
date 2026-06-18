namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Mutable registration surface for CQRS handlers.
    /// Registration and unregistration may be called at any time on the main thread.
    /// Thread safety is the caller's responsibility.
    /// </summary>
    public interface ICqrsRegistry
    {
        void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;

        void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>;

        void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;

        void UnregisterCommand<TCommand>()
            where TCommand : struct, ICommand;

        void UnregisterQuery<TQuery, TResult>()
            where TQuery : struct, IQuery<TResult>;

        void Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;
    }
}
