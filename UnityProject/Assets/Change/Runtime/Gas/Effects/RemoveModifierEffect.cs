using System.Collections.Generic;
using Change.Framework.Gas;

namespace Change.Runtime.Gas.Effects
{
    public sealed class RemoveModifierEffect : ISkillEffect
    {
        private readonly SkillTag _dispelTag;

        public RemoveModifierEffect(SkillTag dispelTag)
        {
            _dispelTag = dispelTag;
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            target.RemoveModifierByTag(_dispelTag);
        }
    }
}
