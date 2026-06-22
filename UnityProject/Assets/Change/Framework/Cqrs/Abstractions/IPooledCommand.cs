using Change.Framework.Pooling;
using Cysharp.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// 池化自处理同步 command。实现为 class，配合 <c>CqrsBus.Send&lt;T&gt;(Action&lt;T&gt;)</c> 分发。
    /// 实例由 <see cref="Pool{T}"/> 借出，Execute 后 bus 调 <see cref="Pool{T}.Release"/> 归还
    /// （<see cref="Pool{T}.Release"/> 内部会调用 <see cref="IPoolable.Reset"/>）。
    /// per-dispatch 数据通过 configure 回调设置。
    /// </summary>
    public interface IPooledCommand : IPoolable
    {
        void Execute();
    }

    /// <summary>
    /// 池化自处理异步 command。实现为 class，配合 <c>CqrsBus.SendAsync&lt;T&gt;(Action&lt;T&gt;)</c> 分发。
    /// bus 在 await ExecuteAsync 完成后才 Release 归还，避免异步竞态。
    /// </summary>
    public interface IPooledAsyncCommand : IPoolable
    {
        UniTask ExecuteAsync();
    }
}
