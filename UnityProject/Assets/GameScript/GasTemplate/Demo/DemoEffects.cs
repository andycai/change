using Change.Framework.Gas;
using Change.Runtime.Gas.Effects;

namespace GameScript.GasTemplate.Demo
{
    public static class DemoEffects
    {
        public static IGameplayEffect CastDamage()
        {
            return new DamageEffect(flatAmount: 40f, scalingAttribute: "ATK", scalingMultiplier: 0.5f);
        }

        public static IGameplayEffect BurnTickDamage()
        {
            return new DamageEffect(flatAmount: 30f);
        }

        public static IGameplayEffect TriggerHeal()
        {
            return new HealEffect(flatAmount: 15f);
        }
    }
}
