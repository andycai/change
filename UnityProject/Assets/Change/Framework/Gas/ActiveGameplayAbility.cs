namespace Change.Framework.Gas
{
    public class ActiveGameplayAbility : BaseGameplayAbility
    {
        public ActiveGameplayAbility(
            string id,
            ActivationType activation,
            GameplayTag[] tags,
            float cooldownDuration,
            int maxCharges)
            : base(id, AbilityType.Active, activation, tags, cooldownDuration, maxCharges)
        {
        }
    }
}
