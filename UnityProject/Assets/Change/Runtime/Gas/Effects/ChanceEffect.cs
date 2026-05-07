using Change.Framework.Gas;
using System;

namespace Change.Runtime.Gas.Effects
{
    public sealed class ChanceEffect : IGameplayEffect
    {
        private readonly float _chance;
        private readonly IGameplayEffect _effect;
        private readonly Random _random;

        public ChanceEffect(float chance, IGameplayEffect effect, Random random = null)
        {
            _chance = chance;
            _effect = effect;
            _random = random ?? new Random();
        }

        public void Execute(in EffectContext context)
        {
            if (_random.NextDouble() <= _chance)
            {
                _effect.Execute(in context);
            }
        }
    }
}
