using System;
using Change.Framework.Gas;
using Change.Runtime.Gas;
using Change.Runtime.Gas.Effects;
using NUnit.Framework;

namespace Change.Runtime.Tests.Gas
{
    public class GameplayAbilityTests
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
            var ability = new GameplayAbility(new AbilityConfig
            {
                Id = "fireball",
                Type = AbilityType.Active,
                Activation = ActivationType.Manual,
                Tags = new[] { new GameplayTag("skill.fire") },
                CostAttribute = null,
                CostAmount = 0,
                CooldownDuration = 0f,
                MaxCharges = 1,
                Effects = new IGameplayEffect[]
                {
                    new DamageEffect(flatAmount: 50f, scalingFormula: null, scalingAttribute: null)
                }
            });

            Assert.IsTrue(ability.CanActivate(_source));
            ability.Activate(_source, _targets);
            Assert.AreEqual(150f, _targets[0].Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void Activate_WithCastTime_ExecutesAfterDelay()
        {
            var ability = new GameplayAbility(new AbilityConfig
            {
                Id = "delayed",
                Type = AbilityType.Active,
                Activation = ActivationType.Manual,
                Tags = new GameplayTag[0],
                CastTime = 2f,
                Effects = new IGameplayEffect[]
                {
                    new DamageEffect(flatAmount: 50f)
                }
            });

            ability.Activate(_source, _targets);
            Assert.AreEqual(AbilityState.Casting, ability.State);
            Assert.AreEqual(200f, _targets[0].Attributes.GetCurrentValue("HP"));

            ability.Tick(2f);
            Assert.AreEqual(150f, _targets[0].Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void Activate_InsufficientResource_ReturnsFalse()
        {
            _source.Attributes.SetBaseValue("Mana", 10f);
            var ability = new GameplayAbility(new AbilityConfig
            {
                Id = "expensive",
                Type = AbilityType.Active,
                Activation = ActivationType.Manual,
                Tags = new GameplayTag[0],
                CostAttribute = "Mana",
                CostAmount = 50,
                CooldownDuration = 0f,
                MaxCharges = 1,
                Effects = Array.Empty<IGameplayEffect>()
            });

            Assert.IsFalse(ability.CanActivate(_source));
        }

        [Test]
        public void Cooldown_PreventsReactivation()
        {
            var ability = new GameplayAbility(new AbilityConfig
            {
                Id = "cooldown_skill",
                Type = AbilityType.Active,
                Activation = ActivationType.Manual,
                Tags = new GameplayTag[0],
                CostAttribute = null,
                CostAmount = 0,
                CooldownDuration = 5f,
                MaxCharges = 1,
                Effects = Array.Empty<IGameplayEffect>()
            });

            ability.Activate(_source, _targets);
            Assert.IsFalse(ability.CanActivate(_source));

            ability.Tick(5f);
            Assert.IsTrue(ability.CanActivate(_source));
        }

        [Test]
        public void Charges_AllowMultipleUsesBeforeCooldown()
        {
            var ability = new GameplayAbility(new AbilityConfig
            {
                Id = "charge_skill",
                Type = AbilityType.Active,
                Activation = ActivationType.Manual,
                Tags = new GameplayTag[0],
                CostAttribute = null,
                CostAmount = 0,
                CooldownDuration = 10f,
                MaxCharges = 2,
                Effects = new IGameplayEffect[]
                {
                    new DamageEffect(flatAmount: 10f, scalingFormula: null, scalingAttribute: null)
                }
            });

            Assert.AreEqual(2, ability.CurrentCharges);
            ability.Activate(_source, _targets);
            Assert.AreEqual(1, ability.CurrentCharges);
            Assert.IsTrue(ability.CanActivate(_source));
            ability.Activate(_source, _targets);
            Assert.AreEqual(0, ability.CurrentCharges);
            Assert.IsFalse(ability.CanActivate(_source));
        }
    }
}
