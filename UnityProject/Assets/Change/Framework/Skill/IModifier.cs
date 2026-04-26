namespace Change.Framework.Skill
{
    public interface IModifier
    {
        string Id { get; }
        ModifierPolarity Polarity { get; }
        SkillTag[] GrantedTags { get; }
        int StackCount { get; }
        bool IsExpired { get; }
        void OnApply(IAbilitySystem target);
        void OnTick(IAbilitySystem target, float deltaTime);
        void OnRemove(IAbilitySystem target);
    }
}
