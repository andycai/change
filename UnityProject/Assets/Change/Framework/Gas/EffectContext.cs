namespace Change.Framework.Gas
{
    public readonly struct EffectContext
    {
        public EffectContext(IAbilitySystem source, IAbilitySystem target, IGameplayAbility ability = null)
        {
            Source = source;
            Target = target;
            Ability = ability;
        }

        public IAbilitySystem Source { get; }
        public IAbilitySystem Target { get; }
        public IGameplayAbility Ability { get; }
    }
}
