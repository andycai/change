namespace Change.Framework.Gas
{
    public interface ITargetResolver
    {
        TargetType Type { get; }
        IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities);
    }
}
