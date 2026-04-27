using Change.Framework.Cqrs;
using Change.Framework.Gas;

namespace Change.Runtime.Gas.Cqrs
{
    public readonly struct HasTagQry : IQuery<bool>
    {
        public HasTagQry(string targetId, GameplayTag tag)
        {
            TargetId = targetId; Tag = tag;
        }
        public string TargetId { get; }
        public GameplayTag Tag { get; }
    }
}
