using Change.Framework.Gas;

namespace Change.Runtime.Gas.Effects
{
    public sealed class AttributeModifyEffect : IGameplayEffect
    {
        private readonly float _flatAmount;
        private readonly string _scalingAttribute;
        private readonly float _scalingMultiplier;
        private readonly string _targetAttribute;
        private readonly bool _isDamage;

        public AttributeModifyEffect(float flatAmount, string scalingAttribute = null,
            float scalingMultiplier = 1f, string targetAttribute = "HP", bool isDamage = true)
        {
            _flatAmount = flatAmount;
            _scalingAttribute = scalingAttribute;
            _scalingMultiplier = scalingMultiplier;
            _targetAttribute = targetAttribute;
            _isDamage = isDamage;
        }

        public void Execute(in EffectContext context)
        {
            var source = context.Source;
            var target = context.Target;
            float amount = _flatAmount;
            if (_scalingAttribute != null)
            {
                float scalingValue = source.Attributes.GetCurrentValue(_scalingAttribute);
                amount += scalingValue * _scalingMultiplier;
            }
            target.Attributes.ModifyCurrent(_targetAttribute, _isDamage ? -amount : amount);
        }
    }
}
