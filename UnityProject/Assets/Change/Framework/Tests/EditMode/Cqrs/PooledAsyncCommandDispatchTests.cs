using System;
using System.Collections;
using Change.Framework.Cqrs;
using Change.Framework.Pooling;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Change.Framework.Tests
{
    public class PooledAsyncCommandDispatchTests
    {
        private sealed class IncrementAsyncCommand : IPooledAsyncCommand
        {
            public CounterState State;
            public int Amount;
            public int ResetCount;
            public bool CompleteSynchronously;

            public async UniTask ExecuteAsync()
            {
                if (!CompleteSynchronously)
                {
                    await UniTask.Yield();
                }
                State.Value += Amount;
            }

            public void Reset()
            {
                Amount = 0;
                ResetCount++;
            }
        }

        private sealed class CounterState
        {
            public int Value;
        }

        private sealed class ThrowAsyncCommand : IPooledAsyncCommand
        {
            public async UniTask ExecuteAsync()
            {
                await UniTask.Yield();
                throw new InvalidOperationException("boom");
            }
            public void Reset() { }
        }

        // ExecuteAsync 由外部 TCS 控制完成时机，用于断言“await 完成前实例未归还”
        private sealed class ControlledAsyncCommand : IPooledAsyncCommand
        {
            public readonly UniTaskCompletionSource Tcs = new UniTaskCompletionSource();
            public async UniTask ExecuteAsync() => await Tcs.Task;
            public void Reset() { }
        }

        [SetUp]
        public void SetUp()
        {
            Pool<IncrementAsyncCommand>.Clear();
            Pool<ThrowAsyncCommand>.Clear();
            Pool<ControlledAsyncCommand>.Clear();
        }

        // 注：Unity 的 NUnit `[Test]` 不接受非 void 返回值（报 "Method has non-void
        // return value, but no result is expected"），因此使用 `[UnityTest] IEnumerator`
        // 配合 `UniTask.ToCoroutine`，这是 UniTask + Unity EditMode 测试的官方推荐写法。
        // 测试断言体与原始设计完全一致，仅外层包装不同。

        [UnityTest]
        public IEnumerator SendAsync_ExecutesPooledCommand()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var state = new CounterState();
                var bus = new CqrsBus();
                await bus.SendAsync<IncrementAsyncCommand>(c => { c.State = state; c.Amount = 5; });
                Assert.AreEqual(5, state.Value);
            });
        }

        [UnityTest]
        public IEnumerator SendAsync_ConfigureSetsFields()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var state = new CounterState();
                var bus = new CqrsBus();
                await bus.SendAsync<IncrementAsyncCommand>(c => { c.State = state; c.Amount = 9; });
                Assert.AreEqual(9, state.Value);
            });
        }

        [UnityTest]
        public IEnumerator SendAsync_ReleasesOnlyAfterAwait()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var bus = new CqrsBus();
                ControlledAsyncCommand held = null;

                var sendTask = bus.SendAsync<ControlledAsyncCommand>(c => held = c);

                // ExecuteAsync 未完成：实例仍在 bus 手中，未归还
                Assert.AreEqual(0, Pool<ControlledAsyncCommand>.InactiveCount);
                Assert.NotNull(held);

                held.Tcs.TrySetResult();
                await sendTask;

                // await 完成后：实例已归还
                Assert.AreEqual(1, Pool<ControlledAsyncCommand>.InactiveCount);
            });
        }

        [UnityTest]
        public IEnumerator SendAsync_ResetsAfterAwait()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var state = new CounterState();
                var bus = new CqrsBus();
                int resetBefore = 0;

                await bus.SendAsync<IncrementAsyncCommand>(c =>
                {
                    c.State = state;
                    c.Amount = 2;
                    c.CompleteSynchronously = true;
                    resetBefore = c.ResetCount;
                });

                var returned = Pool<IncrementAsyncCommand>.Get();
                try
                {
                    Assert.Greater(returned.ResetCount, resetBefore);
                }
                finally
                {
                    Pool<IncrementAsyncCommand>.Release(returned);
                }
            });
        }

        [UnityTest]
        public IEnumerator SendAsync_ReleaseOnException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var bus = new CqrsBus();
                Assert.AreEqual(0, Pool<ThrowAsyncCommand>.InactiveCount);

                try
                {
                    await bus.SendAsync<ThrowAsyncCommand>(_ => { });
                    Assert.Fail("Expected InvalidOperationException");
                }
                catch (InvalidOperationException) { }

                Assert.AreEqual(1, Pool<ThrowAsyncCommand>.InactiveCount);
            });
        }
    }
}
