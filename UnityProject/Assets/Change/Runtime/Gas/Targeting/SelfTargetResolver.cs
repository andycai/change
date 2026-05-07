using Change.Framework.Gas;

namespace Change.Runtime.Gas.Targeting
{
    public sealed class SelfTargetResolver : ITargetResolver
    {
        public TargetType Type => TargetType.Self;
        public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
        {
            return new[] { source };
        }
    }
}
