using Change.Framework.Cqrs;

namespace Change.Runtime.Gas.Cqrs
{
    public readonly struct AttributeChangedEvt : IEvent
    {
        public AttributeChangedEvt(string targetId, string attributeName, float oldValue, float newValue)
        {
            TargetId = targetId; AttributeName = attributeName; OldValue = oldValue; NewValue = newValue;
        }
        public string TargetId { get; }
        public string AttributeName { get; }
        public float OldValue { get; }
        public float NewValue { get; }
    }
}
