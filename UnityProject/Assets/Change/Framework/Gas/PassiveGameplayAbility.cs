using System;

namespace Change.Framework.Gas
{
    public class PassiveGameplayAbility : BaseGameplayAbility
    {
        public PassiveGameplayAbility(
            string id,
            ActivationType activation,
            GameplayTag[] tags = null,
            float cooldownDuration = 0f,
            int maxCharges = 1)
            : base(id, AbilityType.Passive, activation, tags, cooldownDuration, maxCharges)
        {
        }

        public override bool CanActivate(IAbilitySystem source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return false;
        }

        public override void Activate(IAbilitySystem source, IAbilitySystem[] targets)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            throw new InvalidOperationException($"Passive ability '{Id}' does not support manual activation.");
        }
    }
}
