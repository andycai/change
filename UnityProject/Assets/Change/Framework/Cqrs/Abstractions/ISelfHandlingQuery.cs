namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Marks a struct query as self-handling: it computes its own result via
    /// <see cref="Execute"/> with no external handler registration.
    /// </summary>
    /// <remarks>
    /// Implement as <c>readonly struct</c>. Dispatch is zero-allocation via a per-type
    /// cached constrained call. A query type cannot be both self-handling and registered
    /// with <see cref="IQueryHandler{TQuery, TResult}"/>.
    /// </remarks>
    public interface ISelfHandlingQuery<TResult> : IQuery<TResult>
    {
        TResult Execute();
    }
}
