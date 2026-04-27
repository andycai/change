namespace Change.Framework.Gas
{
    public interface IModifier
    {
        string Id { get; }
        ModifierPolarity Polarity { get; }
        GameplayTag[] GrantedTags { get; }
        int StackCount { get; }
        bool IsExpired { get; }
        ModifierStacking StackingRule { get; }
        void OnApply(IAbilitySystem target);
        void OnTick(IAbilitySystem target, float deltaTime);
        void OnRemove(IAbilitySystem target);
        void AddStack();
        void RefreshDuration();
    }
}
