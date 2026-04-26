using Change.Framework.Cqrs;

namespace GameScript.Skill.Cqrs
{
    public readonly struct CastSkillCmd : ICommand
    {
        public CastSkillCmd(string skillId, string sourceId, string[] targetIds)
        {
            SkillId = skillId; SourceId = sourceId; TargetIds = targetIds;
        }
        public string SkillId { get; }
        public string SourceId { get; }
        public string[] TargetIds { get; }
    }
}
