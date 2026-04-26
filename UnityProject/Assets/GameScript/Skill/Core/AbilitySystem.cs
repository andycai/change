using System;
using System.Collections.Generic;
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class AbilitySystem : IAbilitySystem
    {
        private readonly Dictionary<string, ISkill> _skills = new Dictionary<string, ISkill>();
        private readonly List<IModifier> _modifiers = new List<IModifier>();
        private readonly List<ITrigger> _triggers = new List<ITrigger>();

        public string EntityId { get; }
        public IAttributeSet Attributes { get; }
        public ISkillTagSet Tags { get; }

        public AbilitySystem(string entityId)
        {
            EntityId = entityId;
            Attributes = new AttributeSet();
            Tags = new SkillTagSet(16);
        }

        public void AddSkill(ISkill skill)
        {
            _skills[skill.Id] = skill;
        }

        public ISkill GetSkill(string skillId)
        {
            return _skills.TryGetValue(skillId, out var skill) ? skill : null;
        }

        public void AddModifier(IModifier modifier)
        {
            _modifiers.Add(modifier);
            modifier.OnApply(this);
        }

        public void RemoveModifier(string modifierId)
        {
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i].Id == modifierId)
                {
                    _modifiers[i].OnRemove(this);
                    _modifiers.RemoveAt(i);
                    return;
                }
            }
        }

        public void AddTrigger(ITrigger trigger)
        {
            _triggers.Add(trigger);
        }

        public void TickModifiers(float deltaTime)
        {
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                _modifiers[i].OnTick(this, deltaTime);
                if (_modifiers[i].IsExpired)
                {
                    _modifiers[i].OnRemove(this);
                    _modifiers.RemoveAt(i);
                }
            }
        }

        public void TickSkills(float deltaTime)
        {
            foreach (var kvp in _skills)
            {
                kvp.Value.TickCooldown(deltaTime);
            }
        }

        internal IReadOnlyList<ITrigger> GetTriggers() => _triggers;
    }
}
