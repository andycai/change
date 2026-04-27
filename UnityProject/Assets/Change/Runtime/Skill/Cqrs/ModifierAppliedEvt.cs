using Change.Framework.Cqrs;

namespace Change.Runtime.Skill.Cqrs
{
    public readonly struct ModifierAppliedEvt : IEvent
    {
        public ModifierAppliedEvt(string modifierId, string targetId)
        {
            ModifierId = modifierId; TargetId = targetId;
        }
        public string ModifierId { get; }
        public string TargetId { get; }
    }
}
