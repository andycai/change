using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests.Pooling
{
    public class PoolCapacityAndStatsTests
    {
        private sealed class CapacityPayload : IPoolable
        {
            public void Reset()
            {
            }
        }

        [SetUp]
        public void SetUp()
        {
            Pool<CapacityPayload>.SetMaxSize(PoolDefaults.DefaultMaxSize);
            Pool<CapacityPayload>.Clear();
        }

        [Test]
        public void Prewarm_DoesNotExceedMaxSize()
        {
            Pool<CapacityPayload>.SetMaxSize(2);

            Pool<CapacityPayload>.Prewarm(8);

            Assert.AreEqual(2, Pool<CapacityPayload>.InactiveCount);
        }

        [Test]
        public void SetMaxSize_Shrink_ImmediatelyTrimsInactive()
        {
            Pool<CapacityPayload>.SetMaxSize(4);
            Pool<CapacityPayload>.Prewarm(4);

            Pool<CapacityPayload>.SetMaxSize(1);

            Assert.AreEqual(1, Pool<CapacityPayload>.InactiveCount);
        }

        [Test]
        public void Release_WhenFull_DropsObjectAndIncrementsDropped()
        {
            Pool<CapacityPayload>.SetMaxSize(1);
            var first = Pool<CapacityPayload>.Get();
            var second = Pool<CapacityPayload>.Get();
            var before = Pool<CapacityPayload>.GetStats();

            Pool<CapacityPayload>.Release(first);
            Pool<CapacityPayload>.Release(second);

            var after = Pool<CapacityPayload>.GetStats();
            Assert.AreEqual(1, Pool<CapacityPayload>.InactiveCount);
            Assert.AreEqual(before.Dropped + 1, after.Dropped);
        }

        [Test]
        public void GetStats_TracksCreatedRentedReleasedDeltas()
        {
            var before = Pool<CapacityPayload>.GetStats();
            var item = Pool<CapacityPayload>.Get();
            Pool<CapacityPayload>.Release(item);
            var after = Pool<CapacityPayload>.GetStats();

            Assert.AreEqual(before.Created + 1, after.Created);
            Assert.AreEqual(before.Rented + 1, after.Rented);
            Assert.AreEqual(before.Released + 1, after.Released);
        }
    }
}
