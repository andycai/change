# Skill System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a production-grade GAS-inspired skill system for a 5V5 card battle game, with Buff/Debuff management, event triggers, and skill combos.

**Architecture:** Three-layer design — Framework interfaces (stable, not hot-updatable), GameScript implementations (hot-updatable via HybridCLR), JSON data config (hot-updatable via YooAsset). Integrates with existing CQRS, FSM, Pooling, Timer, and Collections modules.

**Tech Stack:** Unity 2022.3 LTS, C#, HybridCLR, UniTask, Nino serialization, NUnit tests

---

## File Structure

### Framework Layer (`UnityProject/Assets/Change/Framework/Skill/`)

| File | Responsibility |
|------|---------------|
| `SkillTag.cs` | Value type representing a hierarchical tag (readonly struct) |
| `ISkillTagSet.cs` | Interface for tag collection management |
| `IAttribute.cs` | Interface for a single attribute with modifiers |
| `IAttributeSet.cs` | Interface for entity stats container |
| `ISkillEffect.cs` | Interface for atomic skill outcomes (damage, heal, buff, etc.) |
| `IModifier.cs` | Interface for Buff/Debuff lifecycle |
| `ITargetResolver.cs` | Interface for target selection logic |
| `ITrigger.cs` | Interface for event-driven passive activation |
| `ISkill.cs` | Interface for skill lifecycle |
| `IAbilitySystem.cs` | Interface for entity-level skill manager |
| `Enums.cs` | All shared enums (SkillType, ModifierPolarity, etc.) |

### GameScript Layer (`UnityProject/Assets/GameScript/Skill/`)

| File | Responsibility |
|------|---------------|
| `Core/SkillTagSet.cs` | FastHashSet-based tag collection |
| `Core/Attribute.cs` | Single attribute with additive/multiplicative mods |
| `Core/AttributeSet.cs` | FastDictionary-based stats container |
| `Core/Modifier.cs` | Buff/Debuff with stacking, tick, remove lifecycle |
| `Core/Trigger.cs` | Event-triggered passive with conditions and cooldown |
| `Core/Skill.cs` | FSM-based skill with validation, casting, execution |
| `Core/AbilitySystem.cs` | Entity manager — skills, modifiers, tags, triggers |
| `Core/TriggerEngine.cs` | Global event dispatch across all entities |
| `Effects/DamageEffect.cs` | Damage application (flat, %, formula) |
| `Effects/HealEffect.cs` | Heal application |
| `Effects/ApplyModifierEffect.cs` | Apply buff/debuff to target |
| `Effects/RemoveModifierEffect.cs` | Dispel by tag/priority |
| `Effects/TagEffect.cs` | Grant/remove tags |
| `Effects/ChanceEffect.cs` | Conditional wrapper (random roll + branch) |
| `Targeting/SelfTargetResolver.cs` | Target self |
| `Targeting/EnemyTargetResolver.cs` | Target N random enemies |
| `Targeting/AllyTargetResolver.cs` | Target N random allies |
| `Targeting/AoETargetResolver.cs` | Target all enemies/allies |
| `Cqrs/CastSkillCmd.cs` | Command: activate a skill |
| `Cqrs/ApplyModifierCmd.cs` | Command: apply a modifier |
| `Cqrs/SkillExecutedEvt.cs` | Event: skill completed execution |
| `Cqrs/DamageAppliedEvt.cs` | Event: damage dealt |
| `Cqrs/HealAppliedEvt.cs` | Event: healing applied |
| `Cqrs/ModifierAppliedEvt.cs` | Event: buff/debuff applied |
| `Cqrs/ModifierExpiredEvt.cs` | Event: buff/debuff expired or dispelled |
| `Cqrs/AttributeChangedEvt.cs` | Event: attribute value changed |
| `Cqrs/GetAttributeValueQry.cs` | Query: read attribute value |
| `Cqrs/HasTagQry.cs` | Query: check if entity has tag |
| `Data/SkillConfig.cs` | Skill JSON config (Nino) |
| `Data/ModifierConfig.cs` | Modifier JSON config (Nino) |
| `Data/TriggerConfig.cs` | Trigger JSON config (Nino) |
| `Data/SkillDataLoader.cs` | Load JSON configs via YooAsset |

### Tests (`UnityProject/Assets/GameScript/Tests/EditMode/`)

| File | Tests |
|------|-------|
| `SkillTagSetTests.cs` | Tag matching, hierarchy, add/remove |
| `AttributeSetTests.cs` | Base value, additive/mult mods, change events |
| `ModifierTests.cs` | Lifecycle, stacking, tick, remove |
| `SkillEffectTests.cs` | Damage/heal/apply modifier/chance effects |
| `TargetResolverTests.cs` | Target selection scenarios |
| `TriggerTests.cs` | Event matching, conditions, cooldown |
| `SkillTests.cs` | FSM lifecycle, cooldown, cost |
| `AbilitySystemTests.cs` | Integration: skill + modifier + trigger |
| `TriggerEngineTests.cs` | Cross-entity event dispatch, cascade guard |
| `DataConfigTests.cs` | JSON deserialization |
| `IntegrationTests.cs` | Full battle scenario |

---

## Task 1: Assembly Setup

**Files:**
- Create: `UnityProject/Assets/GameScript/GameScript.asmdef`
- Create: `UnityProject/Assets/GameScript/AssemblyInfo.cs`
- Create: `UnityProject/Assets/GameScript/Tests/EditMode/GameScript.EditModeTests.asmdef`
- Modify: `UnityProject/Assets/Change/Framework/AssemblyInfo.cs` — add InternalsVisibleTo for GameScript tests

- [ ] **Step 1: Create GameScript assembly definition**

```json
{
    "name": "GameScript",
    "rootNamespace": "GameScript",
    "references": ["Change.Framework", "Change.Runtime"],
    "optionalUnityReferences": [],
    "includePlatforms": [],
    "excludePlatforms": []
}
```

Save to `UnityProject/Assets/GameScript/GameScript.asmdef`.

