using Change.Framework.Gas;
using Change.Runtime.Gas;

namespace GameScript.GasTemplate.Demo
{
    public static class DemoTriggers
    {
        public static TriggerEngine CreateEngine()
        {
            return new TriggerEngine(maxCascadeDepth: 3);
        }

        public static Trigger CreateOnDealDamageHealTrigger()
        {
            return new Trigger(
                eventType: TriggerEventType.OnDealDamage,
                scope: TriggerScope.Source,
                condition: null,
                cooldown: 0f,
                effects: new[] { DemoEffects.TriggerHeal() });
        }
    }
}
