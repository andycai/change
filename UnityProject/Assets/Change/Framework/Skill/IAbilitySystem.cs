using System;

namespace Change.Framework.Skill
{
    public interface IAbilitySystem
    {
        string EntityId { get; }
        IAttributeSet Attributes { get; }
        ISkillTagSet Tags { get; }
        void AddSkill(ISkill skill);
        ISkill GetSkill(string skillId);
        void AddModifier(IModifier modifier);
        void RemoveModifier(string modifierId);
        void AddTrigger(ITrigger trigger);
        void TickModifiers(float deltaTime);
        void TickSkills(float deltaTime);
        void TickTriggers(float deltaTime);
    }
}
