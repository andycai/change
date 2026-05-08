using System;
using System.Collections.Generic;

namespace Change.Framework.Gas
{
    public sealed class DefaultAbilitySystem : IAbilitySystem
    {
        private readonly Dictionary<string, IGameplayAbility> _abilities;
        private readonly Dictionary<string, IModifier> _modifiers;
        private readonly Dictionary<TriggerEventType, List<ITrigger>> _triggersByEvent;

        public DefaultAbilitySystem(string entityId, int teamId, IAttributeSet attributes, IGameplayTagSet tags)
        {
            if (string.IsNullOrEmpty(entityId))
            {
                throw new ArgumentException("Entity id cannot be null or empty.", nameof(entityId));
            }

            EntityId = entityId;
            TeamId = teamId;
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            Tags = tags ?? throw new ArgumentNullException(nameof(tags));
            _abilities = new Dictionary<string, IGameplayAbility>(StringComparer.Ordinal);
            _modifiers = new Dictionary<string, IModifier>(StringComparer.Ordinal);
            _triggersByEvent = new Dictionary<TriggerEventType, List<ITrigger>>();
        }

        public string EntityId { get; }
        public int TeamId { get; }
        public IAttributeSet Attributes { get; }
        public IGameplayTagSet Tags { get; }

        public void AddAbility(IGameplayAbility ability)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            if (_abilities.ContainsKey(ability.Id))
            {
                throw new InvalidOperationException($"Duplicate ability registration: {ability.Id}");
            }

            _abilities.Add(ability.Id, ability);
        }

        public IGameplayAbility GetAbility(string abilityId)
        {
            if (!_abilities.TryGetValue(abilityId, out var ability))
            {
                throw new InvalidOperationException($"Ability is not registered: {abilityId}");
            }

            return ability;
        }

        public void AddModifier(IModifier modifier)
        {
            if (modifier == null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            if (_modifiers.ContainsKey(modifier.Id))
            {
                throw new InvalidOperationException($"Duplicate modifier registration: {modifier.Id}");
            }

            _modifiers.Add(modifier.Id, modifier);
            modifier.OnApply(this);
        }

        public void RemoveModifier(string modifierId)
        {
            if (!_modifiers.TryGetValue(modifierId, out var modifier))
            {
                throw new InvalidOperationException($"Modifier is not registered: {modifierId}");
            }

            _modifiers.Remove(modifierId);
            modifier.OnRemove(this);
        }

        public void RemoveModifierByTag(GameplayTag tag)
        {
            var removed = false;
            var idsToRemove = new List<string>();
            foreach (var pair in _modifiers)
            {
                var grantedTags = pair.Value.GrantedTags;
                for (var i = 0; i < grantedTags.Length; i++)
                {
                    if (!grantedTags[i].Equals(tag))
                    {
                        continue;
                    }

                    idsToRemove.Add(pair.Key);
                    break;
                }
            }

            for (var i = 0; i < idsToRemove.Count; i++)
            {
                var id = idsToRemove[i];
                var modifier = _modifiers[id];
                _modifiers.Remove(id);
                modifier.OnRemove(this);
                removed = true;
            }

            if (!removed)
            {
                throw new InvalidOperationException($"No modifier grants tag: {tag}");
            }
        }

        public void AddTrigger(ITrigger trigger)
        {
            if (trigger == null)
            {
                throw new ArgumentNullException(nameof(trigger));
            }

            if (!_triggersByEvent.TryGetValue(trigger.EventType, out var triggers))
            {
                triggers = new List<ITrigger>();
                _triggersByEvent.Add(trigger.EventType, triggers);
            }

            triggers.Add(trigger);
        }

        public IReadOnlyList<ITrigger> GetTriggers(TriggerEventType eventType)
        {
            if (_triggersByEvent.TryGetValue(eventType, out var triggers))
            {
                return triggers;
            }

            return Array.Empty<ITrigger>();
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new InvalidOperationException($"Tick deltaTime must be non-negative: {deltaTime}");
            }

            foreach (var ability in _abilities.Values)
            {
                ability.Tick(deltaTime);
            }

            var expiredIds = new List<string>();
            foreach (var pair in _modifiers)
            {
                var modifier = pair.Value;
                modifier.OnTick(this, deltaTime);
                if (modifier.IsExpired)
                {
                    expiredIds.Add(pair.Key);
                }
            }

            for (var i = 0; i < expiredIds.Count; i++)
            {
                var id = expiredIds[i];
                var modifier = _modifiers[id];
                _modifiers.Remove(id);
                modifier.OnRemove(this);
            }

            foreach (var triggers in _triggersByEvent.Values)
            {
                for (var i = 0; i < triggers.Count; i++)
                {
                    triggers[i].TickCooldown(deltaTime);
                }
            }
        }
    }
}
