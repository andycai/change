using Change.Framework.Skill;

namespace GameScript.Skill.Effects
{
    public sealed class RemoveModifierEffect : ISkillEffect
    {
        private readonly SkillTag _dispelTag;
        public RemoveModifierEffect(SkillTag dispelTag) { _dispelTag = dispelTag; }
        public void Execute(IAbilitySystem source, IAbilitySystem target) { /* Connected in Task 10 */ }
    }
}
