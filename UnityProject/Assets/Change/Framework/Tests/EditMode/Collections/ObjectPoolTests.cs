using System;
using Change.Framework.Collections;
using NUnit.Framework;

namespace Change.Framework.Tests.Collections
{
    public class ObjectPoolTests
    {
        private sealed class Payload : IResettable
        {
            public int Value;

            public void ResetState()
            {
                Value = 0;
            }
        }

        [Test]
        public void RentReturn_ReusesInstanceAndResetsState()
        {
            var pool = new ObjectPool<Payload>(() => new Payload(), 4);
            var payload = pool.Rent();
            payload.Value = 9;

            pool.Return(payload);

            var reused = pool.Rent();
            Assert.AreSame(payload, reused);
            Assert.AreEqual(0, reused.Value);
        }

        [Test]
        public void Prewarm_CreatesRequestedCount()
        {
            var created = 0;
            var pool = new ObjectPool<Payload>(() =>
            {
                created++;
                return new Payload();
            }, 8);

            pool.Prewarm(4);

            Assert.AreEqual(4, created);
            Assert.AreEqual(4, pool.InactiveCount);
        }

        [Test]
        public void Return_FalseWhenPoolFull()
        {
            var pool = new ObjectPool<Payload>(() => new Payload(), 1);
            pool.Return(new Payload());

            Assert.IsFalse(pool.Return(new Payload()));
        }

        [Test]
        public void Clear_RemovesAllPooledInstances()
        {
            var pool = new ObjectPool<Payload>(() => new Payload(), 4);
            pool.Return(new Payload());
            pool.Return(new Payload());

            pool.Clear();

            Assert.AreEqual(0, pool.InactiveCount);
        }
    }
}
