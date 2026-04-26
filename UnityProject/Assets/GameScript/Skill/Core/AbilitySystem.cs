using System;
using System.Collections.Generic;
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class AbilitySystem : IAbilitySystem
    {
        public string EntityId { get; }
        public IAttributeSet Attributes { get; }
        public ISkillTagSet Tags { get; }

        public AbilitySystem(string entityId)
        {
            EntityId = entityId;
            Attributes = new AttributeSet();
            Tags = new SkillTagSet(16);
        }

        public void AddSkill(ISkill skill) { }
        public ISkill GetSkill(string skillId) => null;
        public void AddModifier(IModifier modifier) { }
        public void RemoveModifier(string modifierId) { }
        public void AddTrigger(ITrigger trigger) { }
        public void TickModifiers(float deltaTime) { }
        public void TickSkills(float deltaTime) { }
    }
}
