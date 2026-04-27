using Change.Framework.Gas;

namespace Change.Runtime.Gas.Effects
{
    public sealed class HealEffect : IGameplayEffect
    {
        private readonly AttributeModifyEffect _inner;

        public HealEffect(float flatAmount, string scalingFormula = null,
            string scalingAttribute = null, float scalingMultiplier = 1f, string targetAttribute = "HP")
        {
            _inner = new AttributeModifyEffect(flatAmount, scalingAttribute, scalingMultiplier, targetAttribute, isDamage: false);
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target) => _inner.Execute(source, target);
    }
}
