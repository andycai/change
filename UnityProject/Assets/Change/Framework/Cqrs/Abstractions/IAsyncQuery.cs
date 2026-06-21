using Cysharp.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Async query that computes its own result and returns a <see cref="UniTask{TResult}"/>.
    /// Implement as <c>readonly struct</c>. Dispatched via <c>CqrsBus.AskAsync&lt;T&gt;</c>.
    /// Does <b>not</b> inherit <see cref="IQuery{TResult}"/>; a struct may implement both
    /// <see cref="IAsyncQuery{TResult}"/> and <see cref="IQuery{TResult}"/> to support both
    /// sync and async dispatch.
    /// </summary>
    public interface IAsyncQuery<TResult>
    {
        UniTask<TResult> QueryAsync();
    }
}
