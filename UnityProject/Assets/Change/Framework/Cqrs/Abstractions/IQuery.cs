namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Query message that computes its own result. Implement as <c>readonly struct</c>
    /// for zero-allocation dispatch. Provides <see cref="Query"/> as the single
    /// execution entry point, invoked by <c>CqrsBus.Ask&lt;T&gt;</c>.
    /// <para>
    /// <typeparamref name="TResult"/> is unconstrained and flows through dispatch as a generic
    /// type argument; both reference and value result types avoid boxing.
    /// </para>
    /// </summary>
    public interface IQuery<TResult>
    {
        TResult Query();
    }
}
