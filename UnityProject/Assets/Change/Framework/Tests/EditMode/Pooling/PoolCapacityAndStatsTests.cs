using System;
using System.Reflection;
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

        private sealed class ThrowingPayload : IPoolable
        {
            public static bool ThrowOnCreate;

            public ThrowingPayload()
            {
                if (ThrowOnCreate)
                {
                    throw new InvalidOperationException("ctor failure");
                }
            }

            public void Reset()
            {
            }
        }

        [SetUp]
        public void SetUp()
        {
            Pool<CapacityPayload>.SetMaxSize(PoolDefaults.DefaultMaxSize);
            Pool<CapacityPayload>.Clear();

            ThrowingPayload.ThrowOnCreate = false;
            Pool<ThrowingPayload>.SetMaxSize(PoolDefaults.DefaultMaxSize);
            Pool<ThrowingPayload>.Clear();
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

        [Test]
        public void Get_WhenCtorThrows_DoesNotIncrementCreatedOrRented()
        {
            var before = Pool<ThrowingPayload>.GetStats();
            ThrowingPayload.ThrowOnCreate = true;

            var ex = Assert.Catch<Exception>(() => Pool<ThrowingPayload>.Get());
            Assert.IsTrue(ex is InvalidOperationException || ex is TargetInvocationException);
            if (ex is TargetInvocationException tie)
            {
                Assert.IsInstanceOf<InvalidOperationException>(tie.InnerException);
            }

            var after = Pool<ThrowingPayload>.GetStats();
            Assert.AreEqual(before.Created, after.Created);
            Assert.AreEqual(before.Rented, after.Rented);
        }

        [Test]
        public void Prewarm_WhenCtorThrows_DoesNotIncrementCreated()
        {
            var before = Pool<ThrowingPayload>.GetStats();
            ThrowingPayload.ThrowOnCreate = true;

            var ex = Assert.Catch<Exception>(() => Pool<ThrowingPayload>.Prewarm(1));
            Assert.IsTrue(ex is InvalidOperationException || ex is TargetInvocationException);
            if (ex is TargetInvocationException tie)
            {
                Assert.IsInstanceOf<InvalidOperationException>(tie.InnerException);
            }

            var after = Pool<ThrowingPayload>.GetStats();
            Assert.AreEqual(before.Created, after.Created);
        }
    }
}
