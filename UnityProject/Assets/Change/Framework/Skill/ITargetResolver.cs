namespace Change.Framework.Skill
{
    public interface ITargetResolver
    {
        TargetType Type { get; }
        IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities);
    }
}
