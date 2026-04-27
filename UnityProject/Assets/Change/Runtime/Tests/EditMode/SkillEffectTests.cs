using Change.Framework.Gas;
using Change.Runtime.Gas;
using Change.Runtime.Gas.Effects;
using NUnit.Framework;

namespace Change.Runtime.Tests
{
    public class SkillEffectTests
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
            var effect = new DamageEffect(flatAmount: 50f, scalingFormula: null, scalingAttribute: null);
            effect.Execute(_source, _target);
            Assert.AreEqual(150f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void DamageEffect_Scaling_UsesSourceAttribute()
        {
            var effect = new DamageEffect(flatAmount: 10f, scalingFormula: null, scalingAttribute: "ATK", scalingMultiplier: 1.5f);
            effect.Execute(_source, _target);
            Assert.AreEqual(40f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void HealEffect_IncreasesTargetHP()
        {
            _target.Attributes.SetBaseValue("HP", 50f);
            var effect = new HealEffect(flatAmount: 30f, scalingFormula: null, scalingAttribute: null);
            effect.Execute(_source, _target);
            Assert.AreEqual(80f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void ChanceEffect_OnSuccess_AppliesEffect()
        {
            var innerEffect = new DamageEffect(flatAmount: 25f, scalingFormula: null, scalingAttribute: null);
            var effect = new ChanceEffect(rate: 1.0f, onSuccess: new ISkillEffect[] { innerEffect }, onFailure: null);
            effect.Execute(_source, _target);
            Assert.AreEqual(175f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void TagEffect_Grant_AddsTagToTarget()
        {
            var effect = new TagEffect(grant: true, tag: new SkillTag("state.stunned"));
            effect.Execute(_source, _target);
            Assert.IsTrue(_target.Tags.HasTag(new SkillTag("state.stunned")));
        }

        [Test]
        public void TagEffect_Remove_RemovesTagFromTarget()
        {
            _target.Tags.AddTag(new SkillTag("state.stunned"));
            var effect = new TagEffect(grant: false, tag: new SkillTag("state.stunned"));
            effect.Execute(_source, _target);
            Assert.IsFalse(_target.Tags.HasTag(new SkillTag("state.stunned")));
        }
    }
}
