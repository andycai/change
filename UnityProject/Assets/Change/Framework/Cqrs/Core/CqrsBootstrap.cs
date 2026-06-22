using System;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Default CQRS bootstrap that owns event subscription and exposes the bus as a runtime.
    /// <para>
    /// After the first <see cref="Build"/>, subsequent calls return the same
    /// <see cref="ICqrsBus"/> instance (the underlying CqrsBus directly).
    /// </para>
    /// </summary>
    public sealed class CqrsBootstrap : ICqrsBootstrap
    {
        private readonly CqrsBus _bus;
        private readonly object _buildGate = new();
        private volatile ICqrsBus _runtime;

        public CqrsBootstrap()
            : this(new CqrsBus())
        {
        }

        public CqrsBootstrap(CqrsBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            _bus.Subscribe(handler);
        }

        public void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent
        {
            _bus.Subscribe(handler);
        }

        public ICqrsBus Build()
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
