using Change.Framework.Cqrs;

namespace Change.Runtime.Gas.Cqrs
{
    public readonly struct DamageAppliedEvt : IEvent
    {
        public DamageAppliedEvt(string targetId, float amount, string sourceSkillId)
        {
            TargetId = targetId; Amount = amount; SourceSkillId = sourceSkillId;
        }
        public string TargetId { get; }
        public float Amount { get; }
        public string SourceSkillId { get; }
    }
}
