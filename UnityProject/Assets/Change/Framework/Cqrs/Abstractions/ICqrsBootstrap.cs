using System;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Mutable CQRS bootstrap surface used to register handlers and delegates.
    /// Registration remains valid before and after <see cref="Build"/>; the returned
    /// <see cref="ICqrsRuntime"/> is the underlying bus, which also exposes the
    /// <see cref="ICqrsRegistry"/> registration surface.
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
        /// Subscribes a static delegate. Must not capture variables; a capturing
        /// delegate throws <see cref="ClosureCaptureException"/>.
        /// </summary>
        void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent;

        /// <summary>
        /// Returns the runtime dispatch surface (the underlying bus).
        /// Implementations must return the same <see cref="ICqrsRuntime"/> instance on
        /// every subsequent call and must be safe to invoke concurrently.
        /// </summary>
        ICqrsRuntime Build();

        void RegisterAsyncCommand<TCommand>(IAsyncCommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;

        void RegisterAsyncQuery<TQuery, TResult>(IAsyncQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>;
    }
}
