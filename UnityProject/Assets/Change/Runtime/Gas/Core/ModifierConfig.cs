using Change.Framework.Gas;

namespace Change.Runtime.Gas
{
    public sealed class ModifierConfig
    {
        public string Id;
        public ModifierPolarity Polarity;
        public SkillTag[] GrantedTags;
        public ModifierStacking Stacking;
        public int MaxStack = 1;
        public float Duration;
        public float TickInterval;
        public ISkillEffect[] ApplyEffects;
        public ISkillEffect[] TickEffects;
        public ISkillEffect[] RemoveEffects;
    }
}
