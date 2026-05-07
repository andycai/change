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

        /// <summary>
        /// Executes a query and returns its result.
        /// Ask is the runtime-facing alias for the legacy bus-level Query semantics.
        /// </summary>
        TResult Ask<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>;

        void Publish<TDomainEvent>(in TDomainEvent domainEvent)
            where TDomainEvent : struct, IDomainEvent;
    }
}
