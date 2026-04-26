using Change.Framework.Cqrs;

namespace GameScript.Skill.Cqrs
{
    public readonly struct ApplyModifierCmd : ICommand
    {
        public ApplyModifierCmd(string modifierId, string sourceId, string targetId)
        {
            ModifierId = modifierId; SourceId = sourceId; TargetId = targetId;
        }
        public string ModifierId { get; }
        public string SourceId { get; }
        public string TargetId { get; }
    }
}
