using System;

namespace Change.Framework.Gas
{
    public interface IAbilitySystem
    {
        string EntityId { get; }
        int TeamId { get; }
        IAttributeSet Attributes { get; }
        ISkillTagSet Tags { get; }
        void AddSkill(ISkill skill);
        ISkill GetSkill(string skillId);
        void AddModifier(IModifier modifier);
        void RemoveModifier(string modifierId);
        void RemoveModifierByTag(SkillTag tag);
        void AddTrigger(ITrigger trigger);
        void TickModifiers(float deltaTime);
        void TickSkills(float deltaTime);
        void TickTriggers(float deltaTime);
    }
}
