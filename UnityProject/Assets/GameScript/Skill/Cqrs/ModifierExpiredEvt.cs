using Change.Framework.Cqrs;

namespace GameScript.Skill.Cqrs
{
    public readonly struct ModifierExpiredEvt : IEvent
    {
        public ModifierExpiredEvt(string modifierId, string targetId)
        {
            ModifierId = modifierId; TargetId = targetId;
        }
        public string ModifierId { get; }
        public string TargetId { get; }
    }
}
