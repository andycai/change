using System;
using System.Collections.Generic;
using Change.Framework.Gas;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class DefaultAbilitySystemTests
    {
        [Test]
        public void DefaultAttribute_ConstructsWithBaseAndCurrentValue()
        {
            var attribute = new DefaultAttribute("Health", 100f);

            Assert.AreEqual("Health", attribute.Name);
            Assert.AreEqual(100f, attribute.BaseValue);
            Assert.AreEqual(100f, attribute.CurrentValue);
        }

        [Test]
        public void DefaultAttribute_Recalculate_AppliesAdditiveAndMultiplicativeModifiers()
        {
            var attribute = new DefaultAttribute("Attack", 10f);

            attribute.AddAdditive(5f);
            attribute.AddMultiplicative(0.5f);
            attribute.Recalculate();

            Assert.AreEqual(22.5f, attribute.CurrentValue);
        }

        [Test]
        public void DefaultAttribute_EmptyName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new DefaultAttribute(string.Empty, 1f));
        }

        [Test]
        public void DefaultAttributeSet_GetCurrentValue_UsesRegisteredAttribute()
        {
            var set = new DefaultAttributeSet(new DefaultAttribute("Health", 50f));

            Assert.AreEqual(50f, set.GetCurrentValue("Health"));
        }

        [Test]
        public void DefaultAttributeSet_SetBaseValue_UpdatesAttributeAndRaisesEvent()
        {
            var set = new DefaultAttributeSet(new DefaultAttribute("Health", 50f));
            var eventRaised = false;
            var oldValue = 0f;
            var newValue = 0f;

            set.OnAttributeChanged += (name, oldCurrent, newCurrent) =>
            {
                if (name != "Health")
                {
                    return;
                }

                eventRaised = true;
                oldValue = oldCurrent;
                newValue = newCurrent;
            };

            set.SetBaseValue("Health", 80f);

            Assert.IsTrue(eventRaised);
            Assert.AreEqual(50f, oldValue);
            Assert.AreEqual(80f, newValue);
        }

        [Test]
        public void DefaultAttributeSet_GetAttribute_MissingAttribute_ThrowsInvalidOperationException()
        {
            var set = new DefaultAttributeSet(new DefaultAttribute("Health", 50f));

            Assert.Throws<InvalidOperationException>(() => set.GetAttribute("Missing"));
        }

        [Test]
        public void DefaultGameplayTagSet_AddAndRemoveTag_TracksCounts()
        {
            var tags = new DefaultGameplayTagSet();
            var burning = new GameplayTag("State.Burning");

            tags.AddTag(burning);
            tags.AddTag(burning);
            tags.RemoveTag(burning);

            Assert.IsTrue(tags.HasTag(burning));
            Assert.AreEqual(1, tags.GetTagCount(burning));
        }

        [Test]
        public void DefaultGameplayTagSet_RemoveMissingTag_ThrowsInvalidOperationException()
        {
            var tags = new DefaultGameplayTagSet();

            Assert.Throws<InvalidOperationException>(() => tags.RemoveTag(new GameplayTag("State.Frozen")));
        }

        [Test]
        public void DefaultAbilitySystem_Tick_AdvancesAbilitiesModifiersAndTriggerCooldowns()
        {
            var ability = new TestAbility("ability.tick");
            var modifier = new TestModifier("modifier.tick");
            var trigger = new TestTrigger(TriggerEventType.OnAttack);
            var system = new DefaultAbilitySystem("entity-1", 1, new DefaultAttributeSet(), new DefaultGameplayTagSet());

            system.AddAbility(ability);
            system.AddModifier(modifier);
            system.AddTrigger(trigger);

            system.Tick(0.5f);

            Assert.AreEqual(1, ability.TickCallCount);
            Assert.AreEqual(0.5f, ability.LastDeltaTime);
            Assert.AreEqual(1, modifier.TickCallCount);
            Assert.AreSame(system, modifier.LastTarget);
            Assert.AreEqual(0.5f, modifier.LastDeltaTime);
            Assert.AreEqual(1, trigger.TickCooldownCallCount);
            Assert.AreEqual(0.5f, trigger.LastDeltaTime);
        }

        [Test]
        public void DefaultAbilitySystem_AddAbility_DuplicateId_ThrowsInvalidOperationException()
        {
            var system = new DefaultAbilitySystem("entity-1", 1, new DefaultAttributeSet(), new DefaultGameplayTagSet());

            system.AddAbility(new TestAbility("ability.duplicate"));

            Assert.Throws<InvalidOperationException>(() => system.AddAbility(new TestAbility("ability.duplicate")));
        }

        private sealed class TestAbility : IGameplayAbility
        {
            public TestAbility(string id)
            {
                Id = id;
                Tags = Array.Empty<GameplayTag>();
            }

            public int TickCallCount { get; private set; }
            public float LastDeltaTime { get; private set; }
            public string Id { get; }
            public AbilityType Type => AbilityType.Active;
            public ActivationType Activation => ActivationType.Manual;
            public AbilityState State => AbilityState.Ready;
            public GameplayTag[] Tags { get; }
            public float CooldownDuration => 0f;
            public int MaxCharges => 0;
            public int CurrentCharges => 0;
            public event Action<AbilityState> OnStateChange;

            public bool CanActivate(IAbilitySystem source)
            {
                return true;
            }

            public void Activate(IAbilitySystem source, IAbilitySystem[] targets)
            {
            }

            public void Tick(float deltaTime)
            {
                TickCallCount++;
                LastDeltaTime = deltaTime;
            }
        }

        private sealed class TestModifier : IModifier
        {
            public TestModifier(string id)
            {
                Id = id;
                GrantedTags = Array.Empty<GameplayTag>();
            }

            public int TickCallCount { get; private set; }
            public IAbilitySystem LastTarget { get; private set; }
            public float LastDeltaTime { get; private set; }
            public string Id { get; }
            public ModifierPolarity Polarity => ModifierPolarity.Neutral;
            public GameplayTag[] GrantedTags { get; }
            public int StackCount => 1;
            public bool IsExpired => false;
            public ModifierStacking StackingRule => ModifierStacking.Refresh;

            public void OnApply(IAbilitySystem target)
            {
            }

            public void OnTick(IAbilitySystem target, float deltaTime)
            {
                TickCallCount++;
                LastTarget = target;
                LastDeltaTime = deltaTime;
            }

            public void OnRemove(IAbilitySystem target)
            {
            }

            public void AddStack()
            {
            }

            public void RefreshDuration()
            {
            }
        }

        private sealed class TestTrigger : ITrigger
        {
            public TestTrigger(TriggerEventType eventType)
            {
                EventType = eventType;
            }

            public int TickCooldownCallCount { get; private set; }
            public float LastDeltaTime { get; private set; }
            public TriggerEventType EventType { get; }
            public TriggerScope Scope => TriggerScope.Self;

            public bool EvaluateCondition(IAbilitySystem source, IAbilitySystem target)
            {
                return true;
            }

            public void ExecuteEffects(IAbilitySystem source, IAbilitySystem target, int cascadeDepth)
            {
            }

            public bool TryFire(IAbilitySystem source, IAbilitySystem target, int cascadeDepth)
            {
                return true;
            }

            public void TickCooldown(float deltaTime)
            {
                TickCooldownCallCount++;
                LastDeltaTime = deltaTime;
            }
        }
    }
}
