using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests.Pooling
{
    public class InstancePoolIsolationTests
    {
        private sealed class IsolationPayload : IPoolable
        {
            public IsolationPayload(string ownerTag)
            {
                OwnerTag = ownerTag;
            }

            public string OwnerTag { get; }

            public int Value { get; set; }

            public void Reset()
            {
                Value = 0;
            }
        }

        [Test]
        public void SameType_MultiplePools_KeepStorageAndStatsIsolated()
        {
            var poolA = new InstancePool<IsolationPayload>(() => new IsolationPayload("A"), 1);
            var poolB = new InstancePool<IsolationPayload>(() => new IsolationPayload("B"), 2);

            var a1 = poolA.Get();
            var a2 = poolA.Get();
            var b1 = poolB.Get();
            var b2 = poolB.Get();

            poolA.Release(a1);
            poolA.Release(a2);
            poolB.Release(b1);
            poolB.Release(b2);

            Assert.AreEqual(1, poolA.InactiveCount);
            Assert.AreEqual(2, poolB.InactiveCount);

            var statsA = poolA.GetStats();
            var statsB = poolB.GetStats();
            Assert.AreEqual(1, statsA.Dropped);
            Assert.AreEqual(0, statsB.Dropped);
        }

        [Test]
        public void SameType_MultiplePools_DoNotCrossReuseInstances()
        {
            var poolA = new InstancePool<IsolationPayload>(() => new IsolationPayload("A"), 1);
            var poolB = new InstancePool<IsolationPayload>(() => new IsolationPayload("B"), 1);

            var a = poolA.Get();
            var b = poolB.Get();
            poolA.Release(a);
            poolB.Release(b);

            var aReused = poolA.Get();
            var bReused = poolB.Get();

            Assert.AreSame(a, aReused);
            Assert.AreSame(b, bReused);
            Assert.AreEqual("A", aReused.OwnerTag);
            Assert.AreEqual("B", bReused.OwnerTag);
        }
    }
}
