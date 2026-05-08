namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Marker interface for event messages. Events represent domain occurrences that notify multiple subscribers.
    /// Implement as <c>readonly struct</c> for zero-allocation dispatch.
    /// <para>
    /// Dispatch rule: <c>Publish&lt;TEvent&gt;</c> matches subscribers strictly by the closed
    /// generic type <c>TEvent</c>. Subscriptions against base interfaces are not invoked for
    /// derived events; each concrete event type must be subscribed explicitly.
    /// </para>
    /// </summary>
    public interface IEvent
    {
    }
}
