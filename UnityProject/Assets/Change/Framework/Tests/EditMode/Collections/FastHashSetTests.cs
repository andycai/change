using Fun.Framework.Collections;
using NUnit.Framework;

namespace Fun.Framework.Tests.Collections
{
    public class FastHashSetTests
    {
        [Test]
        public void AddContainsRemove_WorkAsExpected()
        {
            var set = new FastHashSet<int>(4);

            Assert.IsTrue(set.Add(10));
            Assert.AreEqual(1, set.Count);
            Assert.IsFalse(set.Add(10));
            Assert.AreEqual(1, set.Count);
            Assert.IsTrue(set.Contains(10));
            Assert.IsTrue(set.Remove(10));
            Assert.AreEqual(0, set.Count);
            Assert.IsFalse(set.Contains(10));
        }

        [Test]
        public void AddNoResize_ReturnsFalseWhenFull()
        {
            var set = new FastHashSet<int>(1);

            Assert.IsTrue(set.AddNoResize(1));
            Assert.IsFalse(set.AddNoResize(2));
        }

        [Test]
        public void Clear_RemovesAllEntries()
        {
            var set = new FastHashSet<int>(4);
            set.Add(1);
            set.Add(2);

            set.Clear();

            Assert.AreEqual(0, set.Count);
            Assert.IsFalse(set.Contains(1));
            Assert.IsFalse(set.Contains(2));
        }
    }
}
