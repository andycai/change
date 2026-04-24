using Change.Framework.Collections;
using NUnit.Framework;

namespace Change.Framework.Tests.Collections
{
    public class FastPriorityQueueTests
    {
        [Test]
        public void TryDequeue_ReturnsItemsInAscendingOrder()
        {
            var pq = new FastPriorityQueue<int>(8);
            pq.Enqueue(5);
            pq.Enqueue(1);
            pq.Enqueue(3);

            Assert.IsTrue(pq.TryDequeue(out var a));
            Assert.IsTrue(pq.TryDequeue(out var b));
            Assert.IsTrue(pq.TryDequeue(out var c));
            Assert.AreEqual(1, a);
            Assert.AreEqual(3, b);
            Assert.AreEqual(5, c);
        }

        [Test]
        public void EnqueueNoResize_ReturnsFalseWhenCapacityReached()
        {
            var pq = new FastPriorityQueue<int>(1);
            Assert.IsTrue(pq.EnqueueNoResize(10));
            Assert.IsFalse(pq.EnqueueNoResize(11));
        }
    }
}
