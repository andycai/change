using Change.Framework.Skill;

namespace Change.Runtime.Skill.Effects
{
    public sealed class HealEffect : ISkillEffect
    {
        private readonly float _flatAmount;
        private readonly string _scalingAttribute;
        private readonly float _scalingMultiplier;
        private readonly string _targetAttribute;

        public HealEffect(float flatAmount, string scalingFormula, string scalingAttribute, float scalingMultiplier = 1f, string targetAttribute = "HP")
        {
            _flatAmount = flatAmount;
            _scalingAttribute = scalingAttribute;
            _scalingMultiplier = scalingMultiplier;
            _targetAttribute = targetAttribute;
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            float amount = _flatAmount;
            if (_scalingAttribute != null)
            {
                float scalingValue = source.Attributes.GetCurrentValue(_scalingAttribute);
                amount += scalingValue * _scalingMultiplier;
            }
            target.Attributes.ModifyCurrent(_targetAttribute, amount);
        }
    }
}
