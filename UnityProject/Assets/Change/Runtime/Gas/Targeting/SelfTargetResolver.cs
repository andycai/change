using Change.Framework.Gas;

namespace Change.Runtime.Gas.Targeting
{
    public sealed class SelfTargetResolver : ITargetResolver
    {
        private static readonly IAbilitySystem[] _cached = new IAbilitySystem[1];

        public TargetType Type => TargetType.Self;
        public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
        {
            _cached[0] = source;
            return _cached;
        }
    }
}
