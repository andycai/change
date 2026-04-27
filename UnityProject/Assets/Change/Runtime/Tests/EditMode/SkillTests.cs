using System;
using Change.Framework.Skill;
using Change.Runtime.Skill;
using Change.Runtime.Skill.Effects;
using NUnit.Framework;

namespace Change.Runtime.Tests
{
    public class SkillTests
    {
        private AbilitySystem _source;
        private IAbilitySystem[] _targets;

        [SetUp]
        public void SetUp()
        {
            _source = new AbilitySystem("hero");
            _source.Attributes.SetBaseValue("HP", 100f);
            _source.Attributes.SetBaseValue("ATK", 100f);
            _source.Attributes.SetBaseValue("Mana", 100f);

            var enemy = new AbilitySystem("enemy");
            enemy.Attributes.SetBaseValue("HP", 200f);
            _targets = new IAbilitySystem[] { enemy };
        }

        [Test]
        public void Activate_ExecutesEffectsOnTargets()
        {
            var skill = new Skill(new SkillConfig
            {
                Id = "fireball",
                Type = SkillType.Active,
                Activation = ActivationType.Manual,
                Tags = new[] { new SkillTag("skill.fire") },
                CostAttribute = null,
                CostAmount = 0,
                CooldownDuration = 0f,
                MaxCharges = 1,
                Effects = new ISkillEffect[]
                {
                    new DamageEffect(flatAmount: 50f, scalingFormula: null, scalingAttribute: null)
                }
            });

            Assert.IsTrue(skill.CanActivate(_source));
            skill.Activate(_source, _targets);
            Assert.AreEqual(150f, _targets[0].Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void Activate_InsufficientResource_ReturnsFalse()
        {
            _source.Attributes.SetBaseValue("Mana", 10f);
            var skill = new Skill(new SkillConfig
            {
                Id = "expensive",
                Type = SkillType.Active,
                Activation = ActivationType.Manual,
                Tags = new SkillTag[0],
                CostAttribute = "Mana",
                CostAmount = 50,
                CooldownDuration = 0f,
                MaxCharges = 1,
                Effects = Array.Empty<ISkillEffect>()
            });

            Assert.IsFalse(skill.CanActivate(_source));
        }

        [Test]
        public void Cooldown_PreventsReactivation()
        {
            var skill = new Skill(new SkillConfig
            {
                Id = "cooldown_skill",
                Type = SkillType.Active,
                Activation = ActivationType.Manual,
                Tags = new SkillTag[0],
                CostAttribute = null,
                CostAmount = 0,
                CooldownDuration = 5f,
                MaxCharges = 1,
                Effects = Array.Empty<ISkillEffect>()
            });

            skill.Activate(_source, _targets);
            Assert.IsFalse(skill.CanActivate(_source));

            skill.TickCooldown(5f);
            Assert.IsTrue(skill.CanActivate(_source));
        }

        [Test]
        public void Charges_AllowMultipleUsesBeforeCooldown()
        {
            var skill = new Skill(new SkillConfig
            {
                Id = "charge_skill",
                Type = SkillType.Active,
                Activation = ActivationType.Manual,
                Tags = new SkillTag[0],
                CostAttribute = null,
                CostAmount = 0,
                CooldownDuration = 10f,
                MaxCharges = 2,
                Effects = new ISkillEffect[]
                {
                    new DamageEffect(flatAmount: 10f, scalingFormula: null, scalingAttribute: null)
                }
            });

            Assert.AreEqual(2, skill.CurrentCharges);
            skill.Activate(_source, _targets);
            Assert.AreEqual(1, skill.CurrentCharges);
            Assert.IsTrue(skill.CanActivate(_source));
            skill.Activate(_source, _targets);
            Assert.AreEqual(0, skill.CurrentCharges);
            Assert.IsFalse(skill.CanActivate(_source));
        }
    }
}
