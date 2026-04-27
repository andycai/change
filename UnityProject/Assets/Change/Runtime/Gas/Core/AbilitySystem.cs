using System;
using System.Collections.Generic;
using Change.Framework.Gas;

namespace Change.Runtime.Gas
{
    public sealed class AbilitySystem : IAbilitySystem
    {
        private readonly Dictionary<string, IGameplayAbility> _abilities = new Dictionary<string, IGameplayAbility>();
        private readonly List<IModifier> _modifiers = new List<IModifier>();
        private readonly Dictionary<TriggerEventType, List<ITrigger>> _triggerGroups = new Dictionary<TriggerEventType, List<ITrigger>>();

        public string EntityId { get; }
        public int TeamId { get; }
        public IAttributeSet Attributes { get; }
        public IGameplayTagSet Tags { get; }

        public AbilitySystem(string entityId, int teamId = 0)
        {
            EntityId = entityId;
            TeamId = teamId;
            Attributes = new AttributeSet();
            Tags = new GameplayTagSet(16);
        }

        public void AddAbility(IGameplayAbility ability)
        {
            _abilities[ability.Id] = ability;
        }

        public IGameplayAbility GetAbility(string abilityId)
        {
            return _abilities.TryGetValue(abilityId, out var ability) ? ability : null;
        }

        public void AddModifier(IModifier modifier)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].Id == modifier.Id)
                {
                    var existing = _modifiers[i];
                    switch (existing.StackingRule)
                    {
                        case ModifierStacking.Refresh:
                            existing.RefreshDuration();
                            return;
                        case ModifierStacking.AddStack:
                            existing.AddStack();
                            return;
                        case ModifierStacking.Replace:
                            existing.OnRemove(this);
                            _modifiers.RemoveAt(i);
                            break;
                        case ModifierStacking.Ignore:
                            return;
                    }
                    break;
                }
            }
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

        public void RemoveModifierByTag(GameplayTag tag)
        {
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                var granted = _modifiers[i].GrantedTags;
                if (granted != null)
                {
                    for (int t = 0; t < granted.Length; t++)
                    {
                        if (granted[t] == tag)
                        {
                            _modifiers[i].OnRemove(this);
                            _modifiers.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        public void AddTrigger(ITrigger trigger)
        {
            if (!_triggerGroups.TryGetValue(trigger.EventType, out var list))
            {
                list = new List<ITrigger>();
                _triggerGroups[trigger.EventType] = list;
            }
            list.Add(trigger);
        }

        public void Tick(float deltaTime)
        {
            // Tick Modifiers
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                _modifiers[i].OnTick(this, deltaTime);
                if (_modifiers[i].IsExpired)
                {
                    _modifiers[i].OnRemove(this);
                    _modifiers.RemoveAt(i);
                }
            }

            // Tick Abilities
            foreach (var kvp in _abilities)
            {
                kvp.Value.Tick(deltaTime);
            }

            // Tick Triggers
            foreach (var group in _triggerGroups.Values)
            {
                for (int i = 0; i < group.Count; i++)
                    group[i].TickCooldown(deltaTime);
            }
        }

        internal IReadOnlyList<ITrigger> GetTriggers(TriggerEventType eventType)
        {
            return _triggerGroups.TryGetValue(eventType, out var list) ? list : Array.Empty<ITrigger>();
        }
    }
}
