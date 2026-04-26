using System;
using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests.Pooling
{
    public class InstancePoolFactoryTests
    {
        private sealed class FactoryPayload : IPoolable
        {
            public int Value;

            public void Reset()
            {
                Value = 0;
            }
        }

        [Test]
        public void Ctor_WhenFactoryIsNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new InstancePool<FactoryPayload>(null));
        }

        [Test]
        public void Ctor_WhenMaxSizeIsInvalid_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InstancePool<FactoryPayload>(() => new FactoryPayload(), 0));
        }

        [Test]
        public void GetRelease_WhenReusing_DoesNotCallFactoryAgain()
        {
            var created = 0;
            var pool = new InstancePool<FactoryPayload>(() =>
            {
                created++;
                return new FactoryPayload();
            }, 2);

            var first = pool.Get();
            pool.Release(first);
            var second = pool.Get();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, created);
        }

        [Test]
        public void Prewarm_CreatesUpToMaxSizeThroughFactory()
        {
            var created = 0;
            var pool = new InstancePool<FactoryPayload>(() =>
            {
                created++;
                return new FactoryPayload();
            }, 2);

            pool.Prewarm(10);

            Assert.AreEqual(2, pool.InactiveCount);
            Assert.AreEqual(2, created);
        }
    }
}
