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

        private sealed class ResetThrowingPayload : IPoolable
        {
            public bool ThrowOnReset;

            public void Reset()
            {
                if (ThrowOnReset)
                {
                    throw new InvalidOperationException("reset failure");
                }
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

        [Test]
        public void Get_WhenFactoryReturnsNull_ThrowsAndDoesNotIncrementStats()
        {
            var pool = new InstancePool<FactoryPayload>(() => null, 2);
            var before = pool.GetStats();

            Assert.Throws<InvalidOperationException>(() => pool.Get());

            var after = pool.GetStats();
            Assert.AreEqual(before.Created, after.Created);
            Assert.AreEqual(before.Rented, after.Rented);
            Assert.AreEqual(0, pool.InactiveCount);
        }

        [Test]
        public void Prewarm_WhenFactoryReturnsNull_ThrowsAndDoesNotIncrementCreated()
        {
            var pool = new InstancePool<FactoryPayload>(() => null, 2);
            var before = pool.GetStats();

            Assert.Throws<InvalidOperationException>(() => pool.Prewarm(1));

            var after = pool.GetStats();
            Assert.AreEqual(before.Created, after.Created);
            Assert.AreEqual(0, pool.InactiveCount);
        }

        [Test]
        public void Release_WhenResetThrows_DoesNotIncrementReleased()
        {
            var pool = new InstancePool<ResetThrowingPayload>(() => new ResetThrowingPayload(), 1);
            var item = pool.Get();
            var before = pool.GetStats();
            item.ThrowOnReset = true;

            Assert.Throws<InvalidOperationException>(() => pool.Release(item));

            var after = pool.GetStats();
            Assert.AreEqual(before.Released, after.Released);
            Assert.AreEqual(before.Dropped, after.Dropped);
            Assert.AreEqual(0, pool.InactiveCount);
        }
    }
}
