using System;
using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests.Pooling
{
    public class InstancePoolStrictSafetyTests
    {
        private sealed class StrictPayload : IPoolable
        {
            public int ResetCount;

            public void Reset()
            {
                ResetCount++;
            }
        }

        private InstancePool<StrictPayload> _pool;

        [SetUp]
        public void SetUp()
        {
            _pool = new InstancePool<StrictPayload>(() => new StrictPayload());
        }

        [Test]
        public void Release_Null_FollowsBuildPolicy()
        {
            var before = _pool.GetStats();
            var inactiveBefore = _pool.InactiveCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<ArgumentNullException>(() => _pool.Release(null));
#else
            Assert.DoesNotThrow(() => _pool.Release(null));
#endif

            var after = _pool.GetStats();
            Assert.AreEqual(inactiveBefore, _pool.InactiveCount);
            Assert.AreEqual(before.Released, after.Released);
            Assert.AreEqual(before.Dropped, after.Dropped);
        }

        [Test]
        public void Release_DoubleRelease_FollowsBuildPolicy()
        {
            var item = _pool.Get();
            _pool.Release(item);
            var before = _pool.GetStats();
            var inactiveBefore = _pool.InactiveCount;
            var resetCountBefore = item.ResetCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<InvalidOperationException>(() => _pool.Release(item));
#else
            Assert.DoesNotThrow(() => _pool.Release(item));
#endif

            var after = _pool.GetStats();
            Assert.AreEqual(resetCountBefore, item.ResetCount);
            Assert.AreEqual(inactiveBefore, _pool.InactiveCount);
            Assert.AreEqual(before.Released, after.Released);
            Assert.AreEqual(before.Dropped, after.Dropped);
        }

        [Test]
        public void Release_ForeignInstance_FollowsBuildPolicy()
        {
            var foreign = new StrictPayload();
            var before = _pool.GetStats();
            var inactiveBefore = _pool.InactiveCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<InvalidOperationException>(() => _pool.Release(foreign));
#else
            Assert.DoesNotThrow(() => _pool.Release(foreign));
#endif

            var after = _pool.GetStats();
            Assert.AreEqual(0, foreign.ResetCount);
            Assert.AreEqual(inactiveBefore, _pool.InactiveCount);
            Assert.AreEqual(before.Released, after.Released);
            Assert.AreEqual(before.Dropped, after.Dropped);
        }

        [Test]
        public void Clear_InvalidatesOutstandingRental_FollowsBuildPolicy()
        {
            var item = _pool.Get();
            var before = _pool.GetStats();

            _pool.Clear();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<InvalidOperationException>(() => _pool.Release(item));
#else
            Assert.DoesNotThrow(() => _pool.Release(item));
#endif

            var after = _pool.GetStats();
            Assert.AreEqual(0, _pool.InactiveCount);
            Assert.AreEqual(before.Released, after.Released);
            Assert.AreEqual(before.Dropped, after.Dropped);
        }
    }
}
