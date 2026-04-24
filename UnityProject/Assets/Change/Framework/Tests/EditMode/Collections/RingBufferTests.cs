using Change.Framework.Collections;
using NUnit.Framework;

namespace Change.Framework.Tests.Collections
{
    public class RingBufferTests
    {
        [Test]
        public void EnqueueNoResize_ReturnsFalseWhenFull()
        {
            var q = new RingBuffer<int>(2);

            Assert.IsTrue(q.EnqueueNoResize(1));
            Assert.IsTrue(q.EnqueueNoResize(2));
            Assert.IsFalse(q.EnqueueNoResize(3));
            Assert.AreEqual(2, q.Count);
            Assert.AreEqual(2, q.Capacity);
        }

        [Test]
        public void TryDequeue_FollowsFifoAcrossWrapAround()
        {
            var q = new RingBuffer<int>(3);
            q.Enqueue(1);
            q.Enqueue(2);

            Assert.IsTrue(q.TryDequeue(out var first));

            q.Enqueue(3);
            q.Enqueue(4);

            Assert.AreEqual(1, first);
            Assert.IsTrue(q.TryDequeue(out var second));
            Assert.IsTrue(q.TryDequeue(out var third));
            Assert.IsTrue(q.TryDequeue(out var fourth));
            Assert.AreEqual(2, second);
            Assert.AreEqual(3, third);
            Assert.AreEqual(4, fourth);
        }

        [Test]
        public void TryPeek_ReturnsFrontWithoutRemoving()
        {
            var q = new RingBuffer<int>(2);
            q.Enqueue(42);

            Assert.IsTrue(q.TryPeek(out var value));
            Assert.AreEqual(42, value);
            Assert.AreEqual(1, q.Count);
        }

        [Test]
        public void Clear_ResetsCountAndAllowsReuse()
        {
            var q = new RingBuffer<int>(2);
            q.Enqueue(1);
            q.Enqueue(2);

            q.Clear();

            Assert.AreEqual(0, q.Count);
            Assert.IsFalse(q.TryDequeue(out _));

            q.Enqueue(3);
            Assert.IsTrue(q.TryDequeue(out var value));
            Assert.AreEqual(3, value);
        }
    }
}
