using Fun.Framework.Collections;
using NUnit.Framework;

namespace Fun.Framework.Tests.Collections
{
    public class FastDictionaryTests
    {
        [Test]
        public void TryAddAndTryGetValue_WorkForUniqueKeys()
        {
            var map = new FastDictionary<int, string>(4);

            Assert.IsTrue(map.TryAdd(1, "one"));
            Assert.IsTrue(map.TryGetValue(1, out var value));
            Assert.AreEqual("one", value);
        }

        [Test]
        public void TryAddNoResize_ReturnsFalseWhenCapacityFull()
        {
            var map = new FastDictionary<int, string>(1);

            Assert.IsTrue(map.TryAddNoResize(1, "one"));
            Assert.IsFalse(map.TryAddNoResize(2, "two"));
        }

        [Test]
        public void Remove_DeletesEntry()
        {
            var map = new FastDictionary<int, string>(4);
            map.TryAdd(7, "x");

            Assert.IsTrue(map.Remove(7));
            Assert.IsFalse(map.TryGetValue(7, out _));
        }

        [Test]
        public void ContainsKeyAndClear_WorkAsExpected()
        {
            var map = new FastDictionary<int, string>(4);
            map.TryAdd(10, "ten");

            Assert.IsTrue(map.ContainsKey(10));
            map.Clear();

            Assert.AreEqual(0, map.Count);
            Assert.IsFalse(map.ContainsKey(10));
        }

        [Test]
        public void ValueTypeKey_DefaultValueKeyWorks()
        {
            var map = new FastDictionary<int, string>(2);

            Assert.IsTrue(map.TryAdd(default, "zero"));
            Assert.IsTrue(map.TryGetValue(default, out var value));
            Assert.AreEqual("zero", value);
        }
    }
}