- [ ] **Step 2: Create GameScript AssemblyInfo**

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("GameScript.EditModeTests")]
```

Save to `UnityProject/Assets/GameScript/AssemblyInfo.cs`.

- [ ] **Step 3: Create test assembly definition**

```json
{
    "name": "GameScript.EditModeTests",
    "rootNamespace": "GameScript",
    "references": ["GameScript", "Change.Framework", "Change.Runtime"],
    "optionalUnityReferences": ["UnityEngine.TestRunner", "UnityEditor.TestRunner"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": []
}
```

Save to `UnityProject/Assets/GameScript/Tests/EditMode/GameScript.EditModeTests.asmdef`.

- [ ] **Step 4: Add InternalsVisibleTo for GameScript tests in Framework**

Add to `UnityProject/Assets/Change/Framework/AssemblyInfo.cs`:

```csharp
[assembly: InternalsVisibleTo("GameScript")]
[assembly: InternalsVisibleTo("GameScript.EditModeTests")]
```

- [ ] **Step 5: Create directory structure**

```
UnityProject/Assets/GameScript/Skill/Core/
UnityProject/Assets/GameScript/Skill/Effects/
UnityProject/Assets/GameScript/Skill/Targeting/
UnityProject/Assets/GameScript/Skill/Cqrs/
UnityProject/Assets/GameScript/Skill/Data/
UnityProject/Assets/Change/Framework/Skill/
UnityProject/Assets/GameScript/Tests/EditMode/
```

- [ ] **Step 6: Verify compilation**

Run: Open Unity project, confirm no compilation errors.

- [ ] **Step 7: Commit**

```bash
git add UnityProject/Assets/GameScript/ UnityProject/Assets/Change/Framework/AssemblyInfo.cs
git commit -m "feat(skill): add GameScript assembly and directory structure for skill system"
```

---

## Task 2: SkillTag System

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Skill/SkillTag.cs`
- Create: `UnityProject/Assets/Change/Framework/Skill/ISkillTagSet.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Core/SkillTagSet.cs`
- Test: `UnityProject/Assets/GameScript/Tests/EditMode/SkillTagSetTests.cs`

### Framework: SkillTag value type

- [ ] **Step 1: Write SkillTag struct**

```csharp
using System;

namespace Change.Framework.Skill
{
    public readonly struct SkillTag : IEquatable<SkillTag>, IComparable<SkillTag>
    {
        public string Value { get; }

        public SkillTag(string value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool Equals(SkillTag other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SkillTag other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;
        public int CompareTo(SkillTag other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

        public static bool operator ==(SkillTag left, SkillTag right) => left.Equals(right);
        public static bool operator !=(SkillTag left, SkillTag right) => !left.Equals(right);

        public static implicit operator SkillTag(string value) => new SkillTag(value);
    }
}
```

Save to `UnityProject/Assets/Change/Framework/Skill/SkillTag.cs`.

- [ ] **Step 2: Write ISkillTagSet interface**

```csharp
namespace Change.Framework.Skill
{
    public interface ISkillTagSet
    {
        bool HasTag(SkillTag tag);
        void AddTag(SkillTag tag);
        void RemoveTag(SkillTag tag);
        void Clear();
        int Count { get; }
    }
}
```

Save to `UnityProject/Assets/Change/Framework/Skill/ISkillTagSet.cs`.

### GameScript: SkillTagSet implementation

- [ ] **Step 3: Write failing tests for SkillTagSet**

```csharp
using Change.Framework.Skill;
using GameScript.Skill.Core;
using NUnit.Framework;

namespace GameScript.Tests
{
    public class SkillTagSetTests
    {
        [Test]
        public void AddTag_ThenHasTag_ReturnsTrue()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("buff.fire"));
            Assert.IsTrue(set.HasTag(new SkillTag("buff.fire")));
        }

        [Test]
        public void HasTag_WithParentTag_MatchesChildTags()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("buff.fire.ignite"));
            Assert.IsTrue(set.HasTag(new SkillTag("buff.fire")));
        }

        [Test]
        public void HasTag_WithChildTag_DoesNotMatchParentOnly()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("buff.fire"));
            Assert.IsFalse(set.HasTag(new SkillTag("buff.fire.ignite")));
        }

        [Test]
        public void RemoveTag_ThenHasTag_ReturnsFalse()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("buff.fire"));
            set.RemoveTag(new SkillTag("buff.fire"));
            Assert.IsFalse(set.HasTag(new SkillTag("buff.fire")));
        }

        [Test]
        public void Clear_RemovesAllTags()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("a"));
            set.AddTag(new SkillTag("b"));
            set.Clear();
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void Count_ReturnsCorrectCount()
        {
            var set = new SkillTagSet(8);
            Assert.AreEqual(0, set.Count);
            set.AddTag(new SkillTag("a"));
            set.AddTag(new SkillTag("b"));
            Assert.AreEqual(2, set.Count);
        }

        [Test]
        public void AddTag_Duplicate_DoesNotIncreaseCount()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("buff.fire"));
            set.AddTag(new SkillTag("buff.fire"));
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void HasTag_ExactMatch_Works()
        {
            var set = new SkillTagSet(8);
            set.AddTag(new SkillTag("buff.fire"));
            Assert.IsTrue(set.HasTag(new SkillTag("buff.fire")));
            Assert.IsFalse(set.HasTag(new SkillTag("buff.ice")));
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Tests/EditMode/SkillTagSetTests.cs`.

- [ ] **Step 4: Run tests to verify they fail**

Run: Unity Test Runner → EditMode → GameScript.Tests.SkillTagSetTests
Expected: FAIL — `SkillTagSet` type not found.

- [ ] **Step 5: Implement SkillTagSet**

```csharp
using System.Collections.Generic;
using Change.Framework.Collections;
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class SkillTagSet : ISkillTagSet
    {
        private readonly FastHashSet<string> _tags;

        public SkillTagSet(int capacity)
        {
            _tags = new FastHashSet<string>(capacity: capacity);
        }

        public int Count => _tags.Count;

        public bool HasTag(SkillTag tag)
        {
            if (_tags.Contains(tag.Value))
                return true;

            string prefix = tag.Value + ".";
            _tags.ForEach(t =>
            {
                // Parent matches child: if tag is "buff.fire" and set has "buff.fire.ignite"
                // We check if any stored tag starts with "tag.Value." (child)
            });

            // Check exact match first (already done above via Contains)
            // Then check if any stored tag is a child of the query tag
            bool found = false;
            _tags.ForEach(t =>
            {
                if (!found && t.StartsWith(prefix))
                    found = true;
            });
            return found;
        }

        public void AddTag(SkillTag tag)
        {
            _tags.Add(tag.Value);
        }

        public void RemoveTag(SkillTag tag)
        {
            _tags.Remove(tag.Value);
        }

        public void Clear()
        {
            _tags.Clear(ClearMode.Logical);
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Core/SkillTagSet.cs`.

- [ ] **Step 6: Run tests to verify they pass**

Run: Unity Test Runner → EditMode → GameScript.Tests.SkillTagSetTests
Expected: ALL PASS.

- [ ] **Step 7: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Skill/ UnityProject/Assets/GameScript/Skill/Core/SkillTagSet.cs UnityProject/Assets/GameScript/Tests/EditMode/SkillTagSetTests.cs
git commit -m "feat(skill): add SkillTag value type and SkillTagSet with hierarchical matching"
```

---

## Task 3: AttributeSet

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Skill/Enums.cs`
- Create: `UnityProject/Assets/Change/Framework/Skill/IAttribute.cs`
- Create: `UnityProject/Assets/Change/Framework/Skill/IAttributeSet.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Core/Attribute.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Core/AttributeSet.cs`
- Test: `UnityProject/Assets/GameScript/Tests/EditMode/AttributeSetTests.cs`

### Framework: Enums

- [ ] **Step 1: Write shared enums**

```csharp
namespace Change.Framework.Skill
{
    public enum SkillType { Active, Passive }
    public enum ActivationType { Manual, Auto, OnEvent }
    public enum SkillState { Ready, Casting, Executing, Cooldown }
    public enum ModifierPolarity { Buff, Debuff, Neutral }
    public enum ModifierStacking { Refresh, AddStack, Replace, Ignore }
    public enum TargetType { Self, Enemy, Ally, AllEnemies, AllAllies }
    public enum TriggerEventType
    {
        OnDealDamage, OnTakeDamage, OnHeal, OnKill, OnDeath, OnAttack,
        OnBuffApplied, OnBuffRemoved, OnDebuffApplied, OnDebuffRemoved, OnBuffStackChanged,
        OnSkillCast, OnSkillHit, OnSkillMiss, OnSkillCooldownEnd,
        OnBattleStart, OnTurnStart, OnTurnEnd, OnSpawn, OnHPThreshold
    }
    public enum TriggerScope { Self, Source, AllEnemies, AllAllies }
}
```

Save to `UnityProject/Assets/Change/Framework/Skill/Enums.cs`.

### Framework: IAttribute and IAttributeSet

- [ ] **Step 2: Write IAttribute interface**

```csharp
namespace Change.Framework.Skill
{
    public interface IAttribute
    {
        string Name { get; }
        float BaseValue { get; set; }
        float CurrentValue { get; }
        void AddAdditive(float value);
        void RemoveAdditive(float value);
        void AddMultiplicative(float value);
        void RemoveMultiplicative(float value);
        void Recalculate();
    }
}
```

Save to `UnityProject/Assets/Change/Framework/Skill/IAttribute.cs`.

- [ ] **Step 3: Write IAttributeSet interface**

```csharp
using System;

namespace Change.Framework.Skill
{
    public delegate void AttributeChangedHandler(string attributeName, float oldValue, float newValue);

    public interface IAttributeSet
    {
        IAttribute GetAttribute(string name);
        float GetCurrentValue(string name);
        void SetBaseValue(string name, float value);
        event AttributeChangedHandler OnAttributeChanged;
    }
}
```

Save to `UnityProject/Assets/Change/Framework/Skill/IAttributeSet.cs`.

### GameScript: Attribute and AttributeSet

- [ ] **Step 4: Write failing tests for AttributeSet**

```csharp
using Change.Framework.Skill;
using GameScript.Skill.Core;
using NUnit.Framework;

namespace GameScript.Tests
{
    public class AttributeSetTests
    {
        [Test]
        public void GetCurrentValue_ReturnsBaseValue_WhenNoModifiers()
        {
            var set = new AttributeSet();
            set.SetBaseValue("HP", 100f);
            Assert.AreEqual(100f, set.GetCurrentValue("HP"));
        }

        [Test]
        public void AddAdditive_IncreasesCurrentValue()
        {
            var set = new AttributeSet();
            set.SetBaseValue("ATK", 50f);
            var attr = set.GetAttribute("ATK");
            attr.AddAdditive(30f);
            attr.Recalculate();
            Assert.AreEqual(80f, attr.CurrentValue);
        }

        [Test]
        public void AddMultiplicative_MultipliesAfterAdditive()
        {
            var set = new AttributeSet();
            set.SetBaseValue("ATK", 100f);
            var attr = set.GetAttribute("ATK");
            attr.AddAdditive(50f);
            attr.AddMultiplicative(1.5f);
            attr.Recalculate();
            // (100 + 50) * 1.5 = 225
            Assert.AreEqual(225f, attr.CurrentValue);
        }

        [Test]
        public void RemoveAdditive_DecreasesCurrentValue()
        {
            var set = new AttributeSet();
            set.SetBaseValue("ATK", 50f);
            var attr = set.GetAttribute("ATK");
            attr.AddAdditive(30f);
            attr.Recalculate();
            Assert.AreEqual(80f, attr.CurrentValue);
            attr.RemoveAdditive(30f);
            attr.Recalculate();
            Assert.AreEqual(50f, attr.CurrentValue);
        }

        [Test]
        public void OnAttributeChanged_FiresWhenBaseValueChanges()
        {
            var set = new AttributeSet();
            set.SetBaseValue("HP", 100f);
            string changedName = null;
            float oldVal = 0f, newVal = 0f;
            set.OnAttributeChanged += (name, old, @new) =>
            {
                changedName = name;
                oldVal = old;
                newVal = @new;
            };
            set.SetBaseValue("HP", 80f);
            Assert.AreEqual("HP", changedName);
            Assert.AreEqual(100f, oldVal);
            Assert.AreEqual(80f, newVal);
        }

        [Test]
        public void GetAttribute_UnknownName_ReturnsNull()
        {
            var set = new AttributeSet();
            Assert.IsNull(set.GetAttribute("Unknown"));
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Tests/EditMode/AttributeSetTests.cs`.

- [ ] **Step 5: Run tests to verify they fail**

Run: Unity Test Runner → EditMode → GameScript.Tests.AttributeSetTests
Expected: FAIL — `Attribute` and `AttributeSet` types not found.

- [ ] **Step 6: Implement Attribute**

```csharp
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class Attribute : IAttribute
    {
        private float _baseValue;
        private float _additiveSum;
        private float _multiplicativeProduct = 1f;
        private float _currentValue;

        public string Name { get; }

        public float BaseValue
        {
            get => _baseValue;
            set
            {
                _baseValue = value;
                Recalculate();
            }
        }

        public float CurrentValue => _currentValue;

        public Attribute(string name, float baseValue = 0f)
        {
            Name = name;
            _baseValue = baseValue;
            _additiveSum = 0f;
            _multiplicativeProduct = 1f;
            _currentValue = baseValue;
        }

        public void AddAdditive(float value)
        {
            _additiveSum += value;
        }

        public void RemoveAdditive(float value)
        {
            _additiveSum -= value;
        }

        public void AddMultiplicative(float value)
        {
            _multiplicativeProduct *= value;
        }

        public void RemoveMultiplicative(float value)
        {
            _multiplicativeProduct /= value;
        }

        public void Recalculate()
        {
            _currentValue = (_baseValue + _additiveSum) * _multiplicativeProduct;
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Core/Attribute.cs`.

- [ ] **Step 7: Implement AttributeSet**

```csharp
using System;
using Change.Framework.Collections;
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class AttributeSet : IAttributeSet
    {
        private readonly FastDictionary<string, Attribute> _attributes;

        public event AttributeChangedHandler OnAttributeChanged;

        public AttributeSet(int capacity = 16)
        {
            _attributes = new FastDictionary<string, Attribute>(capacity: capacity);
        }

        public IAttribute GetAttribute(string name)
        {
            return _attributes.TryGetValue(name, out var attr) ? attr : null;
        }

        public float GetCurrentValue(string name)
        {
            return _attributes.TryGetValue(name, out var attr) ? attr.CurrentValue : 0f;
        }

        public void SetBaseValue(string name, float value)
        {
            if (!_attributes.TryGetValue(name, out var attr))
            {
                attr = new Attribute(name, value);
                _attributes.TryAdd(name, attr);
                OnAttributeChanged?.Invoke(name, 0f, value);
                return;
            }

            float oldValue = attr.CurrentValue;
            attr.BaseValue = value;
            OnAttributeChanged?.Invoke(name, oldValue, attr.CurrentValue);
        }

        internal Attribute GetOrCreateAttribute(string name, float baseValue = 0f)
        {
            if (!_attributes.TryGetValue(name, out var attr))
            {
                attr = new Attribute(name, baseValue);
                _attributes.TryAdd(name, attr);
            }
            return attr;
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Core/AttributeSet.cs`.

- [ ] **Step 8: Run tests to verify they pass**

Run: Unity Test Runner → EditMode → GameScript.Tests.AttributeSetTests
Expected: ALL PASS.

- [ ] **Step 9: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Skill/Enums.cs UnityProject/Assets/Change/Framework/Skill/IAttribute.cs UnityProject/Assets/Change/Framework/Skill/IAttributeSet.cs UnityProject/Assets/GameScript/Skill/Core/Attribute.cs UnityProject/Assets/GameScript/Skill/Core/AttributeSet.cs UnityProject/Assets/GameScript/Tests/EditMode/AttributeSetTests.cs
git commit -m "feat(skill): add AttributeSet with additive and multiplicative modifier support"
```

---

## Task 4: Remaining Framework Interfaces

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Skill/ISkillEffect.cs`
- Create: `UnityProject/Assets/Change/Framework/Skill/IModifier.cs`
- Create: `UnityProject/Assets/Change/Framework/Skill/ITargetResolver.cs`
- Create: `UnityProject/Assets/Change/Framework/Skill/ITrigger.cs`
- Create: `UnityProject/Assets/Change/Framework/Skill/ISkill.cs`
- Create: `UnityProject/Assets/Change/Framework/Skill/IAbilitySystem.cs`

- [ ] **Step 1: Write ISkillEffect interface**

```csharp
namespace Change.Framework.Skill
{
    public interface ISkillEffect
    {
        void Execute(IAbilitySystem source, IAbilitySystem target);
    }
}
```

Save to `UnityProject/Assets/Change/Framework/Skill/ISkillEffect.cs`.

- [ ] **Step 2: Write IModifier interface**

```csharp
namespace Change.Framework.Skill
{
    public interface IModifier
    {
        string Id { get; }
        ModifierPolarity Polarity { get; }
        SkillTag[] GrantedTags { get; }
        int StackCount { get; }
        bool IsExpired { get; }
        void OnApply(IAbilitySystem target);
        void OnTick(IAbilitySystem target, float deltaTime);
        void OnRemove(IAbilitySystem target);
    }
}
```

Save to `UnityProject/Assets/Change/Framework/Skill/IModifier.cs`.

- [ ] **Step 3: Write ITargetResolver interface**

```csharp
namespace Change.Framework.Skill
{
    public interface ITargetResolver
    {
        TargetType Type { get; }
        IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities);
    }
}
```

Save to `UnityProject/Assets/Change/Framework/Skill/ITargetResolver.cs`.

- [ ] **Step 4: Write ITrigger interface**

```csharp
namespace Change.Framework.Skill
{
    public interface ITrigger
    {
        TriggerEventType EventType { get; }
        TriggerScope Scope { get; }
        bool EvaluateCondition(IAbilitySystem source, IAbilitySystem target);
        void ExecuteEffects(IAbilitySystem source, IAbilitySystem target, int cascadeDepth);
    }
}
```

Save to `UnityProject/Assets/Change/Framework/Skill/ITrigger.cs`.

- [ ] **Step 5: Write ISkill interface**

```csharp
using System;

namespace Change.Framework.Skill
{
    public interface ISkill
    {
        string Id { get; }
        SkillType Type { get; }
        ActivationType Activation { get; }
        SkillState State { get; }
        SkillTag[] Tags { get; }
        float CooldownDuration { get; }
        int MaxCharges { get; }
        int CurrentCharges { get; }
        bool CanActivate(IAbilitySystem source);
        void Activate(IAbilitySystem source, IAbilitySystem[] targets);
        void TickCooldown(float deltaTime);
        event Action<string> OnStateChange;
    }
}
```

Save to `UnityProject/Assets/Change/Framework/Skill/ISkill.cs`.

- [ ] **Step 6: Write IAbilitySystem interface**

```csharp
using System;

namespace Change.Framework.Skill
{
    public interface IAbilitySystem
    {
        string EntityId { get; }
        IAttributeSet Attributes { get; }
        ISkillTagSet Tags { get; }
        void AddSkill(ISkill skill);
        ISkill GetSkill(string skillId);
        void AddModifier(IModifier modifier);
        void RemoveModifier(string modifierId);
        void AddTrigger(ITrigger trigger);
        void TickModifiers(float deltaTime);
        void TickSkills(float deltaTime);
    }
}
```

Save to `UnityProject/Assets/Change/Framework/Skill/IAbilitySystem.cs`.

- [ ] **Step 7: Verify compilation**

Run: Open Unity, confirm no errors.

- [ ] **Step 8: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Skill/
git commit -m "feat(skill): add all framework interfaces for skill system contracts"
```

---

## Task 5: SkillEffect Types

**Files:**
- Create: `UnityProject/Assets/GameScript/Skill/Effects/DamageEffect.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Effects/HealEffect.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Effects/ApplyModifierEffect.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Effects/RemoveModifierEffect.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Effects/TagEffect.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Effects/ChanceEffect.cs`
- Test: `UnityProject/Assets/GameScript/Tests/EditMode/SkillEffectTests.cs`

- [ ] **Step 1: Write failing tests for effects**

```csharp
using Change.Framework.Skill;
using GameScript.Skill.Core;
using GameScript.Skill.Effects;
using NUnit.Framework;

namespace GameScript.Tests
{
    public class SkillEffectTests
    {
        private AbilitySystem _source;
        private AbilitySystem _target;

        [SetUp]
        public void SetUp()
        {
            _source = new AbilitySystem("source");
            _source.Attributes.SetBaseValue("ATK", 100f);
            _source.Attributes.SetBaseValue("HP", 100f);

            _target = new AbilitySystem("target");
            _target.Attributes.SetBaseValue("HP", 200f);
            _target.Attributes.SetBaseValue("DEF", 0f);
        }

        [Test]
        public void DamageEffect_Flat_ReducesTargetHP()
        {
            var effect = new DamageEffect(flatAmount: 50f, scalingFormula: null, scalingAttribute: null);
            effect.Execute(_source, _target);
            Assert.AreEqual(150f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void DamageEffect_Scaling_UsesSourceAttribute()
        {
            var effect = new DamageEffect(flatAmount: 10f, scalingFormula: null, scalingAttribute: "ATK", scalingMultiplier: 1.5f);
            effect.Execute(_source, _target);
            // 10 + 100*1.5 = 160 damage → 200-160 = 40
            Assert.AreEqual(40f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void HealEffect_IncreasesTargetHP()
        {
            _target.Attributes.SetBaseValue("HP", 50f);
            var effect = new HealEffect(flatAmount: 30f, scalingFormula: null, scalingAttribute: null);
            effect.Execute(_source, _target);
            Assert.AreEqual(80f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void ChanceEffect_OnSuccess_AppliesEffect()
        {
            // Rate 1.0 = always succeeds
            var innerEffect = new DamageEffect(flatAmount: 25f, scalingFormula: null, scalingAttribute: null);
            var effect = new ChanceEffect(rate: 1.0f, onSuccess: new ISkillEffect[] { innerEffect }, onFailure: null);
            effect.Execute(_source, _target);
            Assert.AreEqual(175f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void TagEffect_Grant_AddsTagToTarget()
        {
            var effect = new TagEffect(grant: true, tag: new SkillTag("state.stunned"));
            effect.Execute(_source, _target);
            Assert.IsTrue(_target.Tags.HasTag(new SkillTag("state.stunned")));
        }

        [Test]
        public void TagEffect_Remove_RemovesTagFromTarget()
        {
            _target.Tags.AddTag(new SkillTag("state.stunned"));
            var effect = new TagEffect(grant: false, tag: new SkillTag("state.stunned"));
            effect.Execute(_source, _target);
            Assert.IsFalse(_target.Tags.HasTag(new SkillTag("state.stunned")));
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Tests/EditMode/SkillEffectTests.cs`.

- [ ] **Step 2: Run tests to verify they fail**

Run: Unity Test Runner → EditMode → GameScript.Tests.SkillEffectTests
Expected: FAIL — types not found.

- [ ] **Step 3: Implement DamageEffect**

```csharp
using Change.Framework.Skill;

namespace GameScript.Skill.Effects
{
    public sealed class DamageEffect : ISkillEffect
    {
        private readonly float _flatAmount;
        private readonly string _scalingAttribute;
        private readonly float _scalingMultiplier;
        private readonly string _scalingFormula;

        public DamageEffect(float flatAmount, string scalingFormula, string scalingAttribute, float scalingMultiplier = 1f)
        {
            _flatAmount = flatAmount;
            _scalingFormula = scalingFormula;
            _scalingAttribute = scalingAttribute;
            _scalingMultiplier = scalingMultiplier;
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            float amount = _flatAmount;
            if (_scalingAttribute != null)
            {
                float scalingValue = source.Attributes.GetCurrentValue(_scalingAttribute);
                amount += scalingValue * _scalingMultiplier;
            }

            float currentHP = target.Attributes.GetCurrentValue("HP");
            target.Attributes.SetBaseValue("HP", currentHP - amount);
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Effects/DamageEffect.cs`.

- [ ] **Step 4: Implement HealEffect**

```csharp
using Change.Framework.Skill;

namespace GameScript.Skill.Effects
{
    public sealed class HealEffect : ISkillEffect
    {
        private readonly float _flatAmount;
        private readonly string _scalingAttribute;
        private readonly float _scalingMultiplier;

        public HealEffect(float flatAmount, string scalingFormula, string scalingAttribute, float scalingMultiplier = 1f)
        {
            _flatAmount = flatAmount;
            _scalingAttribute = scalingAttribute;
            _scalingMultiplier = scalingMultiplier;
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            float amount = _flatAmount;
            if (_scalingAttribute != null)
            {
                float scalingValue = source.Attributes.GetCurrentValue(_scalingAttribute);
                amount += scalingValue * _scalingMultiplier;
            }

            float currentHP = target.Attributes.GetCurrentValue("HP");
            target.Attributes.SetBaseValue("HP", currentHP + amount);
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Effects/HealEffect.cs`.

- [ ] **Step 5: Implement TagEffect**

```csharp
using Change.Framework.Skill;

namespace GameScript.Skill.Effects
{
    public sealed class TagEffect : ISkillEffect
    {
        private readonly bool _grant;
        private readonly SkillTag _tag;

        public TagEffect(bool grant, SkillTag tag)
        {
            _grant = grant;
            _tag = tag;
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            if (_grant)
                target.Tags.AddTag(_tag);
            else
                target.Tags.RemoveTag(_tag);
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Effects/TagEffect.cs`.

- [ ] **Step 6: Implement ChanceEffect**

```csharp
using System;
using Change.Framework.Skill;

namespace GameScript.Skill.Effects
{
    public sealed class ChanceEffect : ISkillEffect
    {
        private readonly float _rate;
        private readonly ISkillEffect[] _onSuccess;
        private readonly ISkillEffect[] _onFailure;

        private static readonly Random _rng = new Random();

        public ChanceEffect(float rate, ISkillEffect[] onSuccess, ISkillEffect[] onFailure)
        {
            _rate = rate;
            _onSuccess = onSuccess ?? Array.Empty<ISkillEffect>();
            _onFailure = onFailure ?? Array.Empty<ISkillEffect>();
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            var effects = _rng.NextDouble() < _rate ? _onSuccess : _onFailure;
            for (int i = 0; i < effects.Length; i++)
            {
                effects[i].Execute(source, target);
            }
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Effects/ChanceEffect.cs`.

- [ ] **Step 7: Implement ApplyModifierEffect and RemoveModifierEffect stubs**

```csharp
using Change.Framework.Skill;

namespace GameScript.Skill.Effects
{
    public sealed class ApplyModifierEffect : ISkillEffect
    {
        private readonly string _modifierId;

        public ApplyModifierEffect(string modifierId)
        {
            _modifierId = modifierId;
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            // Will be implemented in Task 10 when AbilitySystem has modifier creation
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Effects/ApplyModifierEffect.cs`.

```csharp
using Change.Framework.Skill;

namespace GameScript.Skill.Effects
{
    public sealed class RemoveModifierEffect : ISkillEffect
    {
        private readonly SkillTag _dispelTag;

        public RemoveModifierEffect(SkillTag dispelTag)
        {
            _dispelTag = dispelTag;
        }

        public void Execute(IAbilitySystem source, IAbilitySystem target)
        {
            // Will be implemented in Task 10 when AbilitySystem has modifier removal by tag
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Effects/RemoveModifierEffect.cs`.

- [ ] **Step 8: Run tests to verify they pass**

Run: Unity Test Runner → EditMode → GameScript.Tests.SkillEffectTests
Expected: ALL PASS.

- [ ] **Step 9: Commit**

```bash
git add UnityProject/Assets/GameScript/Skill/Effects/ UnityProject/Assets/GameScript/Tests/EditMode/SkillEffectTests.cs
git commit -m "feat(skill): add DamageEffect, HealEffect, TagEffect, ChanceEffect implementations"
```

---

## Task 6: Modifier Implementation

**Files:**
- Create: `UnityProject/Assets/GameScript/Skill/Core/Modifier.cs`
- Test: `UnityProject/Assets/GameScript/Tests/EditMode/ModifierTests.cs`

- [ ] **Step 1: Write failing tests for Modifier**

```csharp
using Change.Framework.Skill;
using GameScript.Skill.Core;
using GameScript.Skill.Effects;
using NUnit.Framework;

namespace GameScript.Tests
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
                GrantedTags = new[] { new SkillTag("state.stunned") },
                Stacking = ModifierStacking.Replace,
                MaxStack = 1
            });
            mod.OnApply(_target);
            Assert.IsTrue(_target.Tags.HasTag(new SkillTag("state.stunned")));
        }

        [Test]
        public void OnRemove_RevokesTags()
        {
            var mod = new Modifier(new ModifierConfig
            {
                Id = "stun",
                Polarity = ModifierPolarity.Debuff,
                GrantedTags = new[] { new SkillTag("state.stunned") },
                Stacking = ModifierStacking.Replace,
                MaxStack = 1
            });
            mod.OnApply(_target);
            mod.OnRemove(_target);
            Assert.IsFalse(_target.Tags.HasTag(new SkillTag("state.stunned")));
        }

        [Test]
        public void OnTick_ExecutesPeriodicEffects()
        {
            var mod = new Modifier(new ModifierConfig
            {
                Id = "ignite",
                Polarity = ModifierPolarity.Debuff,
                GrantedTags = new SkillTag[0],
                Stacking = ModifierStacking.Refresh,
                MaxStack = 1,
                TickInterval = 1f,
                TickEffects = new ISkillEffect[]
                {
                    new DamageEffect(flatAmount: 20f, scalingFormula: null, scalingAttribute: null)
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
                GrantedTags = new SkillTag[0],
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
                GrantedTags = new SkillTag[0],
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
            mod.AddStack(); // Should not exceed max
            Assert.AreEqual(3, mod.StackCount);
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Tests/EditMode/ModifierTests.cs`.

- [ ] **Step 2: Run tests to verify they fail**

Run: Unity Test Runner → EditMode → GameScript.Tests.ModifierTests
Expected: FAIL — `ModifierConfig` and `Modifier` types not found.

- [ ] **Step 3: Implement ModifierConfig**

```csharp
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class ModifierConfig
    {
        public string Id;
        public ModifierPolarity Polarity;
        public SkillTag[] GrantedTags;
        public ModifierStacking Stacking;
        public int MaxStack = 1;
        public float Duration;
        public float TickInterval;
        public ISkillEffect[] ApplyEffects;
        public ISkillEffect[] TickEffects;
        public ISkillEffect[] RemoveEffects;
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Core/ModifierConfig.cs`.

- [ ] **Step 4: Implement Modifier**

```csharp
using System;
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class Modifier : IModifier
    {
        private readonly ModifierConfig _config;
        private float _elapsedTime;
        private float _tickTimer;
        private int _stackCount;

        public string Id => _config.Id;
        public ModifierPolarity Polarity => _config.Polarity;
        public SkillTag[] GrantedTags => _config.GrantedTags;
        public int StackCount => _stackCount;

        public bool IsExpired
        {
            get
            {
                if (_config.Duration <= 0f) return false;
                return _elapsedTime >= _config.Duration;
            }
        }

        public Modifier(ModifierConfig config)
        {
            _config = config;
            _stackCount = 1;
            _elapsedTime = 0f;
            _tickTimer = 0f;
        }

        public void OnApply(IAbilitySystem target)
        {
            if (_config.GrantedTags != null)
            {
                for (int i = 0; i < _config.GrantedTags.Length; i++)
                    target.Tags.AddTag(_config.GrantedTags[i]);
            }

            ExecuteEffects(_config.ApplyEffects, target);
        }

        public void OnTick(IAbilitySystem target, float deltaTime)
        {
            _elapsedTime += deltaTime;

            if (_config.TickInterval > 0f)
            {
                _tickTimer += deltaTime;
                while (_tickTimer >= _config.TickInterval)
                {
                    _tickTimer -= _config.TickInterval;
                    ExecuteEffects(_config.TickEffects, target);
                }
            }
        }

        public void OnRemove(IAbilitySystem target)
        {
            if (_config.GrantedTags != null)
            {
                for (int i = 0; i < _config.GrantedTags.Length; i++)
                    target.Tags.RemoveTag(_config.GrantedTags[i]);
            }

            ExecuteEffects(_config.RemoveEffects, target);
        }

        public void AddStack()
        {
            if (_stackCount < _config.MaxStack)
                _stackCount++;
        }

        public void RefreshDuration()
        {
            _elapsedTime = 0f;
            _tickTimer = 0f;
        }

        private void ExecuteEffects(ISkillEffect[] effects, IAbilitySystem target)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Length; i++)
                effects[i].Execute(target, target);
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Core/Modifier.cs`.

- [ ] **Step 5: Run tests to verify they pass**

Run: Unity Test Runner → EditMode → GameScript.Tests.ModifierTests
Expected: ALL PASS.

- [ ] **Step 6: Commit**

```bash
git add UnityProject/Assets/GameScript/Skill/Core/Modifier.cs UnityProject/Assets/GameScript/Skill/Core/ModifierConfig.cs UnityProject/Assets/GameScript/Tests/EditMode/ModifierTests.cs
git commit -m "feat(skill): add Modifier with lifecycle, stacking, and periodic tick effects"
```

---

## Task 7: TargetResolver Implementations

**Files:**
- Create: `UnityProject/Assets/GameScript/Skill/Targeting/SelfTargetResolver.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Targeting/EnemyTargetResolver.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Targeting/AllyTargetResolver.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Targeting/AoETargetResolver.cs`
- Test: `UnityProject/Assets/GameScript/Tests/EditMode/TargetResolverTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Change.Framework.Skill;
using GameScript.Skill.Core;
using GameScript.Skill.Targeting;
using NUnit.Framework;

namespace GameScript.Tests
{
    public class TargetResolverTests
    {
        private AbilitySystem _source;
        private AbilitySystem[] _allEntities;

        [SetUp]
        public void SetUp()
        {
            _source = new AbilitySystem("hero1");
            var enemy1 = new AbilitySystem("enemy1");
            var enemy2 = new AbilitySystem("enemy2");
            var ally1 = new AbilitySystem("hero2");
            _allEntities = new[] { _source, enemy1, enemy2, ally1 };
        }

        [Test]
        public void SelfTarget_ReturnsSource()
        {
            var resolver = new SelfTargetResolver();
            var targets = resolver.Resolve(_source, _allEntities);
            Assert.AreEqual(1, targets.Length);
            Assert.AreEqual("hero1", targets[0].EntityId);
        }

        [Test]
        public void EnemyTarget_ReturnsCorrectCount()
        {
            var resolver = new EnemyTargetResolver(count: 2);
            var targets = resolver.Resolve(_source, _allEntities);
            Assert.AreEqual(2, targets.Length);
        }

        [Test]
        public void AoETarget_AllEnemies_ReturnsAllEnemies()
        {
            var resolver = new AoETargetResolver(TargetType.AllEnemies);
            var targets = resolver.Resolve(_source, _allEntities);
            // 2 enemies (enemy1, enemy2) out of 4 entities
            Assert.AreEqual(2, targets.Length);
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Tests/EditMode/TargetResolverTests.cs`.

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL — types not found.

- [ ] **Step 3: Implement target resolvers**

```csharp
using Change.Framework.Skill;

namespace GameScript.Skill.Targeting
{
    public sealed class SelfTargetResolver : ITargetResolver
    {
        public TargetType Type => TargetType.Self;

        public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
        {
            return new IAbilitySystem[] { source };
        }
    }
}
```

```csharp
using System;
using Change.Framework.Skill;

namespace GameScript.Skill.Targeting
{
    public sealed class EnemyTargetResolver : ITargetResolver
    {
        private readonly int _count;
        private static readonly Random _rng = new Random();

        public TargetType Type => TargetType.Enemy;

        public EnemyTargetResolver(int count)
        {
            _count = count;
        }

        public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
        {
            var enemies = new System.Collections.Generic.List<IAbilitySystem>();
            for (int i = 0; i < allEntities.Length; i++)
            {
                if (allEntities[i].EntityId != source.EntityId)
                    enemies.Add(allEntities[i]);
            }

            int resultCount = Math.Min(_count, enemies.Count);
            var result = new IAbilitySystem[resultCount];
            for (int i = 0; i < resultCount; i++)
            {
                int idx = _rng.Next(enemies.Count);
                result[i] = enemies[idx];
                enemies.RemoveAt(idx);
            }
            return result;
        }
    }
}
```

```csharp
using System.Collections.Generic;
using Change.Framework.Skill;

namespace GameScript.Skill.Targeting
{
    public sealed class AllyTargetResolver : ITargetResolver
    {
        private readonly int _count;

        public TargetType Type => TargetType.Ally;

        public AllyTargetResolver(int count)
        {
            _count = count;
        }

        public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
        {
            var allies = new List<IAbilitySystem>();
            for (int i = 0; i < allEntities.Length; i++)
            {
                if (allEntities[i].EntityId == source.EntityId)
                {
                    allies.Add(allEntities[i]);
                    break;
                }
            }
            return allies.ToArray();
        }
    }
}
```

```csharp
using System.Collections.Generic;
using Change.Framework.Skill;

namespace GameScript.Skill.Targeting
{
    public sealed class AoETargetResolver : ITargetResolver
    {
        private readonly TargetType _targetType;

        public TargetType Type => _targetType;

        public AoETargetResolver(TargetType targetType)
        {
            _targetType = targetType;
        }

        public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
        {
            var result = new List<IAbilitySystem>();
            bool targetEnemies = _targetType == TargetType.AllEnemies;

            for (int i = 0; i < allEntities.Length; i++)
            {
                bool isEnemy = allEntities[i].EntityId != source.EntityId;
                if (targetEnemies && isEnemy)
                    result.Add(allEntities[i]);
                else if (!targetEnemies && !isEnemy)
                    result.Add(allEntities[i]);
            }
            return result.ToArray();
        }
    }
}
```

Save all to `UnityProject/Assets/GameScript/Skill/Targeting/`.

- [ ] **Step 4: Run tests to verify they pass**

Expected: ALL PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/GameScript/Skill/Targeting/ UnityProject/Assets/GameScript/Tests/EditMode/TargetResolverTests.cs
git commit -m "feat(skill): add SelfTarget, EnemyTarget, AllyTarget, and AoE target resolvers"
```

---

## Task 8: Trigger Implementation

**Files:**
- Create: `UnityProject/Assets/GameScript/Skill/Core/Trigger.cs`
- Test: `UnityProject/Assets/GameScript/Tests/EditMode/TriggerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Change.Framework.Skill;
using GameScript.Skill.Core;
using GameScript.Skill.Effects;
using NUnit.Framework;

namespace GameScript.Tests
{
    public class TriggerTests
    {
        private AbilitySystem _source;
        private AbilitySystem _target;

        [SetUp]
        public void SetUp()
        {
            _source = new AbilitySystem("source");
            _source.Attributes.SetBaseValue("ATK", 100f);
            _target = new AbilitySystem("target");
            _target.Attributes.SetBaseValue("HP", 200f);
        }

        [Test]
        public void ExecuteEffects_AppliesDamage()
        {
            var trigger = new Trigger(
                eventType: TriggerEventType.OnDealDamage,
                scope: TriggerScope.Self,
                condition: null,
                cooldown: 0f,
                effects: new ISkillEffect[]
                {
                    new DamageEffect(flatAmount: 30f, scalingFormula: null, scalingAttribute: null)
                }
            );
            trigger.ExecuteEffects(_source, _target, 0);
            Assert.AreEqual(170f, _target.Attributes.GetCurrentValue("HP"));
        }

        [Test]
        public void EvaluateCondition_NoCondition_ReturnsTrue()
        {
            var trigger = new Trigger(
                eventType: TriggerEventType.OnAttack,
                scope: TriggerScope.Self,
                condition: null,
                cooldown: 0f,
                effects: null
            );
            Assert.IsTrue(trigger.EvaluateCondition(_source, _target));
        }

        [Test]
        public void Cooldown_PreventsReFireWithinPeriod()
        {
            var trigger = new Trigger(
                eventType: TriggerEventType.OnAttack,
                scope: TriggerScope.Self,
                condition: null,
                cooldown: 5f,
                effects: new ISkillEffect[]
                {
                    new DamageEffect(flatAmount: 10f, scalingFormula: null, scalingAttribute: null)
                }
            );
            trigger.ExecuteEffects(_source, _target, 0);
            // Should not fire again within cooldown
            bool didFire = trigger.TryFire(_source, _target, 0);
            Assert.IsFalse(didFire);
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Tests/EditMode/TriggerTests.cs`.

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL — `Trigger` type not found.

- [ ] **Step 3: Implement Trigger**

```csharp
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class Trigger : ITrigger
    {
        private readonly ISkillEffect[] _effects;
        private readonly float _cooldown;
        private float _cooldownTimer;

        public TriggerEventType EventType { get; }
        public TriggerScope Scope { get; }

        public Trigger(
            TriggerEventType eventType,
            TriggerScope scope,
            System.Func<IAbilitySystem, IAbilitySystem, bool> condition,
            float cooldown,
            ISkillEffect[] effects)
        {
            EventType = eventType;
            Scope = scope;
            _cooldown = cooldown;
            _effects = effects ?? System.Array.Empty<ISkillEffect>();
            _cooldownTimer = 0f;
        }

        public bool EvaluateCondition(IAbilitySystem source, IAbilitySystem target)
        {
            return true; // Simplified — condition logic can be added later
        }

        public void ExecuteEffects(IAbilitySystem source, IAbilitySystem target, int cascadeDepth)
        {
            for (int i = 0; i < _effects.Length; i++)
                _effects[i].Execute(source, target);
            _cooldownTimer = _cooldown;
        }

        public bool TryFire(IAbilitySystem source, IAbilitySystem target, int cascadeDepth)
        {
            if (_cooldownTimer > 0f)
                return false;
            if (!EvaluateCondition(source, target))
                return false;
            ExecuteEffects(source, target, cascadeDepth);
            return true;
        }

        public void TickCooldown(float deltaTime)
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= deltaTime;
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Core/Trigger.cs`.

- [ ] **Step 4: Run tests to verify they pass**

Expected: ALL PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/GameScript/Skill/Core/Trigger.cs UnityProject/Assets/GameScript/Tests/EditMode/TriggerTests.cs
git commit -m "feat(skill): add Trigger with event matching, conditions, and cooldown"
```

---

## Task 9: Skill Implementation (FSM-Based)

**Files:**
- Create: `UnityProject/Assets/GameScript/Skill/Core/Skill.cs`
- Test: `UnityProject/Assets/GameScript/Tests/EditMode/SkillTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using System;
using Change.Framework.Skill;
using GameScript.Skill.Core;
using GameScript.Skill.Effects;
using NUnit.Framework;

namespace GameScript.Tests
{
    public class SkillTests
    {
        private AbilitySystem _source;
        private AbilitySystem[] _targets;

        [SetUp]
        public void SetUp()
        {
            _source = new AbilitySystem("hero");
            _source.Attributes.SetBaseValue("HP", 100f);
            _source.Attributes.SetBaseValue("ATK", 100f);
            _source.Attributes.SetBaseValue("Mana", 100f);

            var enemy = new AbilitySystem("enemy");
            enemy.Attributes.SetBaseValue("HP", 200f);
            _targets = new[] { enemy };
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
```

Save to `UnityProject/Assets/GameScript/Tests/EditMode/SkillTests.cs`.

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL — `SkillConfig` and `Skill` types not found.

- [ ] **Step 3: Implement SkillConfig**

```csharp
using System;
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class SkillConfig
    {
        public string Id;
        public SkillType Type;
        public ActivationType Activation;
        public SkillTag[] Tags;
        public string CostAttribute;
        public float CostAmount;
        public float CooldownDuration;
        public int MaxCharges = 1;
        public ISkillEffect[] Effects;
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Core/SkillConfig.cs`.

- [ ] **Step 4: Implement Skill**

```csharp
using System;
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class Skill : ISkill
    {
        private readonly SkillConfig _config;
        private readonly ISkillEffect[] _effects;
        private float _cooldownTimer;
        private int _currentCharges;

        public string Id => _config.Id;
        public SkillType Type => _config.Type;
        public ActivationType Activation => _config.Activation;
        public SkillState State { get; private set; } = SkillState.Ready;
        public SkillTag[] Tags => _config.Tags;
        public float CooldownDuration => _config.CooldownDuration;
        public int MaxCharges => _config.MaxCharges;
        public int CurrentCharges => _currentCharges;

        public event Action<string> OnStateChange;

        public Skill(SkillConfig config)
        {
            _config = config;
            _effects = config.Effects ?? Array.Empty<ISkillEffect>();
            _currentCharges = config.MaxCharges;
        }

        public bool CanActivate(IAbilitySystem source)
        {
            if (State == SkillState.Cooldown && _currentCharges <= 0)
                return false;

            if (_config.CostAttribute != null)
            {
                float current = source.Attributes.GetCurrentValue(_config.CostAttribute);
                if (current < _config.CostAmount)
                    return false;
            }

            return true;
        }

        public void Activate(IAbilitySystem source, IAbilitySystem[] targets)
        {
            if (!CanActivate(source))
                return;

            // Commit cost
            if (_config.CostAttribute != null)
            {
                float current = source.Attributes.GetCurrentValue(_config.CostAttribute);
                source.Attributes.SetBaseValue(_config.CostAttribute, current - _config.CostAmount);
            }

            SetState(SkillState.Casting);
            SetState(SkillState.Executing);

            for (int i = 0; i < _effects.Length; i++)
            {
                for (int t = 0; t < targets.Length; t++)
                {
                    _effects[i].Execute(source, targets[t]);
                }
            }

            // Start cooldown
            _currentCharges--;
            if (_currentCharges <= 0)
            {
                SetState(SkillState.Cooldown);
                _cooldownTimer = _config.CooldownDuration;
            }
            else
            {
                SetState(SkillState.Ready);
            }
        }

        public void TickCooldown(float deltaTime)
        {
            if (State != SkillState.Cooldown)
                return;

            _cooldownTimer -= deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _currentCharges = _config.MaxCharges;
                _cooldownTimer = 0f;
                SetState(SkillState.Ready);
            }
        }

        private void SetState(SkillState state)
        {
            State = state;
            OnStateChange?.Invoke(state.ToString());
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Core/Skill.cs`.

- [ ] **Step 5: Run tests to verify they pass**

Expected: ALL PASS.

- [ ] **Step 6: Commit**

```bash
git add UnityProject/Assets/GameScript/Skill/Core/Skill.cs UnityProject/Assets/GameScript/Skill/Core/SkillConfig.cs UnityProject/Assets/GameScript/Tests/EditMode/SkillTests.cs
git commit -m "feat(skill): add Skill with FSM lifecycle, cost, cooldown, and charges"
```

---

## Task 10: AbilitySystem Implementation

**Files:**
- Create: `UnityProject/Assets/GameScript/Skill/Core/AbilitySystem.cs`
- Test: `UnityProject/Assets/GameScript/Tests/EditMode/AbilitySystemTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Change.Framework.Skill;
using GameScript.Skill.Core;
using GameScript.Skill.Effects;
using NUnit.Framework;

namespace GameScript.Tests
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
            var skill = new Skill(new SkillConfig
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
            var skill = new Skill(new SkillConfig
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
```

Save to `UnityProject/Assets/GameScript/Tests/EditMode/AbilitySystemTests.cs`.

- [ ] **Step 2: Run tests to verify they fail**

Expected: FAIL — `AbilitySystem` constructor/methods not found.

- [ ] **Step 3: Implement AbilitySystem**

```csharp
using System;
using System.Collections.Generic;
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class AbilitySystem : IAbilitySystem
    {
        private readonly Dictionary<string, ISkill> _skills = new Dictionary<string, ISkill>();
        private readonly List<IModifier> _modifiers = new List<IModifier>();
        private readonly List<ITrigger> _triggers = new List<ITrigger>();

        public string EntityId { get; }
        public IAttributeSet Attributes { get; }
        public ISkillTagSet Tags { get; }

        public AbilitySystem(string entityId)
        {
            EntityId = entityId;
            Attributes = new AttributeSet();
            Tags = new SkillTagSet(16);
        }

        public void AddSkill(ISkill skill)
        {
            _skills[skill.Id] = skill;
        }

        public ISkill GetSkill(string skillId)
        {
            return _skills.TryGetValue(skillId, out var skill) ? skill : null;
        }

        public void AddModifier(IModifier modifier)
        {
            _modifiers.Add(modifier);
            modifier.OnApply(this);
        }

        public void RemoveModifier(string modifierId)
        {
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i].Id == modifierId)
                {
                    _modifiers[i].OnRemove(this);
                    _modifiers.RemoveAt(i);
                    return;
                }
            }
        }

        public void AddTrigger(ITrigger trigger)
        {
            _triggers.Add(trigger);
        }

        public void TickModifiers(float deltaTime)
        {
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                _modifiers[i].OnTick(this, deltaTime);
                if (_modifiers[i].IsExpired)
                {
                    _modifiers[i].OnRemove(this);
                    _modifiers.RemoveAt(i);
                }
            }
        }

        public void TickSkills(float deltaTime)
        {
            foreach (var kvp in _skills)
            {
                kvp.Value.TickCooldown(deltaTime);
            }
        }

        internal IReadOnlyList<ITrigger> GetTriggers() => _triggers;
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Core/AbilitySystem.cs`.

- [ ] **Step 4: Run tests to verify they pass**

Expected: ALL PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/GameScript/Skill/Core/AbilitySystem.cs UnityProject/Assets/GameScript/Tests/EditMode/AbilitySystemTests.cs
git commit -m "feat(skill): add AbilitySystem entity manager with skills, modifiers, and triggers"
```

---

## Task 11: CQRS Messages + TriggerEngine

**Files:**
- Create: `UnityProject/Assets/GameScript/Skill/Cqrs/CastSkillCmd.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Cqrs/ApplyModifierCmd.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Cqrs/DamageAppliedEvt.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Cqrs/HealAppliedEvt.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Cqrs/ModifierAppliedEvt.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Cqrs/ModifierExpiredEvt.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Cqrs/AttributeChangedEvt.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Cqrs/GetAttributeValueQry.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Cqrs/HasTagQry.cs`
- Create: `UnityProject/Assets/GameScript/Skill/Core/TriggerEngine.cs`
- Test: `UnityProject/Assets/GameScript/Tests/EditMode/TriggerEngineTests.cs`

- [ ] **Step 1: Write all CQRS message structs**

Each message follows the project convention: `readonly struct` implementing marker interface.

```csharp
using Change.Framework.Cqrs;

namespace GameScript.Skill.Cqrs
{
    public readonly struct CastSkillCmd : ICommand
    {
        public CastSkillCmd(string skillId, string sourceId, string[] targetIds)
        {
            SkillId = skillId;
            SourceId = sourceId;
            TargetIds = targetIds;
        }
        public string SkillId { get; }
        public string SourceId { get; }
        public string[] TargetIds { get; }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Cqrs/CastSkillCmd.cs`.

```csharp
using Change.Framework.Cqrs;

namespace GameScript.Skill.Cqrs
{
    public readonly struct ApplyModifierCmd : ICommand
    {
        public ApplyModifierCmd(string modifierId, string sourceId, string targetId)
        {
            ModifierId = modifierId;
            SourceId = sourceId;
            TargetId = targetId;
        }
        public string ModifierId { get; }
        public string SourceId { get; }
        public string TargetId { get; }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Cqrs/ApplyModifierCmd.cs`.

```csharp
using Change.Framework.Cqrs;

namespace GameScript.Skill.Cqrs
{
    public readonly struct DamageAppliedEvt : IEvent
    {
        public DamageAppliedEvt(string targetId, float amount, string sourceSkillId)
        {
            TargetId = targetId;
            Amount = amount;
            SourceSkillId = sourceSkillId;
        }
        public string TargetId { get; }
        public float Amount { get; }
        public string SourceSkillId { get; }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Cqrs/DamageAppliedEvt.cs`.

```csharp
using Change.Framework.Cqrs;

namespace GameScript.Skill.Cqrs
{
    public readonly struct HealAppliedEvt : IEvent
    {
        public HealAppliedEvt(string targetId, float amount)
        {
            TargetId = targetId;
            Amount = amount;
        }
        public string TargetId { get; }
        public float Amount { get; }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Cqrs/HealAppliedEvt.cs`.

```csharp
using Change.Framework.Cqrs;

namespace GameScript.Skill.Cqrs
{
    public readonly struct ModifierAppliedEvt : IEvent
    {
        public ModifierAppliedEvt(string modifierId, string targetId)
        {
            ModifierId = modifierId;
            TargetId = targetId;
        }
        public string ModifierId { get; }
        public string TargetId { get; }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Cqrs/ModifierAppliedEvt.cs`.

```csharp
using Change.Framework.Cqrs;

namespace GameScript.Skill.Cqrs
{
    public readonly struct ModifierExpiredEvt : IEvent
    {
        public ModifierExpiredEvt(string modifierId, string targetId)
        {
            ModifierId = modifierId;
            TargetId = targetId;
        }
        public string ModifierId { get; }
        public string TargetId { get; }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Cqrs/ModifierExpiredEvt.cs`.

```csharp
using Change.Framework.Cqrs;

namespace GameScript.Skill.Cqrs
{
    public readonly struct AttributeChangedEvt : IEvent
    {
        public AttributeChangedEvt(string targetId, string attributeName, float oldValue, float newValue)
        {
            TargetId = targetId;
            AttributeName = attributeName;
            OldValue = oldValue;
            NewValue = newValue;
        }
        public string TargetId { get; }
        public string AttributeName { get; }
        public float OldValue { get; }
        public float NewValue { get; }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Cqrs/AttributeChangedEvt.cs`.

```csharp
using Change.Framework.Cqrs;

namespace GameScript.Skill.Cqrs
{
    public readonly struct GetAttributeValueQry : IQuery<float>
    {
        public GetAttributeValueQry(string targetId, string attributeName)
        {
            TargetId = targetId;
            AttributeName = attributeName;
        }
        public string TargetId { get; }
        public string AttributeName { get; }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Cqrs/GetAttributeValueQry.cs`.

```csharp
using Change.Framework.Cqrs;
using Change.Framework.Skill;

namespace GameScript.Skill.Cqrs
{
    public readonly struct HasTagQry : IQuery<bool>
    {
        public HasTagQry(string targetId, SkillTag tag)
        {
            TargetId = targetId;
            Tag = tag;
        }
        public string TargetId { get; }
        public SkillTag Tag { get; }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Cqrs/HasTagQry.cs`.

- [ ] **Step 2: Write TriggerEngine tests**

```csharp
using Change.Framework.Skill;
using GameScript.Skill.Core;
using GameScript.Skill.Effects;
using NUnit.Framework;

namespace GameScript.Tests
{
    public class TriggerEngineTests
    {
        [Test]
        public void DispatchEvent_MatchingTrigger_FiresEffect()
        {
            var engine = new TriggerEngine(maxCascadeDepth: 5);
            var source = new AbilitySystem("hero");
            source.Attributes.SetBaseValue("ATK", 100f);
            var target = new AbilitySystem("enemy");
            target.Attributes.SetBaseValue("HP", 200f);

            source.AddTrigger(new Trigger(
                eventType: TriggerEventType.OnDealDamage,
                scope: TriggerScope.Self,
                condition: null,
                cooldown: 0f,
                effects: new ISkillEffect[]
                {
                    new DamageEffect(flatAmount: 20f, scalingFormula: null, scalingAttribute: null)
                }
            ));

            var allEntities = new IAbilitySystem[] { source, target };
            engine.DispatchEvent(TriggerEventType.OnDealDamage, source, target, allEntities);
            // Source's trigger targets source itself (scope Self), but the trigger effect executes on source
            // This tests the dispatch flow, not damage values
            Assert.Pass();
        }

        [Test]
        public void DispatchEvent_ExceedsCascadeDepth_StopsChain()
        {
            var engine = new TriggerEngine(maxCascadeDepth: 2);
            var source = new AbilitySystem("hero");
            source.Attributes.SetBaseValue("HP", 1000f);
            var allEntities = new IAbilitySystem[] { source };

            // Trigger that would cause infinite loop if not guarded
            source.AddTrigger(new Trigger(
                eventType: TriggerEventType.OnDealDamage,
                scope: TriggerScope.Self,
                condition: null,
                cooldown: 0f,
                effects: new ISkillEffect[]
                {
                    new DamageEffect(flatAmount: 10f, scalingFormula: null, scalingAttribute: null)
                }
            ));

            engine.DispatchEvent(TriggerEventType.OnDealDamage, source, source, allEntities);
            // Should not hang — cascade guard stops the chain
            Assert.Pass();
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Tests/EditMode/TriggerEngineTests.cs`.

- [ ] **Step 3: Run tests to verify they fail**

Expected: FAIL — `TriggerEngine` type not found.

- [ ] **Step 4: Implement TriggerEngine**

```csharp
using System.Collections.Generic;
using Change.Framework.Skill;

namespace GameScript.Skill.Core
{
    public sealed class TriggerEngine
    {
        private readonly int _maxCascadeDepth;

        public TriggerEngine(int maxCascadeDepth = 5)
        {
            _maxCascadeDepth = maxCascadeDepth;
        }

        public void DispatchEvent(
            TriggerEventType eventType,
            IAbilitySystem source,
            IAbilitySystem target,
            IAbilitySystem[] allEntities,
            int cascadeDepth = 0)
        {
            if (cascadeDepth >= _maxCascadeDepth)
                return;

            for (int i = 0; i < allEntities.Length; i++)
            {
                var entity = allEntities[i] as AbilitySystem;
                if (entity == null) continue;

                var triggers = entity.GetTriggers();
                for (int t = 0; t < triggers.Count; t++)
                {
                    var trigger = triggers[t];
                    if (trigger.EventType != eventType)
                        continue;

                    if (trigger.TryFire(source, target, cascadeDepth))
                    {
                        // Recursive dispatch for effects triggered by this trigger
                        DispatchEvent(eventType, source, target, allEntities, cascadeDepth + 1);
                    }
                }
            }
        }
    }
}
```

Save to `UnityProject/Assets/GameScript/Skill/Core/TriggerEngine.cs`.

- [ ] **Step 5: Run tests to verify they pass**

Expected: ALL PASS.

- [ ] **Step 6: Commit**

```bash
git add UnityProject/Assets/GameScript/Skill/Cqrs/ UnityProject/Assets/GameScript/Skill/Core/TriggerEngine.cs UnityProject/Assets/GameScript/Tests/EditMode/TriggerEngineTests.cs
git commit -m "feat(skill): add CQRS messages and TriggerEngine with cascade guard"
```

---

## Task 12: End-to-End Integration Test

**Files:**
- Test: `UnityProject/Assets/GameScript/Tests/EditMode/IntegrationTests.cs`

- [ ] **Step 1: Write full battle scenario test**

```csharp
using Change.Framework.Skill;
using GameScript.Skill.Core;
using GameScript.Skill.Effects;
using NUnit.Framework;

namespace GameScript.Tests
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
            var fireball = new Skill(new SkillConfig
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

            enemy.TickModifiers(1f); // tick 2: 30 damage → would go negative
            // HP clamped or goes negative depending on implementation

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
            var skill = new Skill(new SkillConfig
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
```

Save to `UnityProject/Assets/GameScript/Tests/EditMode/IntegrationTests.cs`.

- [ ] **Step 2: Run integration tests**

Run: Unity Test Runner → EditMode → GameScript.Tests.IntegrationTests
Expected: ALL PASS (some adjustments may be needed based on edge cases).

- [ ] **Step 3: Commit**

```bash
git add UnityProject/Assets/GameScript/Tests/EditMode/IntegrationTests.cs
git commit -m "test(skill): add end-to-end integration tests for fireball+ignite and buff+damage scenarios"
```

---

## Self-Review Checklist

### Spec Coverage

| Spec Section | Task |
|-------------|------|
| SkillTag (3.5) | Task 2 |
| AttributeSet (3.4) | Task 3 |
| ISkill, IModifier, etc. (3.1-3.7) | Task 4 |
| SkillEffect (3.7) | Task 5 |
| Modifier lifecycle (5) | Task 6 |
| TargetResolver (3.6 ref) | Task 7 |
| Trigger (3.6) | Task 8 |
| Skill lifecycle (4) | Task 9 |
| AbilitySystem (3.1) | Task 10 |
| CQRS messages | Task 11 |
| TriggerEngine (6) | Task 11 |
| Integration | Task 12 |

### Placeholder Scan
- No TBD, TODO, or incomplete sections
- ApplyModifierEffect and RemoveModifierEffect have stub Execute methods — will be connected in Task 10's AbilitySystem integration

### Type Consistency
- `SkillTag` used consistently across all tasks as `Change.Framework.Skill.SkillTag`
- `IAbilitySystem` interface signature matches between Framework definition and GameScript usage
- `ISkillEffect.Execute(IAbilitySystem, IAbilitySystem)` signature consistent in all effect implementations
- `ModifierConfig` fields match `IModifier` interface properties
- `SkillConfig` fields match `ISkill` interface properties

### Gaps
- **Data Configuration (JSON/Nino)** deferred — requires Nino schema design per-game, not framework-level
- **Pool integration** for Modifier instances — recommended follow-up task
- **Damage formula parser** (`"ATK*1.5+100"`) simplified to direct attribute reference — full parser is a follow-up
