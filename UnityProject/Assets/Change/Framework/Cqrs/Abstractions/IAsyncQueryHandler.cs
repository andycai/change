using System.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Handles a query asynchronously. Implement as a class. Only Class mode
    /// supports async dispatch.
    /// </summary>
    public interface IAsyncQueryHandler<TQuery, TResult>
        where TQuery : struct, IQuery<TResult>
    {
        Task<TResult> ExecuteAsync(TQuery query);
    }
}
