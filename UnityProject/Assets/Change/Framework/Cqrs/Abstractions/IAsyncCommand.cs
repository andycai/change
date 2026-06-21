using Cysharp.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Async command that executes itself and returns a <see cref="UniTask"/>.
    /// Implement as <c>readonly struct</c>. Dispatched via <c>CqrsBus.SendAsync&lt;T&gt;</c>.
    /// Does <b>not</b> inherit <see cref="ICommand"/>; a struct may implement both
    /// <see cref="IAsyncCommand"/> and <see cref="ICommand"/> to support both
    /// sync and async dispatch.
    /// </summary>
    public interface IAsyncCommand
    {
        UniTask ExecuteAsync();
    }
}
