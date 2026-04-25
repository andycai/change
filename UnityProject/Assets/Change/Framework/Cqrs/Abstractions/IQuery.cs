namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Marker interface for query messages. Queries represent read-side requests that return <typeparamref name="TResult"/>.
    /// Implement as <c>readonly struct</c> for zero-allocation dispatch.
    /// </summary>
    public interface IQuery<TResult>
    {
    }
}
