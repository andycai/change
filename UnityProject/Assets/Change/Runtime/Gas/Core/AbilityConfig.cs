using Change.Framework.Gas;
using System;

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

        public void Validate()
        {
            if (string.IsNullOrEmpty(Id))
                throw new InvalidOperationException("AbilityConfig.Id is required.");
            if (CooldownDuration < 0f)
                throw new InvalidOperationException("AbilityConfig.CooldownDuration cannot be negative.");
            if (MaxCharges <= 0)
                throw new InvalidOperationException("AbilityConfig.MaxCharges must be greater than zero.");
            if (CastTime < 0f)
                throw new InvalidOperationException("AbilityConfig.CastTime cannot be negative.");
            if (CostAmount < 0f)
                throw new InvalidOperationException("AbilityConfig.CostAmount cannot be negative.");
        }
    }
}
