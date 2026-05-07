using System;
using Change.Framework.Gas;

namespace Change.Runtime.Gas.Effects
{
    public sealed class ApplyModifierEffect : IGameplayEffect
    {
        private readonly Func<IModifier> _modifierFactory;

        public ApplyModifierEffect(Func<IModifier> modifierFactory)
        {
            _modifierFactory = modifierFactory ?? throw new ArgumentNullException(nameof(modifierFactory));
        }

        public void Execute(in EffectContext context)
        {
            context.Target.AddModifier(_modifierFactory());
        }
    }
}
