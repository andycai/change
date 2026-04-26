namespace Change.Framework.Skill
{
    public interface ITrigger
    {
        TriggerEventType EventType { get; }
        TriggerScope Scope { get; }
        bool EvaluateCondition(IAbilitySystem source, IAbilitySystem target);
        void ExecuteEffects(IAbilitySystem source, IAbilitySystem target, int cascadeDepth);
    }
}
