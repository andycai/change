using System;

namespace Change.Framework.Gas
{
    public interface IAbilitySystem
    {
        string EntityId { get; }
        int TeamId { get; }
        IAttributeSet Attributes { get; }
        IGameplayTagSet Tags { get; }
        void AddAbility(IGameplayAbility ability);
        IGameplayAbility GetAbility(string abilityId);
        void AddModifier(IModifier modifier);
        void RemoveModifier(string modifierId);
        void RemoveModifierByTag(GameplayTag tag);
        void AddTrigger(ITrigger trigger);
        void Tick(float deltaTime);
    }
}
