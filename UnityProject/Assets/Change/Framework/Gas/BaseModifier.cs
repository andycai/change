using System;

namespace Change.Framework.Gas
{
    public abstract class BaseModifier : IModifier
    {
        private readonly float _baseDurationSeconds;
        private float _remainingDurationSeconds;

        protected BaseModifier(string id, ModifierPolarity polarity, float durationSeconds, ModifierStacking stackingRule, GameplayTag[] grantedTags)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Modifier id cannot be null or empty.", nameof(id));
            }

            if (durationSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Modifier duration must be non-negative.");
            }

            Id = id;
            Polarity = polarity;
            _baseDurationSeconds = durationSeconds;
            _remainingDurationSeconds = durationSeconds;
            StackingRule = stackingRule;
            GrantedTags = grantedTags ?? throw new ArgumentNullException(nameof(grantedTags));
            StackCount = 1;
        }

        public string Id { get; }
        public ModifierPolarity Polarity { get; }
        public GameplayTag[] GrantedTags { get; }
        public int StackCount { get; private set; }
        public bool IsExpired { get; private set; }
        public ModifierStacking StackingRule { get; }

        public void OnApply(IAbilitySystem target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            IsExpired = false;
            _remainingDurationSeconds = _baseDurationSeconds;
            OnApplied(target);
        }

        public void OnTick(IAbilitySystem target, float deltaTime)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Tick delta time must be non-negative.");
            }

            if (IsExpired)
            {
                return;
            }

            OnTickCore(target, deltaTime);

            if (_baseDurationSeconds <= 0f)
            {
                return;
            }

            _remainingDurationSeconds -= deltaTime;
            if (_remainingDurationSeconds > 0f)
            {
                return;
            }

            IsExpired = true;
            OnExpired(target);
        }

        public void OnRemove(IAbilitySystem target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            OnRemoved(target);
        }

        public void AddStack()
        {
            switch (StackingRule)
            {
                case ModifierStacking.Refresh:
                    RefreshDuration();
                    break;
                case ModifierStacking.AddStack:
                    StackCount++;
                    OnStackCountChanged();
                    break;
                case ModifierStacking.Replace:
                    StackCount = 1;
                    RefreshDuration();
                    OnStackCountChanged();
                    break;
                case ModifierStacking.Ignore:
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported stacking rule: {StackingRule}");
            }
        }

        public void RefreshDuration()
        {
            _remainingDurationSeconds = _baseDurationSeconds;
            IsExpired = false;
            OnDurationRefreshed();
        }

        protected virtual void OnApplied(IAbilitySystem target) { }
        protected virtual void OnTickCore(IAbilitySystem target, float deltaTime) { }
        protected virtual void OnRemoved(IAbilitySystem target) { }
        protected virtual void OnExpired(IAbilitySystem target) { }
        protected virtual void OnDurationRefreshed() { }
        protected virtual void OnStackCountChanged() { }
    }
}
