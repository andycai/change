using Fun.Framework.Collections;
using NUnit.Framework;

namespace Fun.Framework.Tests.Collections.Allocation
{
    public class FastCollectionsAllocationTests
    {
        [Test]
        public void FastList_AddNoResize_SteadyStateZeroAlloc()
        {
            var list = new FastList<int>(4096);
            for (var i = 0; i < 2048; i++)
            {
                list.AddNoResize(i);
            }

            list.Clear(ClearMode.Logical);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2048; i++)
            {
                list.AddNoResize(i);
            }

            list.Clear(ClearMode.Logical);
            var after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0L, after - before);
        }

        [Test]
        public void RingBuffer_EnqueueDequeue_SteadyStateZeroAlloc()
        {
            var rb = new RingBuffer<int>(2048);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2048; i++)
            {
                rb.EnqueueNoResize(i);
            }

            for (var i = 0; i < 2048; i++)
            {
                rb.TryDequeue(out _);
            }

            var after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0L, after - before);
        }

        [Test]
        public void FastDictionary_TryGetValue_SteadyStateZeroAlloc()
        {
            var map = new FastDictionary<int, int>(2048);
            for (var i = 0; i < 1024; i++)
            {
                map.TryAddNoResize(i, i);
            }

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1024; i++)
            {
                map.TryGetValue(i, out _);
            }

            var after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0L, after - before);
        }

        [Test]
        public void ObjectPool_RentReturn_SteadyStateZeroAlloc()
        {
            var pool = new ObjectPool<PooledNode>(() => new PooledNode(), 256);
            pool.Prewarm(256);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 256; i++)
            {
                var value = pool.Rent();
                pool.Return(value);
            }

            var after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0L, after - before);
        }

        private sealed class PooledNode : IResettable
        {
            public int Value;

            public void ResetState()
            {
                Value = 0;
            }
        }
    }
}
