using System;
using Change.Framework.Gas;

namespace Change.Runtime.Gas.Effects
{
    public sealed class ChanceEffect : ISkillEffect
    {
        private readonly float _rate;
        private readonly ISkillEffect[] _onSuccess;
        private readonly ISkillEffect[] _onFailure;
        private readonly Random _rng;

        public ChanceEffect(float rate, ISkillEffect[] onSuccess, ISkillEffect[] onFailure)
        {
            _rate = rate;
            _onSuccess = onSuccess ?? Array.Empty<ISkillEffect>();
            _onFailure = onFailure ?? Array.Empty<ISkillEffect>();
            _rng = new Random();
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            var effects = _rng.NextDouble() < _rate ? _onSuccess : _onFailure;
            for (int i = 0; i < effects.Length; i++)
                effects[i].Execute(source, target);
        }
    }
}
