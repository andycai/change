using Change.Framework.Gas;
using Change.Runtime.Gas;
using NUnit.Framework;

namespace Change.Runtime.Tests.Gas
{
    public class GameplayTagSetTests
    {
        [Test]
        public void AddTag_ThenHasTag_ReturnsTrue()
        {
            var set = new GameplayTagSet(8);
            set.AddTag(new GameplayTag("buff.fire"));
            Assert.IsTrue(set.HasTag(new GameplayTag("buff.fire")));
        }

        [Test]
        public void HasTag_WithParentTag_MatchesChildTags()
        {
            var set = new GameplayTagSet(8);
            set.AddTag(new GameplayTag("buff.fire.ignite"));
            Assert.IsTrue(set.HasTag(new GameplayTag("buff.fire")));
        }

        [Test]
        public void HasTag_WithChildTag_DoesNotMatchParentOnly()
        {
            var set = new GameplayTagSet(8);
            set.AddTag(new GameplayTag("buff.fire"));
            Assert.IsFalse(set.HasTag(new GameplayTag("buff.fire.ignite")));
        }

        [Test]
        public void RemoveTag_ThenHasTag_ReturnsFalse()
        {
            var set = new GameplayTagSet(8);
            set.AddTag(new GameplayTag("buff.fire"));
            set.RemoveTag(new GameplayTag("buff.fire"));
            Assert.IsFalse(set.HasTag(new GameplayTag("buff.fire")));
        }

        [Test]
        public void Clear_RemovesAllTags()
        {
            var set = new GameplayTagSet(8);
            set.AddTag(new GameplayTag("a"));
            set.AddTag(new GameplayTag("b"));
            set.Clear();
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void Count_ReturnsCorrectCount()
        {
            var set = new GameplayTagSet(8);
            Assert.AreEqual(0, set.Count);
            set.AddTag(new GameplayTag("a"));
            set.AddTag(new GameplayTag("b"));
            // Note: GameplayTagSet now includes parent tags in count
            Assert.AreEqual(2, set.Count);
        }

        [Test]
        public void AddTag_Duplicate_DoesNotIncreaseCount()
        {
            var set = new GameplayTagSet(8);
            set.AddTag(new GameplayTag("buff.fire"));
            set.AddTag(new GameplayTag("buff.fire"));
            Assert.AreEqual(2, set.Count); // buff and buff.fire
        }

        [Test]
        public void HasTag_ExactMatch_Works()
        {
            var set = new GameplayTagSet(8);
            set.AddTag(new GameplayTag("buff.fire"));
            Assert.IsTrue(set.HasTag(new GameplayTag("buff.fire")));
            Assert.IsFalse(set.HasTag(new GameplayTag("buff.ice")));
        }
    }
}
