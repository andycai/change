using Change.Framework.Gas;

namespace Change.Runtime.Gas.Effects
{
    public sealed class ChanceEffect : IGameplayEffect
    {
        private readonly float _chance;
        private readonly IGameplayEffect _effect;
        private readonly System.Random _random;

        public ChanceEffect(float chance, IGameplayEffect effect)
        {
            _chance = chance;
            _effect = effect;
            _random = new System.Random();
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            if (_random.NextDouble() <= _chance)
            {
                _effect.Execute(source, target);
            }
        }
    }
}
