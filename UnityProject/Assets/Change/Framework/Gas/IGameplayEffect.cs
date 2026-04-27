namespace Change.Framework.Gas
{
    public interface IGameplayEffect
    {
        void Execute(IAbilitySystem source, IAbilitySystem target);
    }
}
