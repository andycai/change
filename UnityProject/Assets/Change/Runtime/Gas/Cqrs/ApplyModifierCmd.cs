using Change.Framework.Cqrs;

namespace Change.Runtime.Gas.Cqrs
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

        public void Execute()
        {
            throw new System.NotSupportedException(
                "ApplyModifierCmd is dispatched through the GAS system, not the CQRS bus.");
        }
    }
}
