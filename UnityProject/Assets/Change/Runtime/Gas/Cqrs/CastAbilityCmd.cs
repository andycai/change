using Change.Framework.Cqrs;

namespace Change.Runtime.Gas.Cqrs
{
    public readonly struct CastAbilityCmd : ICommand
    {
        public CastAbilityCmd(string skillId, string sourceId, string[] targetIds)
        {
            AbilityId = skillId; SourceId = sourceId; TargetIds = targetIds;
        }
        public string AbilityId { get; }
        public string SourceId { get; }
        public string[] TargetIds { get; }
    }
}
