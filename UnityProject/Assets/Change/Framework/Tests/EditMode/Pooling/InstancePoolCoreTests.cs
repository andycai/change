using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests.Pooling
{
    public class InstancePoolCoreTests
    {
        private sealed class CorePayload : IPoolable
        {
            public int Value;
            public int ResetCount;

            public void Reset()
            {
                Value = 0;
                ResetCount++;
            }
        }

        [Test]
        public void Get_WhenPoolIsEmpty_CreatesInstanceFromFactory()
        {
            var pool = new InstancePool<CorePayload>(() => new CorePayload());

            var item = pool.Get();

            Assert.IsNotNull(item);
            Assert.AreEqual(0, pool.InactiveCount);
        }

        [Test]
        public void Release_ResetsAndReusesSameInstance()
        {
            var pool = new InstancePool<CorePayload>(() => new CorePayload());
            var item = pool.Get();
            item.Value = 42;

            pool.Release(item);
            var reused = pool.Get();

            Assert.AreSame(item, reused);
            Assert.AreEqual(0, reused.Value);
            Assert.AreEqual(1, reused.ResetCount);
        }

        [Test]
        public void Clear_RemovesInactiveItems()
        {
            var pool = new InstancePool<CorePayload>(() => new CorePayload());
            var a = pool.Get();
            var b = pool.Get();
            pool.Release(a);
            pool.Release(b);

            pool.Clear();

            Assert.AreEqual(0, pool.InactiveCount);
        }
    }
}
