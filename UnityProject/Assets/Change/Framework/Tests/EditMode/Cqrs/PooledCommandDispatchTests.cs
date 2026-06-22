using System;
using Change.Framework.Cqrs;
using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class PooledCommandDispatchTests
    {
        private sealed class IncrementCommand : IPooledCommand
        {
            public CounterState State;
            public int Amount;
            public int ResetCount;

            public void Execute()
            {
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

        private sealed class ThrowCommand : IPooledCommand
        {
            public void Execute() => throw new InvalidOperationException("boom");
            public void Reset() { }
        }

        [SetUp]
        public void SetUp()
        {
            Pool<IncrementCommand>.Clear();
            Pool<ThrowCommand>.Clear();
        }

        [Test]
        public void Send_ExecutesPooledCommand()
        {
            var state = new CounterState();
            var bus = new CqrsBus();

            bus.Send<IncrementCommand>(c => { c.State = state; c.Amount = 3; });

            Assert.AreEqual(3, state.Value);
        }

        [Test]
        public void Send_ConfigureSetsFields()
        {
            var state = new CounterState();
            var bus = new CqrsBus();

            bus.Send<IncrementCommand>(c => { c.State = state; c.Amount = 7; });

            Assert.AreEqual(7, state.Value);
        }

        [Test]
        public void Send_ReleasesAndResetsAfterExecute()
        {
            var state = new CounterState();
            var bus = new CqrsBus();
            int resetBefore = 0;

            bus.Send<IncrementCommand>(c =>
            {
                c.State = state;
                c.Amount = 2;
                resetBefore = c.ResetCount;
            });

            // 实例已归还：池中有 1 个空闲
            Assert.AreEqual(1, Pool<IncrementCommand>.InactiveCount);

            // 归还的实例 Reset 已被调用（Release 内部触发）
            var returned = Pool<IncrementCommand>.Get();
            try
            {
                Assert.Greater(returned.ResetCount, resetBefore);
            }
            finally
            {
                Pool<IncrementCommand>.Release(returned);
            }
        }

        [Test]
        public void Send_ReleaseOnException()
        {
            var bus = new CqrsBus();
            Assert.AreEqual(0, Pool<ThrowCommand>.InactiveCount);

            Assert.Throws<InvalidOperationException>(() => bus.Send<ThrowCommand>(_ => { }));

            Assert.AreEqual(1, Pool<ThrowCommand>.InactiveCount);
        }
    }
}
