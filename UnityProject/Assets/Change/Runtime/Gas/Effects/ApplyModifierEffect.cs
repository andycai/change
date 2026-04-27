using System;
using Change.Framework.Gas;

namespace Change.Runtime.Gas.Effects
{
    public sealed class ApplyModifierEffect : ISkillEffect
    {
        private readonly string _modifierId;
        private readonly Func<string, IModifier> _modifierFactory;

        public ApplyModifierEffect(string modifierId, Func<string, IModifier> modifierFactory)
        {
            _modifierId = modifierId;
            _modifierFactory = modifierFactory;
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            var modifier = _modifierFactory(_modifierId);
            if (modifier != null)
                target.AddModifier(modifier);
        }
    }
}
