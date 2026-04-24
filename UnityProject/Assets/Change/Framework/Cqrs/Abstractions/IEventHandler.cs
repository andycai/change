namespace Change.Framework.Cqrs
{
    public interface IEventHandler<TEvent>
        where TEvent : struct, IEvent
    {
        void Handle(in TEvent @event);
    }
}
