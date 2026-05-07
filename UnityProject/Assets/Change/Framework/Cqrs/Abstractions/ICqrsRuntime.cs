namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Immutable CQRS dispatch surface returned by <see cref="ICqrsBootstrap.Build"/>.
    /// Implementations must not expose handler registration APIs.
    /// </summary>
    public interface ICqrsRuntime
    {
        void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand;

        TResult Ask<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>;

        void Publish<TEvent>(in TEvent @event)
            where TEvent : struct, IEvent;
    }
}
