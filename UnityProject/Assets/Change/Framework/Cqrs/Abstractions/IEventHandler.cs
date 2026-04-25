namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Handles event messages. Implement as a class to avoid interface boxing on registration.
    /// </summary>
    public interface IEventHandler<TEvent>
        where TEvent : struct, IEvent
    {
        void Handle(in TEvent @event);
    }
}
