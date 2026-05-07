namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Handles domain event messages.
    /// Kept compatible with <see cref="IEventHandler{TEvent}"/> so existing event infrastructure remains unchanged.
    /// </summary>
    public interface IDomainEventHandler<TDomainEvent> : IEventHandler<TDomainEvent>
        where TDomainEvent : struct, IDomainEvent
    {
    }
}
