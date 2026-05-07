using System;
using Change.Framework.Gas;

namespace Change.Runtime.Gas
{
    public sealed class ModifierConfig
    {
        public string Id;
        public ModifierPolarity Polarity;
        public GameplayTag[] GrantedTags;
        public ModifierStacking Stacking;
        public int MaxStack = 1;
        public float Duration;
        public float TickInterval;
        
        public IGameplayEffect[] ApplyEffects;
        public IGameplayEffect[] TickEffects;
        public IGameplayEffect[] RemoveEffects;

        public void Validate()
        {
            if (string.IsNullOrEmpty(Id))
                throw new InvalidOperationException("ModifierConfig.Id is required.");
            if (MaxStack <= 0)
                throw new InvalidOperationException("ModifierConfig.MaxStack must be greater than zero.");
            if (Duration < 0f)
                throw new InvalidOperationException("ModifierConfig.Duration cannot be negative.");
            if (TickInterval < 0f)
                throw new InvalidOperationException("ModifierConfig.TickInterval cannot be negative.");
        }
    }
}
