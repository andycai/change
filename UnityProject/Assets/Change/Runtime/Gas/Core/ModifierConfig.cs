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
    }
}
