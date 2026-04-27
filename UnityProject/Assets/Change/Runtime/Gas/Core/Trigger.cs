using System;
using Change.Framework.Gas;

namespace Change.Runtime.Gas
{
    public sealed class Trigger : ITrigger
    {
        private readonly ISkillEffect[] _effects;
        private readonly float _cooldown;
        private readonly Func<IAbilitySystem, IAbilitySystem, bool> _condition;
        private float _cooldownTimer;

        public TriggerEventType EventType { get; }
        public TriggerScope Scope { get; }

        public Trigger(
            TriggerEventType eventType,
            TriggerScope scope,
            Func<IAbilitySystem, IAbilitySystem, bool> condition,
            float cooldown,
            ISkillEffect[] effects)
        {
            EventType = eventType;
            Scope = scope;
            _condition = condition;
            _cooldown = cooldown;
            _effects = effects ?? Array.Empty<ISkillEffect>();
            _cooldownTimer = 0f;
        }

        public bool EvaluateCondition(IAbilitySystem source, IAbilitySystem target) =>
            _condition == null || _condition(source, target);

        public void ExecuteEffects(IAbilitySystem source, IAbilitySystem target, int cascadeDepth)
        {
            for (int i = 0; i < _effects.Length; i++)
                _effects[i].Execute(source, target);
            _cooldownTimer = _cooldown;
        }

        public bool TryFire(IAbilitySystem source, IAbilitySystem target, int cascadeDepth)
        {
            if (_cooldownTimer > 0f) return false;
            if (!EvaluateCondition(source, target)) return false;
            ExecuteEffects(source, target, cascadeDepth);
            return true;
        }

        public void TickCooldown(float deltaTime)
        {
            if (_cooldownTimer > 0f) _cooldownTimer -= deltaTime;
        }
    }
}
