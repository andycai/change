using Change.Framework.Gas;
using Change.Runtime.Gas;
using Change.Runtime.Gas.Effects;
using NUnit.Framework;

namespace Change.Runtime.Tests.Gas
{
    public class ModifierTests
    {
        private AbilitySystem _target;

        [SetUp]
        public void SetUp()
        {
            _target = new AbilitySystem("target");
            _target.Attributes.SetBaseValue("HP", 200f);
            _target.Attributes.SetBaseValue("ATK", 100f);
        }

        [Test]
        public void OnApply_GrantsTags()
        {
            var mod = new Modifier(new ModifierConfig
            {
                Id = "stun",
                Polarity = ModifierPolarity.Debuff,
                GrantedTags = new[] { new GameplayTag("state.stunned") },
                Stacking = ModifierStacking.Replace,
                MaxStack = 1
            });
            mod.OnApply(_target);
            Assert.IsTrue(_target.Tags.HasTag(new GameplayTag("state.stunned")));
        }

        [Test]
        public void OnRemove_RevokesTags()
        {
            var mod = new Modifier(new ModifierConfig
            {
                Id = "stun",
                Polarity = ModifierPolarity.Debuff,
                GrantedTags = new[] { new GameplayTag("state.stunned") },
                Stacking = ModifierStacking.Replace,
                MaxStack = 1
            });
            mod.OnApply(_target);
            mod.OnRemove(_target);
            Assert.IsFalse(_target.Tags.HasTag(new GameplayTag("state.stunned")));
        }

        [Test]
        public void OnTick_ExecutesPeriodicEffects()
        {
            var mod = new Modifier(new ModifierConfig
            {
                Id = "ignite",
                Polarity = ModifierPolarity.Debuff,
                GrantedTags = new GameplayTag[0],
                Stacking = ModifierStacking.Refresh,
                MaxStack = 1,
                TickInterval = 1f,
                TickEffects = new IGameplayEffect[]
                {
                    new DamageEffect(flatAmount: 20f, scalingAttribute: null)
                }
            });
            mod.OnApply(_target);
            mod.OnTick(_target, 1f);
            Assert.AreEqual(180f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void IsExpired_TrueWhenDurationExpires()
        {
            var mod = new Modifier(new ModifierConfig
            {
                Id = "short_buff",
                Polarity = ModifierPolarity.Buff,
                GrantedTags = new GameplayTag[0],
                Stacking = ModifierStacking.Replace,
                MaxStack = 1,
                Duration = 2f
            });
            mod.OnApply(_target);
            Assert.IsFalse(mod.IsExpired);
            mod.OnTick(_target, 2f);
            Assert.IsTrue(mod.IsExpired);
        }

        [Test]
        public void StackCount_IncreasesWithAddStack()
        {
            var mod = new Modifier(new ModifierConfig
            {
                Id = "poison",
                Polarity = ModifierPolarity.Debuff,
                GrantedTags = new GameplayTag[0],
                Stacking = ModifierStacking.AddStack,
                MaxStack = 3,
                Duration = 5f
            });
            mod.OnApply(_target);
            Assert.AreEqual(1, mod.StackCount);
            mod.AddStack();
            Assert.AreEqual(2, mod.StackCount);
            mod.AddStack();
            Assert.AreEqual(3, mod.StackCount);
            mod.AddStack();
            Assert.AreEqual(3, mod.StackCount);
        }
    }
}
