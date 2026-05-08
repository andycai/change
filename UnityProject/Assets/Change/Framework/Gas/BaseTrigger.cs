using System;

namespace Change.Framework.Gas
{
    public abstract class BaseTrigger : ITrigger
    {
        private readonly float _cooldownSeconds;
        private float _remainingCooldownSeconds;

        protected BaseTrigger(TriggerEventType eventType, TriggerScope scope, float cooldownSeconds)
        {
            if (cooldownSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds), "Cooldown seconds must be non-negative.");
            }

            EventType = eventType;
            Scope = scope;
            _cooldownSeconds = cooldownSeconds;
        }

        public const int MaxCascadeDepth = 8;

        public TriggerEventType EventType { get; }
        public TriggerScope Scope { get; }

        public bool EvaluateCondition(IAbilitySystem source, IAbilitySystem target)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return Condition(source, target);
        }

        public void ExecuteEffects(IAbilitySystem source, IAbilitySystem target, int cascadeDepth)
        {
            ValidateExecutionArguments(source, target, cascadeDepth);
            Execute(source, target, cascadeDepth);
        }

        public bool TryFire(IAbilitySystem source, IAbilitySystem target, int cascadeDepth)
        {
            ValidateExecutionArguments(source, target, cascadeDepth);

            if (cascadeDepth > MaxCascadeDepth)
            {
                return false;
            }

            if (_remainingCooldownSeconds > 0f)
            {
                return false;
            }

            if (!EvaluateCondition(source, target))
            {
                return false;
            }

            ExecuteEffects(source, target, cascadeDepth);
            _remainingCooldownSeconds = _cooldownSeconds;
            return true;
        }

        public void TickCooldown(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Tick delta time must be non-negative.");
            }

            if (_remainingCooldownSeconds <= 0f)
            {
                return;
            }

            _remainingCooldownSeconds -= deltaTime;
            if (_remainingCooldownSeconds < 0f)
            {
                _remainingCooldownSeconds = 0f;
            }
        }

        protected virtual bool Condition(IAbilitySystem source, IAbilitySystem target)
        {
            return true;
        }

        protected abstract void Execute(IAbilitySystem source, IAbilitySystem target, int cascadeDepth);

        private static void ValidateExecutionArguments(IAbilitySystem source, IAbilitySystem target, int cascadeDepth)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (cascadeDepth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cascadeDepth), "Cascade depth must be non-negative.");
            }
        }
    }
}
