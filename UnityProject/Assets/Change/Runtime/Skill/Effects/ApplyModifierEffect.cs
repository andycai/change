using Change.Framework.Skill;

namespace Change.Runtime.Skill.Effects
{
    public sealed class ApplyModifierEffect : ISkillEffect
    {
        private readonly string _modifierId;
        public ApplyModifierEffect(string modifierId) { _modifierId = modifierId; }
        public void Execute(IAbilitySystem source, IAbilitySystem target) { /* Connected in Task 10 */ }
    }
}
