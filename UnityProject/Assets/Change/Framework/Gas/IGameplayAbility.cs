using System;

namespace Change.Framework.Gas
{
    public interface IGameplayAbility
    {
        string Id { get; }
        AbilityType Type { get; }
        ActivationType Activation { get; }
        AbilityState State { get; }
        GameplayTag[] Tags { get; }
        float CooldownDuration { get; }
        int MaxCharges { get; }
        int CurrentCharges { get; }
        bool CanActivate(IAbilitySystem source);
        void Activate(IAbilitySystem source, IAbilitySystem[] targets);
        void Tick(float deltaTime);
        event Action<AbilityState> OnStateChange;
    }
}
