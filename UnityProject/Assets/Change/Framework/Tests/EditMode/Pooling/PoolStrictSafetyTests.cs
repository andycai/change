using System;
using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests.Pooling
{
    public class PoolStrictSafetyTests
    {
        private sealed class StrictPayload : IPoolable
        {
            public int ResetCount;

            public void Reset()
            {
                ResetCount++;
            }
        }

        [SetUp]
        public void SetUp()
        {
            Pool<StrictPayload>.SetMaxSize(PoolDefaults.DefaultMaxSize);
            Pool<StrictPayload>.Clear();
        }

        [Test]
        public void Release_Null_FollowsBuildPolicy()
        {
            var before = Pool<StrictPayload>.GetStats();
            var inactiveBefore = Pool<StrictPayload>.InactiveCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<ArgumentNullException>(() => Pool<StrictPayload>.Release(null));
#else
            Assert.DoesNotThrow(() => Pool<StrictPayload>.Release(null));
#endif

            var after = Pool<StrictPayload>.GetStats();
            Assert.AreEqual(inactiveBefore, Pool<StrictPayload>.InactiveCount);
            Assert.AreEqual(before.Released, after.Released);
            Assert.AreEqual(before.Dropped, after.Dropped);
        }

        [Test]
        public void Release_DoubleRelease_FollowsBuildPolicy()
        {
            var item = Pool<StrictPayload>.Get();
            Pool<StrictPayload>.Release(item);
            var before = Pool<StrictPayload>.GetStats();
            var inactiveBefore = Pool<StrictPayload>.InactiveCount;
            var resetCountBefore = item.ResetCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<InvalidOperationException>(() => Pool<StrictPayload>.Release(item));
#else
            Assert.DoesNotThrow(() => Pool<StrictPayload>.Release(item));
#endif

            var after = Pool<StrictPayload>.GetStats();
            Assert.AreEqual(resetCountBefore, item.ResetCount);
            Assert.AreEqual(inactiveBefore, Pool<StrictPayload>.InactiveCount);
            Assert.AreEqual(before.Released, after.Released);
            Assert.AreEqual(before.Dropped, after.Dropped);
        }

        [Test]
        public void Release_ForeignInstance_FollowsBuildPolicy()
        {
            var foreign = new StrictPayload();
            var before = Pool<StrictPayload>.GetStats();
            var inactiveBefore = Pool<StrictPayload>.InactiveCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.Throws<InvalidOperationException>(() => Pool<StrictPayload>.Release(foreign));
#else
            Assert.DoesNotThrow(() => Pool<StrictPayload>.Release(foreign));
#endif

            var after = Pool<StrictPayload>.GetStats();
            Assert.AreEqual(0, foreign.ResetCount);
            Assert.AreEqual(inactiveBefore, Pool<StrictPayload>.InactiveCount);
            Assert.AreEqual(before.Released, after.Released);
            Assert.AreEqual(before.Dropped, after.Dropped);
        }
    }
}
