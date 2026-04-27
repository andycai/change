using System;
using Change.Framework.Gas;

namespace Change.Runtime.Gas
{
    public sealed class GameplayAbility : IGameplayAbility
    {
        private readonly AbilityConfig _config;
        private readonly IGameplayEffect[] _effects;
        private float _cooldownTimer;
        private float _castTimer;
        private int _currentCharges;
        private IAbilitySystem _currentSource;
        private IAbilitySystem[] _currentTargets;

        public string Id => _config.Id;
        public AbilityType Type => _config.Type;
        public ActivationType Activation => _config.Activation;
        public AbilityState State { get; private set; } = AbilityState.Ready;
        public GameplayTag[] Tags => _config.Tags;
        public float CooldownDuration => _config.CooldownDuration;
        public int MaxCharges => _config.MaxCharges;
        public int CurrentCharges => _currentCharges;

        public event Action<string> OnStateChange;

        public GameplayAbility(AbilityConfig config)
        {
            _config = config;
            _effects = config.Effects ?? Array.Empty<IGameplayEffect>();
            _currentCharges = config.MaxCharges;
        }

        public bool CanActivate(IAbilitySystem source)
        {
            if (State != AbilityState.Ready && State != AbilityState.Cooldown)
                return false;

            if (State == AbilityState.Cooldown && _currentCharges <= 0)
                return false;

            if (_config.CostAttribute != null)
            {
                float current = source.Attributes.GetCurrentValue(_config.CostAttribute);
                if (current < _config.CostAmount)
                    return false;
            }

            if (_config.BlockingTags != null)
            {
                for (int i = 0; i < _config.BlockingTags.Length; i++)
                {
                    if (source.Tags.HasTag(_config.BlockingTags[i]))
                        return false;
                }
            }

            return true;
        }

        public void Activate(IAbilitySystem source, IAbilitySystem[] targets)
        {
            if (!CanActivate(source))
                return;

            _currentSource = source;
            _currentTargets = targets;

            // Commit cost
            if (_config.CostAttribute != null)
            {
                source.Attributes.ModifyCurrent(_config.CostAttribute, -_config.CostAmount);
            }

            if (_config.CastTime > 0f)
            {
                SetState(AbilityState.Casting);
                _castTimer = _config.CastTime;
            }
            else
            {
                Execute();
            }
        }

        private void Execute()
        {
            SetState(AbilityState.Executing);

            for (int i = 0; i < _effects.Length; i++)
            {
                for (int t = 0; t < _currentTargets.Length; t++)
                {
                    _effects[i].Execute(_currentSource, _currentTargets[t]);
                }
            }

            Finish();
        }

        private void Finish()
        {
            _currentCharges--;
            if (_currentCharges <= 0)
            {
                SetState(AbilityState.Cooldown);
                _cooldownTimer = _config.CooldownDuration;
            }
            else
            {
                SetState(AbilityState.Ready);
            }
            
            _currentSource = null;
            _currentTargets = null;
        }

        public void Tick(float deltaTime)
        {
            if (State == AbilityState.Casting)
            {
                _castTimer -= deltaTime;
                if (_castTimer <= 0f)
                {
                    Execute();
                }
            }
            else if (State == AbilityState.Cooldown)
            {
                _cooldownTimer -= deltaTime;
                if (_cooldownTimer <= 0f)
                {
                    _currentCharges = _config.MaxCharges;
                    _cooldownTimer = 0f;
                    SetState(AbilityState.Ready);
                }
            }
        }

        private void SetState(AbilityState state)
        {
            State = state;
            OnStateChange?.Invoke(state.ToString());
        }
    }
}
