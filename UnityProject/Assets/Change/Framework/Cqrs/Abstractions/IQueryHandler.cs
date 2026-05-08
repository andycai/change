namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Handles query messages. Implement as a class to avoid interface boxing on registration.
    /// Registration enforces this at runtime: value-type implementations are rejected with
    /// <see cref="System.InvalidOperationException"/>.
    /// </summary>
    public interface IQueryHandler<TQuery, TResult>
        where TQuery : struct, IQuery<TResult>
    {
        TResult Handle(in TQuery query);
    }
}
