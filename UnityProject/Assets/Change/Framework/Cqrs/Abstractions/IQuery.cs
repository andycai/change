namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Marker interface for query messages. Queries represent read-side requests that return <typeparamref name="TResult"/>.
    /// Implement as <c>readonly struct</c> for zero-allocation dispatch.
    /// <para>
    /// <typeparamref name="TResult"/> is unconstrained and flows through dispatch as a generic
    /// type argument; both reference and value result types avoid boxing.
    /// </para>
    /// </summary>
    public interface IQuery<TResult>
    {
    }
}
