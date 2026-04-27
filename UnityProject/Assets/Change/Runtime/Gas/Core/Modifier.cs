using System;
using Change.Framework.Gas;

namespace Change.Runtime.Gas
{
    public sealed class Modifier : IModifier
    {
        private readonly ModifierConfig _config;
        private float _elapsedTime;
        private float _tickTimer;
        private int _stackCount;

        public string Id => _config.Id;
        public ModifierPolarity Polarity => _config.Polarity;
        public GameplayTag[] GrantedTags => _config.GrantedTags;
        public int StackCount => _stackCount;
        public ModifierStacking StackingRule => _config.Stacking;

        public bool IsExpired
        {
            get
            {
                if (_config.Duration <= 0f) return false;
                return _elapsedTime >= _config.Duration;
            }
        }

        public Modifier(ModifierConfig config)
        {
            _config = config;
            _stackCount = 1;
            _elapsedTime = 0f;
            _tickTimer = 0f;
        }

        public void OnApply(IAbilitySystem target)
        {
            if (_config.GrantedTags != null)
            {
                for (int i = 0; i < _config.GrantedTags.Length; i++)
                    target.Tags.AddTag(_config.GrantedTags[i]);
            }
            ExecuteEffects(_config.ApplyEffects, target);
        }

        public void OnTick(IAbilitySystem target, float deltaTime)
        {
            _elapsedTime += deltaTime;
            if (_config.TickInterval > 0f)
            {
                _tickTimer += deltaTime;
                int maxTicksPerFrame = 10;
                int ticksThisFrame = 0;
                while (_tickTimer >= _config.TickInterval && ticksThisFrame < maxTicksPerFrame)
                {
                    _tickTimer -= _config.TickInterval;
                    ExecuteEffects(_config.TickEffects, target);
                    ticksThisFrame++;
                }
                if (ticksThisFrame >= maxTicksPerFrame)
                    _tickTimer = 0f;
            }
        }

        public void OnRemove(IAbilitySystem target)
        {
            if (_config.GrantedTags != null)
            {
                for (int i = 0; i < _config.GrantedTags.Length; i++)
                    target.Tags.RemoveTag(_config.GrantedTags[i]);
            }
            ExecuteEffects(_config.RemoveEffects, target);
        }

        public void AddStack()
        {
            if (_stackCount < _config.MaxStack)
                _stackCount++;
        }

        public void RefreshDuration()
        {
            _elapsedTime = 0f;
            _tickTimer = 0f;
        }

        private void ExecuteEffects(IGameplayEffect[] effects, IAbilitySystem target)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Length; i++)
                effects[i].Execute(target, target);
        }
    }
}
