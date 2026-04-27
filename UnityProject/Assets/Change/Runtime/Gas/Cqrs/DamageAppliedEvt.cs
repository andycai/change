using Change.Framework.Cqrs;

namespace Change.Runtime.Gas.Cqrs
{
    public readonly struct DamageAppliedEvt : IEvent
    {
        public DamageAppliedEvt(string targetId, float amount, string sourceAbilityId)
        {
            TargetId = targetId; Amount = amount; SourceAbilityId = sourceAbilityId;
        }
        public string TargetId { get; }
        public float Amount { get; }
        public string SourceAbilityId { get; }
    }
}
