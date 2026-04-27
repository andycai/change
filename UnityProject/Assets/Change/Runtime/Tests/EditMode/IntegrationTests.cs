using Change.Framework.Skill;
using Change.Runtime.Skill;
using Change.Runtime.Skill.Effects;
using NUnit.Framework;

using SkillInstance = Change.Runtime.Skill.Skill;

namespace Change.Runtime.Tests
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
                GrantedTags = new[] { new SkillTag("state.burning") },
                Stacking = ModifierStacking.Refresh,
                MaxStack = 1,
                Duration = 5f,
                TickInterval = 1f,
                TickEffects = new ISkillEffect[]
                {
                    new DamageEffect(flatAmount: 30f, scalingFormula: null, scalingAttribute: null)
                }
            };

            // Create fireball skill
            var fireball = new SkillInstance(new SkillConfig
            {
                Id = "fireball",
                Type = SkillType.Active,
                Activation = ActivationType.Manual,
                Tags = new[] { new SkillTag("skill.fire") },
                CostAttribute = "Mana",
                CostAmount = 30,
                CooldownDuration = 3f,
                MaxCharges = 1,
                Effects = new ISkillEffect[]
                {
                    new DamageEffect(flatAmount: 100f, scalingFormula: null, scalingAttribute: "ATK", scalingMultiplier: 1.5f),
                    new ChanceEffect(rate: 1.0f, onSuccess: new ISkillEffect[]
                    {
                        new ApplyModifierEffect("ignite")
                    }, onFailure: null)
                }
            });

            hero.AddSkill(fireball);

            // Cast fireball
            var targets = new IAbilitySystem[] { enemy };
            Assert.IsTrue(fireball.CanActivate(hero));
            fireball.Activate(hero, targets);

            // Verify mana cost
            Assert.AreEqual(70f, hero.Attributes.GetCurrentValue("Mana"));

            // Verify damage: 100 + 100*1.5 = 250 damage
            Assert.AreEqual(50f, enemy.Attributes.GetCurrentValue("HP"));

            // Simulate 3 ticks of ignite
            enemy.AddModifier(new Modifier(igniteConfig));
            Assert.IsTrue(enemy.Tags.HasTag(new SkillTag("state.burning")));

            enemy.TickModifiers(1f); // tick 1: 30 damage
            Assert.AreEqual(20f, enemy.Attributes.GetCurrentValue("HP"));

            enemy.TickModifiers(1f); // tick 2: 30 damage → HP goes to -10
            Assert.AreEqual(-10f, enemy.Attributes.GetCurrentValue("HP"));

            // After 5 seconds total, ignite expires
            enemy.TickModifiers(3f);
            Assert.IsFalse(enemy.Tags.HasTag(new SkillTag("state.burning")));
        }

        [Test]
        public void FullScenario_BuffEnhancesDamage()
        {
            var hero = new AbilitySystem("hero");
            hero.Attributes.SetBaseValue("ATK", 100f);

            var enemy = new AbilitySystem("enemy");
            enemy.Attributes.SetBaseValue("HP", 500f);

            // Apply rage buff: ATK +50%
            var rageAttr = (hero.Attributes as AttributeSet).GetOrCreateAttribute("ATK", 100f);
            rageAttr.AddMultiplicative(1.5f);
            rageAttr.Recalculate();
            Assert.AreEqual(150f, hero.Attributes.GetCurrentValue("ATK"));

            // Attack with buffed ATK
            var skill = new SkillInstance(new SkillConfig
            {
                Id = "slash",
                Type = SkillType.Active,
                Activation = ActivationType.Manual,
                Tags = new SkillTag[0],
                CostAttribute = null,
                CostAmount = 0,
                CooldownDuration = 0f,
                MaxCharges = 1,
                Effects = new ISkillEffect[]
                {
                    new DamageEffect(flatAmount: 0f, scalingFormula: null, scalingAttribute: "ATK", scalingMultiplier: 2f)
                }
            });

            skill.Activate(hero, new IAbilitySystem[] { enemy });
            // Damage = 0 + 150*2 = 300
            Assert.AreEqual(200f, enemy.Attributes.GetCurrentValue("HP"));
        }
    }
}
