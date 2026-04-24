using System;
using Fun.Framework.Collections;
using NUnit.Framework;

namespace Fun.Framework.Tests.Collections
{
    public class FastListTests
    {
        [Test]
        public void AddNoResize_ThrowsWhenCapacityIsFull()
        {
            var list = new FastList<int>(1);
            list.AddNoResize(10);

            Assert.Throws<InvalidOperationException>(() => list.AddNoResize(11));
        }

        [Test]
        public void RemoveAtSwapBack_KeepsDenseStorage()
        {
            var list = new FastList<int>(4);
            list.Add(1);
            list.Add(2);
            list.Add(3);

            list.RemoveAtSwapBack(0);

            Assert.AreEqual(2, list.Count);
            Assert.IsTrue(list.Contains(2));
            Assert.IsTrue(list.Contains(3));
        }

        [Test]
        public void Enumerator_ThrowsWhenCollectionModified()
        {
            var list = new FastList<int>(4);
            list.Add(1);
            list.Add(2);

            var e = list.GetEnumerator();
            Assert.IsTrue(e.MoveNext());
            list.Add(3);

            Assert.Throws<InvalidOperationException>(() => e.MoveNext());
        }
    }
}
