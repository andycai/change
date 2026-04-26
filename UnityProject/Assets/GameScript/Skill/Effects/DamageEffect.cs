using Change.Framework.Skill;

namespace GameScript.Skill.Effects
{
    public sealed class DamageEffect : ISkillEffect
    {
        private readonly float _flatAmount;
        private readonly string _scalingAttribute;
        private readonly float _scalingMultiplier;

        public DamageEffect(float flatAmount, string scalingFormula, string scalingAttribute, float scalingMultiplier = 1f)
        {
            _flatAmount = flatAmount;
            _scalingAttribute = scalingAttribute;
            _scalingMultiplier = scalingMultiplier;
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            float amount = _flatAmount;
            if (_scalingAttribute != null)
            {
                float scalingValue = source.Attributes.GetCurrentValue(_scalingAttribute);
                amount += scalingValue * _scalingMultiplier;
            }
            float currentHP = target.Attributes.GetCurrentValue("HP");
            target.Attributes.SetBaseValue("HP", currentHP - amount);
        }
    }
}
