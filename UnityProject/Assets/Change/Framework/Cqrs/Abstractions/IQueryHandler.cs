namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Handles query messages. Implement as a class to avoid interface boxing on registration.
    /// </summary>
    public interface IQueryHandler<TQuery, TResult>
        where TQuery : struct, IQuery<TResult>
    {
        TResult Handle(in TQuery query);
    }
}
