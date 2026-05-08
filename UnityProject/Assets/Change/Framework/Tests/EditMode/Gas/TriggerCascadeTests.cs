using System;
using Change.Framework.Gas;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public sealed class TriggerCascadeTests
    {
        [Test]
        public void BaseModifier_OnTick_ExpiresAfterDuration()
        {
            var modifier = new TestModifier("modifier.duration", 1f, ModifierStacking.Refresh);
            var target = CreateSystem();

            modifier.OnApply(target);
            modifier.OnTick(target, 0.25f);
            Assert.IsFalse(modifier.IsExpired);

            modifier.OnTick(target, 0.75f);

            Assert.IsTrue(modifier.IsExpired);
        }

        [Test]
        public void BaseModifier_AddStack_InvokesStackUpdateHook()
        {
            var modifier = new TestModifier("modifier.stack", 1f, ModifierStacking.AddStack);

            modifier.AddStack();
            modifier.AddStack();

            Assert.AreEqual(3, modifier.StackCount);
            Assert.AreEqual(2, modifier.StackUpdateCount);
        }

        [Test]
        public void BaseModifier_RefreshDuration_ClearsExpiration()
        {
            var modifier = new TestModifier("modifier.refresh", 0.5f, ModifierStacking.Refresh);
            var target = CreateSystem();

            modifier.OnApply(target);
            modifier.OnTick(target, 1f);
            Assert.IsTrue(modifier.IsExpired);

            modifier.RefreshDuration();
            Assert.IsFalse(modifier.IsExpired);
        }

        [Test]
        public void BaseTrigger_TryFire_CascadeDepthTooLarge_ReturnsFalse()
        {
            var trigger = new TestTrigger(TriggerEventType.OnAttack, TriggerScope.Self, 0f);
            var source = CreateSystem();
            var target = CreateSystem();

            var fired = trigger.TryFire(source, target, BaseTrigger.MaxCascadeDepth + 1);

            Assert.IsFalse(fired);
            Assert.AreEqual(0, trigger.ExecuteCount);
        }

        [Test]
        public void BaseTrigger_TryFire_WhenConditionTrueAndNotCoolingDown_ExecutesAndSetsCooldown()
        {
            var trigger = new TestTrigger(TriggerEventType.OnAttack, TriggerScope.Target, 2f);
            var source = CreateSystem();
            var target = CreateSystem();

            var firstFire = trigger.TryFire(source, target, 0);
            var secondFire = trigger.TryFire(source, target, 0);
            trigger.TickCooldown(2f);
            var thirdFire = trigger.TryFire(source, target, 0);

            Assert.IsTrue(firstFire);
            Assert.IsFalse(secondFire);
            Assert.IsTrue(thirdFire);
            Assert.AreEqual(2, trigger.ExecuteCount);
        }

        [Test]
        public void BaseTrigger_TryFire_WhenConditionFalse_ReturnsFalse()
        {
            var trigger = new FalseConditionTrigger(TriggerEventType.OnAttack, TriggerScope.Self, 0f);
            var source = CreateSystem();
            var target = CreateSystem();

            var fired = trigger.TryFire(source, target, 0);

            Assert.IsFalse(fired);
            Assert.AreEqual(0, trigger.ExecuteCount);
        }

        private static IAbilitySystem CreateSystem()
        {
            return new DefaultAbilitySystem("entity-1", 1, new DefaultAttributeSet(), new DefaultGameplayTagSet());
        }

        private sealed class TestModifier : BaseModifier
        {
            public TestModifier(string id, float duration, ModifierStacking stackingRule)
                : base(id, ModifierPolarity.Neutral, duration, stackingRule, Array.Empty<GameplayTag>())
            {
            }

            public int StackUpdateCount { get; private set; }

            protected override void OnStackCountChanged()
            {
                StackUpdateCount++;
            }
        }

        private class TestTrigger : BaseTrigger
        {
            public TestTrigger(TriggerEventType eventType, TriggerScope scope, float cooldownSeconds)
                : base(eventType, scope, cooldownSeconds)
            {
            }

            public int ExecuteCount { get; private set; }

            protected override void Execute(IAbilitySystem source, IAbilitySystem target, int cascadeDepth)
            {
                ExecuteCount++;
            }
        }

        private sealed class FalseConditionTrigger : TestTrigger
        {
            public FalseConditionTrigger(TriggerEventType eventType, TriggerScope scope, float cooldownSeconds)
                : base(eventType, scope, cooldownSeconds)
            {
            }

            protected override bool Condition(IAbilitySystem source, IAbilitySystem target)
            {
                return false;
            }
        }
    }
}
