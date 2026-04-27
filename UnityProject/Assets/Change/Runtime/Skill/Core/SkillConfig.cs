using System;
using Change.Framework.Skill;

namespace Change.Runtime.Skill
{
    public sealed class SkillConfig
    {
        public string Id;
        public SkillType Type;
        public ActivationType Activation;
        public SkillTag[] Tags;
        public string CostAttribute;
        public float CostAmount;
        public float CooldownDuration;
        public int MaxCharges = 1;
        public ISkillEffect[] Effects;
    }
}
