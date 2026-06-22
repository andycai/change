using System;
using System.Collections;
using Change.Framework.Cqrs;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Change.Framework.Tests
{
    public class AsyncCommandDispatchTests
    {
        // readonly struct 的异步执行委托给 static async helper，
        // 避免在 struct 实例上承载 async 状态机。
        private readonly struct AsyncIncrementCommand : IAsyncCommand
        {
            private readonly CounterState _state;
            private readonly int _amount;

            public AsyncIncrementCommand(CounterState state, int amount)
            {
                _state = state;
                _amount = amount;
            }

            public UniTask ExecuteAsync() => ExecuteAsyncCore(_state, _amount);

            private static async UniTask ExecuteAsyncCore(CounterState state, int amount)
            {
                await UniTask.Yield();
                state.Value += amount;
            }
        }

        private readonly struct AsyncThrowCommand : IAsyncCommand
        {
            public UniTask ExecuteAsync() => ThrowCore();

            private static async UniTask ThrowCore()
            {
                await UniTask.Yield();
                throw new InvalidOperationException("boom");
            }
        }

        private sealed class CounterState
        {
            public int Value;
        }

        // 注：Unity 的 NUnit `[Test]` 不接受非 void 返回值（报 "Method has non-void
        // return value, but no result is expected"），因此使用 `[UnityTest] IEnumerator`
        // 配合 `UniTask.ToCoroutine`，这是 UniTask + Unity EditMode 测试的官方推荐写法。
        // 测试断言体与原始设计完全一致，仅外层包装不同。

        [UnityTest]
        public IEnumerator SendAsync_ExecutesSelfHandlingCommand()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var state = new CounterState();
                var bus = new CqrsBus();

                await bus.SendAsync(new AsyncIncrementCommand(state, 4));

                Assert.AreEqual(4, state.Value);
            });
        }

        [UnityTest]
        public IEnumerator SendAsync_AwaitsCompletion()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var state = new CounterState();
                var bus = new CqrsBus();

                // ExecuteAsync 内部 await UniTask.Yield() 推迟完成；
                // 若 bus 不 await（fire-and-forget），断言时 Value 仍为 0。
                await bus.SendAsync(new AsyncIncrementCommand(state, 6));

                Assert.AreEqual(6, state.Value);
            });
        }

        [UnityTest]
        public IEnumerator SendAsync_PropagatesException()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var bus = new CqrsBus();

                try
                {
                    await bus.SendAsync(new AsyncThrowCommand());
                    Assert.Fail("Expected InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            });
        }
    }
}
