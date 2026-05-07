using System;
using Change.Framework.Gas;
using Change.Runtime.Gas;
using Change.Runtime.Gas.Effects;
using NUnit.Framework;

namespace Change.Runtime.Tests.Gas
{
    public class IntegrationTests
    {
        [Test]
        public void FullScenario_FireballWithIgnite()
        {
            // Setup hero
            var hero = new AbilitySystem("hero");
            hero.Attributes.SetBaseValue("HP", 100f);
            hero.Attributes.SetBaseValue("ATK", 100f);
            hero.Attributes.SetBaseValue("Mana", 100f);

            // Setup enemy
            var enemy = new AbilitySystem("enemy");
            enemy.Attributes.SetBaseValue("HP", 300f);
            enemy.Attributes.SetBaseValue("DEF", 0f);

            // Create Ignite modifier config
            var igniteConfig = new ModifierConfig
            {
                Id = "ignite",
                Polarity = ModifierPolarity.Debuff,
                GrantedTags = new[] { new GameplayTag("state.burning") },
                Stacking = ModifierStacking.Refresh,
                MaxStack = 1,
                Duration = 5f,
                TickInterval = 1f,
                TickEffects = new IGameplayEffect[]
                {
                    new DamageEffect(flatAmount: 30f, scalingAttribute: null)
                }
            };

            // Create fireball ability
            var fireball = new GameplayAbility(new AbilityConfig
            {
                Id = "fireball",
                Type = AbilityType.Active,
                Activation = ActivationType.Manual,
                Tags = new[] { new GameplayTag("skill.fire") },
                CostAttribute = "Mana",
                CostAmount = 30,
                CooldownDuration = 3f,
                MaxCharges = 1,
                Effects = new IGameplayEffect[]
                {
                    new DamageEffect(flatAmount: 100f, scalingAttribute: "ATK", scalingMultiplier: 1.5f),
                    new ChanceEffect(chance: 1.0f, effect: new ApplyModifierEffect(() => new Modifier(igniteConfig)))
                }
            });

            hero.AddAbility(fireball);

            // Cast fireball
            var targets = new IAbilitySystem[] { enemy };
            Assert.IsTrue(fireball.CanActivate(hero));
            fireball.Activate(hero, targets);

            // Verify mana cost
            Assert.AreEqual(70f, hero.Attributes.GetCurrentValue("Mana"));

            // Verify damage: 100 + 100*1.5 = 250 damage
            // Recalculate is automatic
            Assert.AreEqual(50f, enemy.Attributes.GetCurrentValue("HP"));

            // Ignite is applied during fireball.Activate because ChanceEffect(chance: 1.0f)
            Assert.IsTrue(enemy.Tags.HasTag(new GameplayTag("state.burning")));

            // Tick modifiers: fireball applied one ignite.
            // Wait, fireball.Activate already executes effects.
            // Let's check enemy HP after ignite ticks.
            
            enemy.Tick(1f); // tick 1: 30 damage
            Assert.AreEqual(20f, enemy.Attributes.GetCurrentValue("HP"));

            enemy.Tick(1f); // tick 2: 30 damage → HP goes to -10
            Assert.AreEqual(-10f, enemy.Attributes.GetCurrentValue("HP"));

            // After 5 seconds total, ignite expires
            enemy.Tick(3f);
            Assert.IsFalse(enemy.Tags.HasTag(new GameplayTag("state.burning")));
        }

        [Test]
        public void FullScenario_BuffEnhancesDamage()
        {
            var hero = new AbilitySystem("hero");
            hero.Attributes.SetBaseValue("ATK", 100f);

            var enemy = new AbilitySystem("enemy");
            enemy.Attributes.SetBaseValue("HP", 500f);

            // Apply rage buff: ATK +50%
            var atkAttr = hero.Attributes.GetAttribute("ATK");
            atkAttr.AddMultiplicative(1.5f);
            Assert.AreEqual(150f, hero.Attributes.GetCurrentValue("ATK"));

            // Attack with buffed ATK
            var ability = new GameplayAbility(new AbilityConfig
            {
                Id = "slash",
                Type = AbilityType.Active,
                Activation = ActivationType.Manual,
                Tags = new GameplayTag[0],
                CostAttribute = null,
                CostAmount = 0,
                CooldownDuration = 0f,
                MaxCharges = 1,
                Effects = new IGameplayEffect[]
                {
                    new DamageEffect(flatAmount: 0f, scalingAttribute: "ATK", scalingMultiplier: 2f)
                }
            });

            ability.Activate(hero, new IAbilitySystem[] { enemy });
            // Damage = 0 + 150*2 = 300
            Assert.AreEqual(200f, enemy.Attributes.GetCurrentValue("HP"));
        }
    }
}
