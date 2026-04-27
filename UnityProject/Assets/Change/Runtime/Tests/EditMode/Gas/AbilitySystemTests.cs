using Change.Framework.Gas;
using Change.Runtime.Gas;
using Change.Runtime.Gas.Effects;
using NUnit.Framework;

namespace Change.Runtime.Tests.Gas
{
    public class AbilitySystemTests
    {
        private AbilitySystem _entity;

        [SetUp]
        public void SetUp()
        {
            _entity = new AbilitySystem("hero1");
            _entity.Attributes.SetBaseValue("HP", 200f);
            _entity.Attributes.SetBaseValue("ATK", 100f);
        }

        [Test]
        public void AddAbility_ThenGetAbility_ReturnsAbility()
        {
            var ability = new GameplayAbility(new AbilityConfig
            {
                Id = "attack",
                Type = AbilityType.Active,
                Activation = ActivationType.Auto,
                Tags = new GameplayTag[0],
                CostAttribute = null,
                CostAmount = 0,
                CooldownDuration = 1f,
                MaxCharges = 1,
                Effects = new IGameplayEffect[]
                {
                    new DamageEffect(flatAmount: 30f, scalingFormula: null, scalingAttribute: null)
                }
            });

            _entity.AddAbility(ability);
            var retrieved = _entity.GetAbility("attack");
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("attack", retrieved.Id);
        }

        [Test]
        public void AddModifier_GrantsTagsAndAppliesEffects()
        {
            var mod = new Modifier(new ModifierConfig
            {
                Id = "shield",
                Polarity = ModifierPolarity.Buff,
                GrantedTags = new[] { new GameplayTag("state.shielded") },
                Stacking = ModifierStacking.Replace,
                MaxStack = 1,
                Duration = 5f
            });

            _entity.AddModifier(mod);
            Assert.IsTrue(_entity.Tags.HasTag(new GameplayTag("state.shielded")));
        }

        [Test]
        public void RemoveModifier_RevokesTags()
        {
            var mod = new Modifier(new ModifierConfig
            {
                Id = "shield",
                Polarity = ModifierPolarity.Buff,
                GrantedTags = new[] { new GameplayTag("state.shielded") },
                Stacking = ModifierStacking.Replace,
                MaxStack = 1,
                Duration = 5f
            });

            _entity.AddModifier(mod);
            _entity.RemoveModifier("shield");
            Assert.IsFalse(_entity.Tags.HasTag(new GameplayTag("state.shielded")));
        }

        [Test]
        public void Tick_ExpiredModifiersRemoved()
        {
            var mod = new Modifier(new ModifierConfig
            {
                Id = "short",
                Polarity = ModifierPolarity.Buff,
                GrantedTags = new[] { new GameplayTag("buff.short") },
                Stacking = ModifierStacking.Replace,
                MaxStack = 1,
                Duration = 2f
            });

            _entity.AddModifier(mod);
            _entity.Tick(3f);
            Assert.IsFalse(_entity.Tags.HasTag(new GameplayTag("buff.short")));
        }

        [Test]
        public void Tick_AdvancesCooldowns()
        {
            var ability = new GameplayAbility(new AbilityConfig
            {
                Id = "test",
                Type = AbilityType.Active,
                Activation = ActivationType.Manual,
                Tags = new GameplayTag[0],
                CostAttribute = null,
                CostAmount = 0,
                CooldownDuration = 3f,
                MaxCharges = 1,
                Effects = System.Array.Empty<IGameplayEffect>()
            });

            _entity.AddAbility(ability);
            ability.Activate(_entity, new IAbilitySystem[0]);
            Assert.IsFalse(ability.CanActivate(_entity));

            _entity.Tick(3f);
            Assert.IsTrue(ability.CanActivate(_entity));
        }
    }
}
