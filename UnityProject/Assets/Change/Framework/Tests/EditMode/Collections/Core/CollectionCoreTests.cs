using System;
using Change.Framework.Collections;
using NUnit.Framework;

namespace Change.Framework.Tests.Collections
{
    public class CollectionCoreTests
    {
        [Test]
        public void GrowPolicy_DoublesAndRespectsMinimum()
        {
            Assert.AreEqual(4, GrowPolicy.Next(0, 4));
            Assert.AreEqual(8, GrowPolicy.Next(4, 5));
            Assert.AreEqual(16, GrowPolicy.Next(8, 9));
        }

        [Test]
        public void ThrowIfNegativeCapacity_ThrowsForNegativeValue()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CollectionGuards.ThrowIfNegativeCapacity(-1));
        }

        [Test]
        public void ThrowIfNegativeCapacity_DoesNotThrowForZeroOrPositive()
        {
            CollectionGuards.ThrowIfNegativeCapacity(0);
            CollectionGuards.ThrowIfNegativeCapacity(8);
        }

        [Test]
        public void CollectionMetrics_ResetClearsCounters()
        {
            CollectionMetrics.RecordFastListGrow();
            CollectionMetrics.RecordFastDictionaryGrow();
            CollectionMetrics.RecordFastPriorityQueueGrow();
            CollectionMetrics.Reset();

            Assert.AreEqual(0, CollectionMetrics.FastListGrowCount);
            Assert.AreEqual(0, CollectionMetrics.FastDictionaryGrowCount);
            Assert.AreEqual(0, CollectionMetrics.FastPriorityQueueGrowCount);
        }
    }
}
