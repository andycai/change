using System;

namespace Change.Framework.Gas
{
    public abstract class BaseGameplayAbility : IGameplayAbility
    {
        private float _cooldownRemaining;

        protected BaseGameplayAbility(
            string id,
            AbilityType type,
            ActivationType activation,
            GameplayTag[] tags,
            float cooldownDuration,
            int maxCharges)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Ability id cannot be null or empty.", nameof(id));
            }

            if (cooldownDuration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownDuration), "Cooldown duration cannot be negative.");
            }

            if (maxCharges <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCharges), "Max charges must be greater than zero.");
            }

            Id = id;
            Type = type;
            Activation = activation;
            Tags = tags ?? Array.Empty<GameplayTag>();
            CooldownDuration = cooldownDuration;
            MaxCharges = maxCharges;
            CurrentCharges = maxCharges;
            State = AbilityState.Ready;
        }

        public string Id { get; }
        public AbilityType Type { get; }
        public ActivationType Activation { get; }
        public AbilityState State { get; private set; }
        public GameplayTag[] Tags { get; }
        public float CooldownDuration { get; }
        public int MaxCharges { get; }
        public int CurrentCharges { get; private set; }
        public event Action<AbilityState> OnStateChange;

        public virtual bool CanActivate(IAbilitySystem source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return State == AbilityState.Ready
                && CurrentCharges > 0
                && _cooldownRemaining <= 0f
                && CanActivateCore(source);
        }

        public virtual void Activate(IAbilitySystem source, IAbilitySystem[] targets)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            if (!CanActivate(source))
            {
                throw new InvalidOperationException($"Ability '{Id}' cannot be activated while in state '{State}'.");
            }

            SetState(AbilityState.Executing);
            OnActivate(source, targets);
            ConsumeChargeAndAdvanceState();
        }

        public virtual void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
            }

            if (State != AbilityState.Cooldown)
            {
                return;
            }

            _cooldownRemaining -= deltaTime;
            if (_cooldownRemaining > 0f)
            {
                return;
            }

            _cooldownRemaining = 0f;
            CurrentCharges = MaxCharges;
            SetState(AbilityState.Ready);
        }

        protected virtual bool CanActivateCore(IAbilitySystem source)
        {
            return true;
        }

        protected virtual void OnActivate(IAbilitySystem source, IAbilitySystem[] targets)
        {
        }

        protected void SetState(AbilityState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            OnStateChange?.Invoke(state);
        }

        private void ConsumeChargeAndAdvanceState()
        {
            CurrentCharges--;
            if (CurrentCharges < 0)
            {
                throw new InvalidOperationException($"Ability '{Id}' charges cannot be negative.");
            }

            if (CooldownDuration <= 0f || CurrentCharges > 0)
            {
                SetState(AbilityState.Ready);
                return;
            }

            _cooldownRemaining = CooldownDuration;
            SetState(AbilityState.Cooldown);
        }
    }
}
