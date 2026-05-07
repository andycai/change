using System;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Default immutable CQRS runtime that delegates dispatch to a frozen bus.
    /// </summary>
    public sealed class CqrsRuntime : ICqrsRuntime
    {
        private readonly ICqrsBus _bus;

        public CqrsRuntime(ICqrsBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            _bus.Send(in command);
        }

        public TResult Ask<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            return _bus.Query<TQuery, TResult>(in query);
        }

        public void Publish<TDomainEvent>(in TDomainEvent domainEvent)
            where TDomainEvent : struct, IDomainEvent
        {
            _bus.Publish(in domainEvent);
        }
    }
}
