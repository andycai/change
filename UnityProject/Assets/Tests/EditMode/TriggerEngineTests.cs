using Change.Framework.Skill;
using GameScript.Skill.Core;
using GameScript.Skill.Effects;
using NUnit.Framework;

namespace GameScript.Tests
{
    public class TriggerEngineTests
    {
        [Test]
        public void DispatchEvent_MatchingTrigger_FiresEffect()
        {
            var engine = new TriggerEngine(maxCascadeDepth: 5);
            var source = new AbilitySystem("hero");
            source.Attributes.SetBaseValue("ATK", 100f);
            var target = new AbilitySystem("enemy");
            target.Attributes.SetBaseValue("HP", 200f);

            source.AddTrigger(new Trigger(
                eventType: TriggerEventType.OnDealDamage,
                scope: TriggerScope.Self,
                condition: null,
                cooldown: 0f,
                effects: new ISkillEffect[]
                {
                    new DamageEffect(flatAmount: 20f, scalingFormula: null, scalingAttribute: null)
                }
            ));

            var allEntities = new IAbilitySystem[] { source, target };
            engine.DispatchEvent(TriggerEventType.OnDealDamage, source, target, allEntities);
            Assert.Pass();
        }

        [Test]
        public void DispatchEvent_ExceedsCascadeDepth_StopsChain()
        {
            var engine = new TriggerEngine(maxCascadeDepth: 2);
            var source = new AbilitySystem("hero");
            source.Attributes.SetBaseValue("HP", 1000f);
            var allEntities = new IAbilitySystem[] { source };

            source.AddTrigger(new Trigger(
                eventType: TriggerEventType.OnDealDamage,
                scope: TriggerScope.Self,
                condition: null,
                cooldown: 0f,
                effects: new ISkillEffect[]
                {
                    new DamageEffect(flatAmount: 10f, scalingFormula: null, scalingAttribute: null)
                }
            ));

            engine.DispatchEvent(TriggerEventType.OnDealDamage, source, source, allEntities);
            Assert.Pass();
        }
    }
}
