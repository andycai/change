using Change.Framework.Cqrs;

namespace Change.Runtime.Gas.Cqrs
{
    public readonly struct HealAppliedEvt : IEvent
    {
        public HealAppliedEvt(string targetId, float amount)
        {
            TargetId = targetId; Amount = amount;
        }
        public string TargetId { get; }
        public float Amount { get; }
    }
}
