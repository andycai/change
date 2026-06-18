using System;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Default CQRS bootstrap that owns registration and exposes the bus as a runtime.
    /// <para>
    /// After the first <see cref="Build"/>, subsequent calls return the same
    /// <see cref="ICqrsRuntime"/> instance (the underlying CqrsBus directly).
    /// Registration and dispatch are both available through the returned bus.
    /// </para>
    /// </summary>
    public sealed class CqrsBootstrap : ICqrsBootstrap
    {
        private readonly CqrsBus _bus;
        private readonly object _buildGate = new();
        private volatile ICqrsRuntime _runtime;

        public CqrsBootstrap()
            : this(new CqrsBus())
        {
        }

        public CqrsBootstrap(CqrsBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand
        {
            _bus.RegisterCommand(handler);
        }

        public void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            _bus.RegisterQuery(handler);
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            _bus.Subscribe(handler);
        }

        public ICqrsRuntime Build()
        {
            var runtime = _runtime;
            if (runtime != null)
            {
                return runtime;
            }

            lock (_buildGate)
            {
                if (_runtime != null)
                {
                    return _runtime;
                }

                _runtime = _bus;
                return _runtime;
            }
        }
    }
}
