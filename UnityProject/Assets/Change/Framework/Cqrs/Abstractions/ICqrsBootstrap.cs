using System;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// CQRS bootstrap surface used to subscribe event handlers before runtime creation.
    /// Command/Query handler registration has been removed — all commands and queries
    /// are now self-handling structs and require no registration.
    /// </summary>
    public interface ICqrsBootstrap
    {
        void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;

        /// <summary>
        /// Subscribes a static delegate. The delegate must NOT capture variables;
        /// a capturing delegate throws <see cref="ClosureCaptureException"/>.
        /// </summary>
        void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent;

        /// <summary>
        /// Returns the runtime dispatch surface. Implementations must return the same
        /// <see cref="ICqrsRuntime"/> instance on every subsequent call and must be safe
        /// to invoke concurrently.
        /// </summary>
        ICqrsRuntime Build();
    }
}
