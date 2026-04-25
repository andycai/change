namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Marker interface for event messages. Events represent domain occurrences that notify multiple subscribers.
    /// Implement as <c>readonly struct</c> for zero-allocation dispatch.
    /// </summary>
    public interface IEvent
    {
    }
}
