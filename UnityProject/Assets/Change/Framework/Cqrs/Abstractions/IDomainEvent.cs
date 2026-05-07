namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Marker interface for domain event messages.
    /// Domain events are a semantic subset of <see cref="IEvent"/> and share the same dispatch path.
    /// </summary>
    public interface IDomainEvent : IEvent
    {
    }
}
