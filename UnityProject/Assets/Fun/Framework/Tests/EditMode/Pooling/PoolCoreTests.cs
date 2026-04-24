using Fun.Framework.Pooling;
using NUnit.Framework;

namespace Fun.Framework.Tests.Pooling
{
    public class PoolCoreTests
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

        [SetUp]
        public void SetUp()
        {
            Pool<CorePayload>.SetMaxSize(PoolDefaults.DefaultMaxSize);
            Pool<CorePayload>.Clear();
        }

        [Test]
        public void Get_WhenPoolIsEmpty_CreatesInstance()
        {
            var item = Pool<CorePayload>.Get();

            Assert.IsNotNull(item);
            Assert.AreEqual(0, Pool<CorePayload>.InactiveCount);
        }

        [Test]
        public void Release_ResetsAndReusesSameInstance()
        {
            var item = Pool<CorePayload>.Get();
            item.Value = 42;

            Pool<CorePayload>.Release(item);
            var reused = Pool<CorePayload>.Get();

            Assert.AreSame(item, reused);
            Assert.AreEqual(0, reused.Value);
            Assert.AreEqual(1, reused.ResetCount);
        }

        [Test]
        public void Clear_RemovesInactiveItems()
        {
            var a = Pool<CorePayload>.Get();
            var b = Pool<CorePayload>.Get();
            Pool<CorePayload>.Release(a);
            Pool<CorePayload>.Release(b);

            Pool<CorePayload>.Clear();

            Assert.AreEqual(0, Pool<CorePayload>.InactiveCount);
        }
    }
}
