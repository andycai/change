using Change.Framework.Collections;
using Change.Framework.Skill;

namespace Change.Runtime.Skill
{
    public sealed class SkillTagSet : ISkillTagSet
    {
        private readonly FastHashSet<string> _tags;

        public SkillTagSet(int capacity)
        {
            _tags = new FastHashSet<string>(capacity: capacity);
        }

        public int Count => _tags.Count;

        public bool HasTag(SkillTag tag)
        {
            if (_tags.Contains(tag.Value))
                return true;

            string prefix = tag.Value + ".";
            bool found = false;
            _tags.ForEach(t =>
            {
                if (!found && t.StartsWith(prefix))
                    found = true;
            });
            return found;
        }

        public void AddTag(SkillTag tag)
        {
            _tags.Add(tag.Value);
        }

        public void RemoveTag(SkillTag tag)
        {
            _tags.Remove(tag.Value);
        }

        public void Clear()
        {
            _tags.Clear(ClearMode.Logical);
        }
    }
}
