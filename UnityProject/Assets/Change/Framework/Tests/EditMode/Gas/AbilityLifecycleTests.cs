using System;
using Change.Framework.Gas;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public sealed class AbilityLifecycleTests
    {
        private static readonly IAbilitySystem[] EmptyTargets = Array.Empty<IAbilitySystem>();

        [Test]
        public void ActiveAbility_ActivateAndTick_TransitionsThroughCooldownAndRestoresCharges()
        {
            var ability = new TestActiveAbility("ability.active", 2f, 2);
            var source = new DefaultAbilitySystem("entity-1", 1, new DefaultAttributeSet(), new DefaultGameplayTagSet());

            Assert.AreEqual(AbilityState.Ready, ability.State);
            Assert.AreEqual(2, ability.CurrentCharges);

            ability.Activate(source, EmptyTargets);
            Assert.AreEqual(AbilityState.Ready, ability.State);
            Assert.AreEqual(1, ability.CurrentCharges);

            ability.Activate(source, EmptyTargets);
            Assert.AreEqual(AbilityState.Cooldown, ability.State);
            Assert.AreEqual(0, ability.CurrentCharges);
            Assert.IsFalse(ability.CanActivate(source));

            ability.Tick(1f);
            Assert.AreEqual(AbilityState.Cooldown, ability.State);
            Assert.AreEqual(0, ability.CurrentCharges);

            ability.Tick(1f);
            Assert.AreEqual(AbilityState.Ready, ability.State);
            Assert.AreEqual(2, ability.CurrentCharges);
            Assert.IsTrue(ability.CanActivate(source));
        }

        [Test]
        public void ActiveAbility_ActivateWhenCannotActivate_ThrowsInvalidOperationException()
        {
            var ability = new TestActiveAbility("ability.invalid", 1f, 1);
            var source = new DefaultAbilitySystem("entity-1", 1, new DefaultAttributeSet(), new DefaultGameplayTagSet());

            ability.Activate(source, EmptyTargets);

            Assert.Throws<InvalidOperationException>(() => ability.Activate(source, EmptyTargets));
        }

        [Test]
        public void PassiveAbility_ManualActivate_ThrowsInvalidOperationException()
        {
            var ability = new PassiveGameplayAbility("ability.passive", ActivationType.OnEvent);
            var source = new DefaultAbilitySystem("entity-1", 1, new DefaultAttributeSet(), new DefaultGameplayTagSet());

            Assert.Throws<InvalidOperationException>(() => ability.Activate(source, EmptyTargets));
        }

        private sealed class TestActiveAbility : ActiveGameplayAbility
        {
            public TestActiveAbility(string id, float cooldownDuration, int maxCharges)
                : base(id, ActivationType.Manual, Array.Empty<GameplayTag>(), cooldownDuration, maxCharges)
            {
            }
        }
    }
}
