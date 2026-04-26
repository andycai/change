namespace Change.Framework.Skill
{
    public interface ISkillTagSet
    {
        bool HasTag(SkillTag tag);
        void AddTag(SkillTag tag);
        void RemoveTag(SkillTag tag);
        void Clear();
        int Count { get; }
    }
}
