using System;
using Change.Framework.Gas;
using Change.Runtime.Gas.Effects;

namespace Change.Runtime.Gas
{
    public sealed class GameplayAbility : IGameplayAbility
    {
        private readonly AbilityConfig _config;
        private readonly IGameplayEffect[] _effects;
        private readonly IGameplayEffect[] _costEffects;
        private readonly IGameplayEffect[] _cooldownEffects;
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

        public event Action<AbilityState> OnStateChange;

        public GameplayAbility(AbilityConfig config)
        {
            _config = config;
            _config.Validate();
            _effects = config.Effects ?? Array.Empty<IGameplayEffect>();
            _costEffects = CreateCostEffects(config);
            _cooldownEffects = CreateCooldownEffects(config);
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
            _currentTargets = targets ?? Array.Empty<IAbilitySystem>();

            ExecuteEffects(_costEffects, new EffectContext(source, source, this));

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
                    if (_currentTargets[t] == null)
                        continue;
                    var effectContext = new EffectContext(_currentSource, _currentTargets[t], this);
                    _effects[i].Execute(in effectContext);
                }
            }

            Finish();
        }

        private void Finish()
        {
            _currentCharges--;
            if (_currentCharges <= 0)
            {
                ExecuteEffects(_cooldownEffects, new EffectContext(_currentSource, _currentSource, this));
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
            OnStateChange?.Invoke(state);
        }

        internal void StartCooldown(float duration)
        {
            if (duration <= 0f)
            {
                _currentCharges = _config.MaxCharges;
                SetState(AbilityState.Ready);
                return;
            }

            SetState(AbilityState.Cooldown);
            _cooldownTimer = duration;
        }

        private static IGameplayEffect[] CreateCostEffects(AbilityConfig config)
        {
            if (string.IsNullOrEmpty(config.CostAttribute) || config.CostAmount <= 0f)
                return Array.Empty<IGameplayEffect>();

            return new IGameplayEffect[] { new CostEffect(config.CostAttribute, config.CostAmount) };
        }

        private static IGameplayEffect[] CreateCooldownEffects(AbilityConfig config)
        {
            if (config.CooldownDuration <= 0f)
                return Array.Empty<IGameplayEffect>();

            return new IGameplayEffect[] { new CooldownEffect(config.CooldownDuration) };
        }

        private static void ExecuteEffects(IGameplayEffect[] effects, in EffectContext context)
        {
            for (int i = 0; i < effects.Length; i++)
                effects[i].Execute(in context);
        }
    }
}
