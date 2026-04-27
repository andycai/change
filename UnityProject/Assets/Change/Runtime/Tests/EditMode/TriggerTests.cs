using Change.Framework.Gas;
using Change.Runtime.Gas;
using Change.Runtime.Gas.Effects;
using NUnit.Framework;

namespace Change.Runtime.Tests
{
    public class TriggerTests
    {
        private AbilitySystem _source;
        private AbilitySystem _target;

        [SetUp]
        public void SetUp()
        {
            _source = new AbilitySystem("source");
            _source.Attributes.SetBaseValue("ATK", 100f);
            _target = new AbilitySystem("target");
            _target.Attributes.SetBaseValue("HP", 200f);
        }

        [Test]
        public void ExecuteEffects_AppliesDamage()
        {
            var trigger = new Trigger(
                eventType: TriggerEventType.OnDealDamage,
                scope: TriggerScope.Self,
                condition: null,
                cooldown: 0f,
                effects: new ISkillEffect[] { new DamageEffect(flatAmount: 30f, scalingFormula: null, scalingAttribute: null) }
            );
            trigger.ExecuteEffects(_source, _target, 0);
            Assert.AreEqual(170f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void EvaluateCondition_NoCondition_ReturnsTrue()
        {
            var trigger = new Trigger(TriggerEventType.OnAttack, TriggerScope.Self, null, 0f, null);
            Assert.IsTrue(trigger.EvaluateCondition(_source, _target));
        }

        [Test]
        public void Cooldown_PreventsReFireWithinPeriod()
        {
            var trigger = new Trigger(
                TriggerEventType.OnAttack, TriggerScope.Self, null, 5f,
                new ISkillEffect[] { new DamageEffect(flatAmount: 10f, scalingFormula: null, scalingAttribute: null) }
            );
            trigger.ExecuteEffects(_source, _target, 0);
            bool didFire = trigger.TryFire(_source, _target, 0);
            Assert.IsFalse(didFire);
        }
    }
}
