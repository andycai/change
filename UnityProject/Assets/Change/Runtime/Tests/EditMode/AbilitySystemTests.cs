using Change.Framework.Gas;
using Change.Runtime.Gas;
using Change.Runtime.Gas.Effects;
using NUnit.Framework;

using SkillInstance = Change.Runtime.Gas.Skill;

namespace Change.Runtime.Tests
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
        public void AddSkill_ThenGetSkill_ReturnsSkill()
        {
            var skill = new SkillInstance(new SkillConfig
            {
                Id = "attack",
                Type = SkillType.Active,
                Activation = ActivationType.Auto,
                Tags = new SkillTag[0],
                CostAttribute = null,
                CostAmount = 0,
                CooldownDuration = 1f,
                MaxCharges = 1,
                Effects = new ISkillEffect[]
                {
                    new DamageEffect(flatAmount: 30f, scalingFormula: null, scalingAttribute: null)
                }
            });

            _entity.AddSkill(skill);
            var retrieved = _entity.GetSkill("attack");
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
                GrantedTags = new[] { new SkillTag("state.shielded") },
                Stacking = ModifierStacking.Replace,
                MaxStack = 1,
                Duration = 5f
            });

            _entity.AddModifier(mod);
            Assert.IsTrue(_entity.Tags.HasTag(new SkillTag("state.shielded")));
        }

        [Test]
        public void RemoveModifier_RevokesTags()
        {
            var mod = new Modifier(new ModifierConfig
            {
                Id = "shield",
                Polarity = ModifierPolarity.Buff,
                GrantedTags = new[] { new SkillTag("state.shielded") },
                Stacking = ModifierStacking.Replace,
                MaxStack = 1,
                Duration = 5f
            });

            _entity.AddModifier(mod);
            _entity.RemoveModifier("shield");
            Assert.IsFalse(_entity.Tags.HasTag(new SkillTag("state.shielded")));
        }

        [Test]
        public void TickModifiers_ExpiredModifiersRemoved()
        {
            var mod = new Modifier(new ModifierConfig
            {
                Id = "short",
                Polarity = ModifierPolarity.Buff,
                GrantedTags = new[] { new SkillTag("buff.short") },
                Stacking = ModifierStacking.Replace,
                MaxStack = 1,
                Duration = 2f
            });

            _entity.AddModifier(mod);
            _entity.TickModifiers(3f);
            Assert.IsFalse(_entity.Tags.HasTag(new SkillTag("buff.short")));
        }

        [Test]
        public void TickSkills_AdvancesCooldowns()
        {
            var skill = new SkillInstance(new SkillConfig
            {
                Id = "test",
                Type = SkillType.Active,
                Activation = ActivationType.Manual,
                Tags = new SkillTag[0],
                CostAttribute = null,
                CostAmount = 0,
                CooldownDuration = 3f,
                MaxCharges = 1,
                Effects = System.Array.Empty<ISkillEffect>()
            });

            _entity.AddSkill(skill);
            skill.Activate(_entity, new IAbilitySystem[0]);
            Assert.IsFalse(skill.CanActivate(_entity));

            _entity.TickSkills(3f);
            Assert.IsTrue(skill.CanActivate(_entity));
        }
    }
}
