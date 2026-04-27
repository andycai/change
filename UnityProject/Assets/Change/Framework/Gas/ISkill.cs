using System;

namespace Change.Framework.Gas
{
    public interface ISkill
    {
        string Id { get; }
        SkillType Type { get; }
        ActivationType Activation { get; }
        SkillState State { get; }
        SkillTag[] Tags { get; }
        float CooldownDuration { get; }
        int MaxCharges { get; }
        int CurrentCharges { get; }
        bool CanActivate(IAbilitySystem source);
        void Activate(IAbilitySystem source, IAbilitySystem[] targets);
        void TickCooldown(float deltaTime);
        event Action<string> OnStateChange;
    }
}
