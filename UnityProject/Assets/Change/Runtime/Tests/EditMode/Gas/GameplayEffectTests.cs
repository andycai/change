using Change.Framework.Gas;
using Change.Runtime.Gas;
using Change.Runtime.Gas.Effects;
using NUnit.Framework;

namespace Change.Runtime.Tests.Gas
{
    public class GameplayEffectTests
    {
        private AbilitySystem _source;
        private AbilitySystem _target;

        [SetUp]
        public void SetUp()
        {
            _source = new AbilitySystem("source");
            _source.Attributes.SetBaseValue("ATK", 100f);
            _source.Attributes.SetBaseValue("HP", 100f);
            _target = new AbilitySystem("target");
            _target.Attributes.SetBaseValue("HP", 200f);
            _target.Attributes.SetBaseValue("DEF", 0f);
        }

        [Test]
        public void DamageEffect_Flat_ReducesTargetHP()
        {
            var effect = new DamageEffect(flatAmount: 50f, scalingAttribute: null);
            var context = new EffectContext(_source, _target);
            effect.Execute(in context);
            Assert.AreEqual(150f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void DamageEffect_Scaling_UsesSourceAttribute()
        {
            var effect = new DamageEffect(flatAmount: 10f, scalingAttribute: "ATK", scalingMultiplier: 1.5f);
            var context = new EffectContext(_source, _target);
            effect.Execute(in context);
            Assert.AreEqual(40f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void HealEffect_IncreasesTargetHP()
        {
            _target.Attributes.SetBaseValue("HP", 50f);
            var effect = new HealEffect(flatAmount: 30f, scalingAttribute: null);
            var context = new EffectContext(_source, _target);
            effect.Execute(in context);
            Assert.AreEqual(80f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void ChanceEffect_OnSuccess_AppliesEffect()
        {
            var innerEffect = new DamageEffect(flatAmount: 25f, scalingAttribute: null);
            var effect = new ChanceEffect(chance: 1.0f, effect: innerEffect);
            var context = new EffectContext(_source, _target);
            effect.Execute(in context);
            Assert.AreEqual(175f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void TagEffect_Grant_AddsTagToTarget()
        {
            var effect = new TagEffect(tag: new GameplayTag("state.stunned"), isAdd: true);
            var context = new EffectContext(_source, _target);
            effect.Execute(in context);
            Assert.IsTrue(_target.Tags.HasTag(new GameplayTag("state.stunned")));
        }

        [Test]
        public void TagEffect_Remove_RemovesTagFromTarget()
        {
            _target.Tags.AddTag(new GameplayTag("state.stunned"));
            var effect = new TagEffect(tag: new GameplayTag("state.stunned"), isAdd: false);
            var context = new EffectContext(_source, _target);
            effect.Execute(in context);
            Assert.IsFalse(_target.Tags.HasTag(new GameplayTag("state.stunned")));
        }
    }
}
