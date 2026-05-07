using System;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Default CQRS bootstrap that owns registration and creates a single runtime instance.
    /// </summary>
    public sealed class CqrsBootstrap : ICqrsBootstrap, ICqrsRuntimeProvider
    {
        private readonly CqrsBus _bus;
        private ICqrsRuntime _runtime;

        public CqrsBootstrap()
            : this(new CqrsBus())
        {
        }

        public CqrsBootstrap(CqrsBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public ICqrsRuntime Runtime => _runtime ?? throw new InvalidOperationException("Build must be called before runtime access.");

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
            if (_runtime != null)
            {
                return _runtime;
            }

            _bus.Freeze();
            _runtime = new CqrsRuntime(_bus);
            return _runtime;
        }
    }
}
