using System;
using Change.Framework.Skill;

namespace Change.Runtime.Skill
{
    public sealed class Skill : ISkill
    {
        private readonly SkillConfig _config;
        private readonly ISkillEffect[] _effects;
        private float _cooldownTimer;
        private int _currentCharges;

        public string Id => _config.Id;
        public SkillType Type => _config.Type;
        public ActivationType Activation => _config.Activation;
        public SkillState State { get; private set; } = SkillState.Ready;
        public SkillTag[] Tags => _config.Tags;
        public float CooldownDuration => _config.CooldownDuration;
        public int MaxCharges => _config.MaxCharges;
        public int CurrentCharges => _currentCharges;

        public event Action<string> OnStateChange;

        public Skill(SkillConfig config)
        {
            _config = config;
            _effects = config.Effects ?? Array.Empty<ISkillEffect>();
            _currentCharges = config.MaxCharges;
        }

        public bool CanActivate(IAbilitySystem source)
        {
            if (State == SkillState.Cooldown && _currentCharges <= 0)
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

            // Commit cost
            if (_config.CostAttribute != null)
            {
                source.Attributes.ModifyCurrent(_config.CostAttribute, -_config.CostAmount);
            }

            SetState(SkillState.Casting);
            SetState(SkillState.Executing);

            for (int i = 0; i < _effects.Length; i++)
            {
                for (int t = 0; t < targets.Length; t++)
                {
                    _effects[i].Execute(source, targets[t]);
                }
            }

            // Start cooldown
            _currentCharges--;
            if (_currentCharges <= 0)
            {
                SetState(SkillState.Cooldown);
                _cooldownTimer = _config.CooldownDuration;
            }
            else
            {
                SetState(SkillState.Ready);
            }
        }

        public void TickCooldown(float deltaTime)
        {
            if (State != SkillState.Cooldown)
                return;

            _cooldownTimer -= deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _currentCharges = _config.MaxCharges;
                _cooldownTimer = 0f;
                SetState(SkillState.Ready);
            }
        }

        private void SetState(SkillState state)
        {
            State = state;
            OnStateChange?.Invoke(state.ToString());
        }
    }
}
