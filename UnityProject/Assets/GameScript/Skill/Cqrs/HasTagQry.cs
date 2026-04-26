using Change.Framework.Cqrs;
using Change.Framework.Skill;

namespace GameScript.Skill.Cqrs
{
    public readonly struct HasTagQry : IQuery<bool>
    {
        public HasTagQry(string targetId, SkillTag tag)
        {
            TargetId = targetId; Tag = tag;
        }
        public string TargetId { get; }
        public SkillTag Tag { get; }
    }
}
