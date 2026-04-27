using Change.Framework.Gas;

namespace Change.Runtime.Gas.Effects
{
    public sealed class DamageEffect : ISkillEffect
    {
        private readonly AttributeModifyEffect _inner;

        public DamageEffect(float flatAmount, string scalingFormula = null,
            string scalingAttribute = null, float scalingMultiplier = 1f, string targetAttribute = "HP")
        {
            _inner = new AttributeModifyEffect(flatAmount, scalingAttribute, scalingMultiplier, targetAttribute, isDamage: true);
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target) => _inner.Execute(source, target);
    }
}
