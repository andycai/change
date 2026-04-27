using Change.Framework.Skill;
using Change.Runtime.Skill;
using NUnit.Framework;

namespace Change.Runtime.Tests
{
    public class SkillTagSetTests
    {
        [Test]
        public void AddTag_ThenHasTag_ReturnsTrue()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("buff.fire"));
            Assert.IsTrue(set.HasTag(new SkillTag("buff.fire")));
        }

        [Test]
        public void HasTag_WithParentTag_MatchesChildTags()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("buff.fire.ignite"));
            Assert.IsTrue(set.HasTag(new SkillTag("buff.fire")));
        }

        [Test]
        public void HasTag_WithChildTag_DoesNotMatchParentOnly()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("buff.fire"));
            Assert.IsFalse(set.HasTag(new SkillTag("buff.fire.ignite")));
        }

        [Test]
        public void RemoveTag_ThenHasTag_ReturnsFalse()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("buff.fire"));
            set.RemoveTag(new SkillTag("buff.fire"));
            Assert.IsFalse(set.HasTag(new SkillTag("buff.fire")));
        }

        [Test]
        public void Clear_RemovesAllTags()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("a"));
            set.AddTag(new SkillTag("b"));
            set.Clear();
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void Count_ReturnsCorrectCount()
        {
            var set = new SkillTagSet(8);
            Assert.AreEqual(0, set.Count);
            set.AddTag(new SkillTag("a"));
            set.AddTag(new SkillTag("b"));
            Assert.AreEqual(2, set.Count);
        }

        [Test]
        public void AddTag_Duplicate_DoesNotIncreaseCount()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("buff.fire"));
            set.AddTag(new SkillTag("buff.fire"));
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void HasTag_ExactMatch_Works()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("buff.fire"));
            Assert.IsTrue(set.HasTag(new SkillTag("buff.fire")));
            Assert.IsFalse(set.HasTag(new SkillTag("buff.ice")));
        }
    }
}
