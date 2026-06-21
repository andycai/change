using System;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Mutable registration surface for CQRS event handlers only.
    /// Command/Query handler registration has been removed — all commands and queries
    /// are now self-handling structs with <c>Execute()</c>/<c>Query()</c> methods.
    /// </summary>
    public interface ICqrsRegistry
    {
        void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;

        /// <summary>
        /// Subscribes a static delegate. The delegate must NOT capture variables
        /// (i.e. must be a static method or non-capturing lambda); a capturing
        /// delegate throws <see cref="ClosureCaptureException"/>.
        /// </summary>
        void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent;

        void Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;

        /// <summary>
        /// Unsubscribes a delegate previously registered via
        /// <see cref="Subscribe{TEvent}(Action{TEvent})"/>. Matching is by exact
        /// delegate reference. No-op if not found.
        /// </summary>
        void Unsubscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent;
    }
}
