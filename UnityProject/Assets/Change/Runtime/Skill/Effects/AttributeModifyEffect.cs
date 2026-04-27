using Change.Framework.Skill;

namespace Change.Runtime.Skill.Effects
{
    public sealed class AttributeModifyEffect : ISkillEffect
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

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
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
