using Change.Framework.Gas;

namespace Change.Runtime.Gas
{
    public sealed class AbilityConfig
    {
        public string Id;
        public AbilityType Type;
        public ActivationType Activation;
        public GameplayTag[] Tags;
        public float CooldownDuration;
        public int MaxCharges = 1;
        public float CastTime; // Added CastTime support
        
        public string CostAttribute;
        public float CostAmount;
        
        public GameplayTag[] BlockingTags;
        public IGameplayEffect[] Effects;
    }
}
