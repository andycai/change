namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Marker interface for domain event messages.
    /// Domain events are a semantic subset of <see cref="IEvent"/> and share the same dispatch path.
    /// <para>
    /// Dispatch rule: <c>Publish&lt;TDomainEvent&gt;</c> matches subscribers strictly by the
    /// closed generic type <c>TDomainEvent</c>. Handlers subscribed against base interfaces
    /// (for example <see cref="IEvent"/> or <see cref="IDomainEvent"/>) are not invoked when
    /// a derived event is published. Subscribe each concrete event type explicitly.
    /// </para>
    /// </summary>
    public interface IDomainEvent : IEvent
    {
    }
}
