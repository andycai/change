using Change.Framework.Skill;

namespace Change.Runtime.Skill.Targeting
{
    public sealed class SelfTargetResolver : ITargetResolver
    {
        public TargetType Type => TargetType.Self;
        public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
        {
            return new IAbilitySystem[] { source };
        }
    }
}
