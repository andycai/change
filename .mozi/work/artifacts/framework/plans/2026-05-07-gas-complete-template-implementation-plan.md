# GAS Complete Template Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a reusable concrete GAS runtime in `Change.Framework.Gas` and a complete dual-entry demo/template system in `GameScript` that runs deterministic battle flows.

**Architecture:** Implement framework-agnostic GAS runtime primitives (ability system, abilities, effects, modifiers, triggers, target resolvers) under `Framework/Gas`, then assemble gameplay templates and scenario orchestration under `GameScript/GasTemplate`. Share one simulation assembly path across pure-code and `MonoBehaviour` entrypoints to prevent divergence.

**Tech Stack:** C# (.NET Standard 2.1), Unity 2022.3, existing `Change.Framework` + `GameScript`, Unity EditMode tests.

---

## File Structure (planned)

### Create

- `UnityProject/Assets/Change/Framework/Gas/DefaultAttribute.cs` - concrete `IAttribute`.
- `UnityProject/Assets/Change/Framework/Gas/DefaultAttributeSet.cs` - concrete `IAttributeSet`.
- `UnityProject/Assets/Change/Framework/Gas/DefaultGameplayTagSet.cs` - concrete `IGameplayTagSet`.
- `UnityProject/Assets/Change/Framework/Gas/DefaultAbilitySystem.cs` - concrete `IAbilitySystem` aggregate.
- `UnityProject/Assets/Change/Framework/Gas/BaseGameplayAbility.cs` - shared ability state machine/cooldown/charges.
- `UnityProject/Assets/Change/Framework/Gas/ActiveGameplayAbility.cs` - active-cast specialization.
- `UnityProject/Assets/Change/Framework/Gas/PassiveGameplayAbility.cs` - passive behavior shell.
- `UnityProject/Assets/Change/Framework/Gas/BaseModifier.cs` - shared modifier lifecycle.
- `UnityProject/Assets/Change/Framework/Gas/BaseTrigger.cs` - shared trigger evaluation + cooldown + cascade guard.
- `UnityProject/Assets/Change/Framework/Gas/DefaultTargetResolvers.cs` - `Self/Enemy/Ally/AllEnemies/AllAllies`.
- `UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/DefaultAbilitySystemTests.cs`
- `UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/AbilityLifecycleTests.cs`
- `UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/TriggerCascadeTests.cs`
- `UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/TargetResolverTests.cs`
- `UnityProject/Assets/GameScript/GasTemplate/ITemplateSource.cs`
- `UnityProject/Assets/GameScript/GasTemplate/TemplateBuildContext.cs`
- `UnityProject/Assets/GameScript/GasTemplate/AbilityTemplateRegistry.cs`
- `UnityProject/Assets/GameScript/GasTemplate/BattleSimulationEvent.cs`
- `UnityProject/Assets/GameScript/GasTemplate/BattleSimulationReport.cs`
- `UnityProject/Assets/GameScript/GasTemplate/Demo/DemoEntities.cs`
- `UnityProject/Assets/GameScript/GasTemplate/Demo/DemoEffects.cs`
- `UnityProject/Assets/GameScript/GasTemplate/Demo/DemoModifiers.cs`
- `UnityProject/Assets/GameScript/GasTemplate/Demo/DemoTriggers.cs`
- `UnityProject/Assets/GameScript/GasTemplate/Demo/DemoTemplateSource.cs`
- `UnityProject/Assets/GameScript/GasTemplate/Demo/DemoScenarioBuilder.cs`
- `UnityProject/Assets/GameScript/GasTemplate/Entry/GasTemplateRunner.cs`
- `UnityProject/Assets/GameScript/GasTemplate/Entry/GasTemplateBehaviour.cs`
- `UnityProject/Assets/Change/Runtime/Tests/EditMode/GasTemplate/GasTemplateIntegrationTests.cs`

### Modify

- `UnityProject/Assets/GameScript/` assembly definition(s) if new folder references are needed.
- `UnityProject/Assets/Change/Framework/AssemblyInfo.cs` and/or test asmdef references only if friend/test access requires update.

---

### Task 1: Build Attribute and Tag Concrete Types

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Gas/DefaultAttribute.cs`
- Create: `UnityProject/Assets/Change/Framework/Gas/DefaultAttributeSet.cs`
- Create: `UnityProject/Assets/Change/Framework/Gas/DefaultGameplayTagSet.cs`
- Test: `UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/DefaultAbilitySystemTests.cs`

- [ ] **Step 1: Write the failing tests for attribute math and tag counts**

```csharp
[Test]
public void DefaultAttributeSet_Recalculate_AppliesAdditiveAndMultiplicative()
{
    var set = new DefaultAttributeSet();
    set.SetBaseValue("Attack", 100f);
    var attack = set.GetAttribute("Attack");
    attack.AddAdditive(20f);
    attack.AddMultiplicative(0.5f);
    attack.Recalculate();
    Assert.AreEqual(180f, set.GetCurrentValue("Attack"), 0.001f);
}

[Test]
public void DefaultGameplayTagSet_MultipleAdds_TracksCount()
{
    var tags = new DefaultGameplayTagSet();
    tags.AddTag("State.Burning");
    tags.AddTag("State.Burning");
    Assert.IsTrue(tags.HasTag("State.Burning"));
    Assert.AreEqual(2, tags.GetTagCount("State.Burning"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test UnityProject/Assets/Change/Framework/Tests/EditMode --filter DefaultAttributeSet_Recalculate_AppliesAdditiveAndMultiplicative`
Expected: FAIL with missing `DefaultAttributeSet`/`DefaultGameplayTagSet` types.

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed class DefaultAttribute : IAttribute
{
    private float _additive;
    private float _multiplicative;
    public string Name { get; }
    public float BaseValue { get; set; }
    public float CurrentValue { get; private set; }
    public void Recalculate() => CurrentValue = (BaseValue + _additive) * (1f + _multiplicative);
    // Add/Remove additive and multiplicative methods...
}
```

```csharp
public sealed class DefaultGameplayTagSet : IGameplayTagSet
{
    private readonly Dictionary<GameplayTag, int> _counts = new();
    public void AddTag(GameplayTag tag) => _counts[tag] = GetTagCount(tag) + 1;
    public void RemoveTag(GameplayTag tag) { /* decrement and cleanup */ }
    public bool HasTag(GameplayTag tag) => GetTagCount(tag) > 0;
    public int GetTagCount(GameplayTag tag) => _counts.TryGetValue(tag, out var count) ? count : 0;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test UnityProject/Assets/Change/Framework/Tests/EditMode --filter "DefaultAttributeSet_|DefaultGameplayTagSet_"`
Expected: PASS for both tests.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Gas/DefaultAttribute.cs \
        UnityProject/Assets/Change/Framework/Gas/DefaultAttributeSet.cs \
        UnityProject/Assets/Change/Framework/Gas/DefaultGameplayTagSet.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/DefaultAbilitySystemTests.cs
git commit -m "feat(gas): add default attribute and gameplay tag implementations"
```

---

### Task 2: Implement Ability System Aggregate and Core Tick Loop

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Gas/DefaultAbilitySystem.cs`
- Modify: `UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/DefaultAbilitySystemTests.cs`

- [ ] **Step 1: Write failing tests for ability/modifier/trigger registration and tick**

```csharp
[Test]
public void DefaultAbilitySystem_AddAbility_DuplicateIdThrows()
{
    var system = new DefaultAbilitySystem("E1", 1);
    var ability = new TestAbility("A1");
    system.AddAbility(ability);
    Assert.Throws<InvalidOperationException>(() => system.AddAbility(new TestAbility("A1")));
}

[Test]
public void DefaultAbilitySystem_Tick_AdvancesAbilitiesModifiersAndTriggers()
{
    var system = new DefaultAbilitySystem("E1", 1);
    var ability = new TestAbility("A1");
    var modifier = new TestModifier("M1");
    var trigger = new TestTrigger(TriggerEventType.OnTakeDamage);
    system.AddAbility(ability);
    system.AddModifier(modifier);
    system.AddTrigger(trigger);
    system.Tick(0.5f);
    Assert.AreEqual(1, ability.TickCount);
    Assert.AreEqual(1, modifier.TickCount);
    Assert.AreEqual(1, trigger.TickCount);
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test UnityProject/Assets/Change/Framework/Tests/EditMode --filter DefaultAbilitySystem_`
Expected: FAIL with missing `DefaultAbilitySystem`.

- [ ] **Step 3: Implement `DefaultAbilitySystem`**

```csharp
public sealed class DefaultAbilitySystem : IAbilitySystem
{
    private readonly Dictionary<string, IGameplayAbility> _abilities = new();
    private readonly Dictionary<string, IModifier> _modifiers = new();
    private readonly Dictionary<TriggerEventType, List<ITrigger>> _triggers = new();
    public void Tick(float deltaTime)
    {
        if (deltaTime < 0f) throw new InvalidOperationException("deltaTime must be non-negative.");
        foreach (var ability in _abilities.Values) ability.Tick(deltaTime);
        foreach (var modifier in _modifiers.Values) modifier.OnTick(this, deltaTime);
        foreach (var list in _triggers.Values) foreach (var trigger in list) trigger.TickCooldown(deltaTime);
    }
    // add/remove/get members...
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test UnityProject/Assets/Change/Framework/Tests/EditMode --filter DefaultAbilitySystem_ -v minimal`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Gas/DefaultAbilitySystem.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/DefaultAbilitySystemTests.cs
git commit -m "feat(gas): add default ability system aggregate with tick loop"
```

---

### Task 3: Implement Ability Lifecycle Base Classes

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Gas/BaseGameplayAbility.cs`
- Create: `UnityProject/Assets/Change/Framework/Gas/ActiveGameplayAbility.cs`
- Create: `UnityProject/Assets/Change/Framework/Gas/PassiveGameplayAbility.cs`
- Create: `UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/AbilityLifecycleTests.cs`

- [ ] **Step 1: Write failing lifecycle tests (cooldown/charges/state transitions)**

```csharp
[Test]
public void ActiveAbility_Activate_ConsumesChargeAndStartsCooldown()
{
    var ability = new TestActiveAbility("Fireball", cooldown: 2f, maxCharges: 2);
    var source = TestAbilitySystemFactory.Create();
    ability.Activate(source, Array.Empty<IAbilitySystem>());
    Assert.AreEqual(1, ability.CurrentCharges);
    Assert.AreEqual(AbilityState.Cooldown, ability.State);
}

[Test]
public void ActiveAbility_Tick_CooldownEnds_ReturnsReady()
{
    var ability = new TestActiveAbility("Fireball", cooldown: 1f, maxCharges: 1);
    var source = TestAbilitySystemFactory.Create();
    ability.Activate(source, Array.Empty<IAbilitySystem>());
    ability.Tick(1.1f);
    Assert.AreEqual(AbilityState.Ready, ability.State);
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test UnityProject/Assets/Change/Framework/Tests/EditMode --filter ActiveAbility_`
Expected: FAIL with missing base ability implementations.

- [ ] **Step 3: Implement base ability classes**

```csharp
public abstract class BaseGameplayAbility : IGameplayAbility
{
    private float _cooldownRemaining;
    public AbilityState State { get; protected set; } = AbilityState.Ready;
    public int CurrentCharges { get; protected set; }

    public virtual void Activate(IAbilitySystem source, IAbilitySystem[] targets)
    {
        if (!CanActivate(source)) throw new InvalidOperationException($"Ability {Id} cannot activate.");
        CurrentCharges--;
        Execute(source, targets);
        StartCooldown();
    }

    protected abstract void Execute(IAbilitySystem source, IAbilitySystem[] targets);
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test UnityProject/Assets/Change/Framework/Tests/EditMode --filter "ActiveAbility_|AbilityLifecycle"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Gas/BaseGameplayAbility.cs \
        UnityProject/Assets/Change/Framework/Gas/ActiveGameplayAbility.cs \
        UnityProject/Assets/Change/Framework/Gas/PassiveGameplayAbility.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/AbilityLifecycleTests.cs
git commit -m "feat(gas): add base gameplay ability lifecycle and cooldown handling"
```

---

### Task 4: Implement Base Modifier and Base Trigger with Cascade Guard

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Gas/BaseModifier.cs`
- Create: `UnityProject/Assets/Change/Framework/Gas/BaseTrigger.cs`
- Create: `UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/TriggerCascadeTests.cs`

- [ ] **Step 1: Write failing tests for modifier duration and trigger cascade limit**

```csharp
[Test]
public void BaseModifier_OnTick_ExpiresAfterDuration()
{
    var system = TestAbilitySystemFactory.Create();
    var modifier = new TestDurationModifier("Burn", duration: 1f);
    modifier.OnApply(system);
    modifier.OnTick(system, 1.1f);
    Assert.IsTrue(modifier.IsExpired);
}

[Test]
public void BaseTrigger_TryFire_StopsAtCascadeDepthLimit()
{
    var trigger = new TestRecursiveTrigger(maxDepth: 2);
    var source = TestAbilitySystemFactory.Create();
    var target = TestAbilitySystemFactory.Create();
    var fired = trigger.TryFire(source, target, cascadeDepth: 3);
    Assert.IsFalse(fired);
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test UnityProject/Assets/Change/Framework/Tests/EditMode --filter "BaseModifier_|BaseTrigger_"`
Expected: FAIL due to missing base classes.

- [ ] **Step 3: Implement minimal lifecycle behavior**

```csharp
public abstract class BaseModifier : IModifier
{
    private float _remainingDuration;
    public bool IsExpired { get; private set; }
    public virtual void OnTick(IAbilitySystem target, float deltaTime)
    {
        if (IsExpired) return;
        _remainingDuration -= deltaTime;
        if (_remainingDuration <= 0f) IsExpired = true;
    }
}
```

```csharp
public abstract class BaseTrigger : ITrigger
{
    protected const int MaxCascadeDepth = 8;
    public bool TryFire(IAbilitySystem source, IAbilitySystem target, int cascadeDepth)
    {
        if (cascadeDepth > MaxCascadeDepth) return false;
        if (!EvaluateCondition(source, target)) return false;
        ExecuteEffects(source, target, cascadeDepth);
        return true;
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test UnityProject/Assets/Change/Framework/Tests/EditMode --filter "TriggerCascade|BaseModifier"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Gas/BaseModifier.cs \
        UnityProject/Assets/Change/Framework/Gas/BaseTrigger.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/TriggerCascadeTests.cs
git commit -m "feat(gas): add base modifier/trigger lifecycle with cascade guard"
```

---

### Task 5: Implement Default Target Resolvers

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Gas/DefaultTargetResolvers.cs`
- Create: `UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/TargetResolverTests.cs`

- [ ] **Step 1: Write failing resolver tests**

```csharp
[Test]
public void AllEnemiesResolver_ReturnsEntitiesFromDifferentTeam()
{
    var source = TestAbilitySystemFactory.Create(teamId: 1);
    var ally = TestAbilitySystemFactory.Create(teamId: 1);
    var enemyA = TestAbilitySystemFactory.Create(teamId: 2);
    var enemyB = TestAbilitySystemFactory.Create(teamId: 2);
    var resolver = new AllEnemiesTargetResolver();
    var result = resolver.Resolve(source, new[] { source, ally, enemyA, enemyB });
    Assert.AreEqual(2, result.Length);
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test UnityProject/Assets/Change/Framework/Tests/EditMode --filter Resolver_`
Expected: FAIL with missing resolver types.

- [ ] **Step 3: Implement resolvers**

```csharp
public sealed class AllEnemiesTargetResolver : ITargetResolver
{
    public TargetType Type => TargetType.AllEnemies;
    public IAbilitySystem[] Resolve(IAbilitySystem source, IAbilitySystem[] allEntities)
        => allEntities.Where(entity => entity.TeamId != source.TeamId).ToArray();
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test UnityProject/Assets/Change/Framework/Tests/EditMode --filter "Resolver_|TargetResolver"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Gas/DefaultTargetResolvers.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Gas/TargetResolverTests.cs
git commit -m "feat(gas): add default target resolver implementations"
```

---

### Task 6: Add Template Registry and Source Abstractions in GameScript

**Files:**
- Create: `UnityProject/Assets/GameScript/GasTemplate/ITemplateSource.cs`
- Create: `UnityProject/Assets/GameScript/GasTemplate/TemplateBuildContext.cs`
- Create: `UnityProject/Assets/GameScript/GasTemplate/AbilityTemplateRegistry.cs`

- [ ] **Step 1: Write failing tests for duplicate/missing template behavior**

```csharp
[Test]
public void AbilityTemplateRegistry_RegisterDuplicateKey_Throws()
{
    var registry = new AbilityTemplateRegistry();
    registry.Register("demo.fireball", ctx => new DemoFireballAbility());
    Assert.Throws<InvalidOperationException>(() =>
        registry.Register("demo.fireball", ctx => new DemoFireballAbility()));
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test UnityProject/Assets/Change/Runtime/Tests/EditMode --filter AbilityTemplateRegistry_`
Expected: FAIL (type missing).

- [ ] **Step 3: Implement registry/source/build context**

```csharp
public interface ITemplateSource
{
    void Register(AbilityTemplateRegistry registry);
}

public sealed class AbilityTemplateRegistry
{
    private readonly Dictionary<string, Func<TemplateBuildContext, IGameplayAbility>> _abilityFactories = new();
    public void Register(string key, Func<TemplateBuildContext, IGameplayAbility> factory)
    {
        if (_abilityFactories.ContainsKey(key))
            throw new InvalidOperationException($"Duplicate template key: {key}");
        _abilityFactories.Add(key, factory);
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test UnityProject/Assets/Change/Runtime/Tests/EditMode --filter AbilityTemplateRegistry_ -v minimal`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/GameScript/GasTemplate/ITemplateSource.cs \
        UnityProject/Assets/GameScript/GasTemplate/TemplateBuildContext.cs \
        UnityProject/Assets/GameScript/GasTemplate/AbilityTemplateRegistry.cs
git commit -m "feat(gas-template): add template source abstraction and registry"
```

---

### Task 7: Implement Demo Effects, Modifiers, Triggers, and Scenario Builder

**Files:**
- Create: `UnityProject/Assets/GameScript/GasTemplate/Demo/DemoEntities.cs`
- Create: `UnityProject/Assets/GameScript/GasTemplate/Demo/DemoEffects.cs`
- Create: `UnityProject/Assets/GameScript/GasTemplate/Demo/DemoModifiers.cs`
- Create: `UnityProject/Assets/GameScript/GasTemplate/Demo/DemoTriggers.cs`
- Create: `UnityProject/Assets/GameScript/GasTemplate/Demo/DemoTemplateSource.cs`
- Create: `UnityProject/Assets/GameScript/GasTemplate/Demo/DemoScenarioBuilder.cs`
- Create: `UnityProject/Assets/GameScript/GasTemplate/BattleSimulationEvent.cs`
- Create: `UnityProject/Assets/GameScript/GasTemplate/BattleSimulationReport.cs`

- [ ] **Step 1: Write failing integration test for one full cast chain**

```csharp
[Test]
public void DemoScenario_FireballCast_ProducesDamageAndBurnTick()
{
    var builder = new DemoScenarioBuilder();
    var scenario = builder.Build();
    scenario.Cast("demo.fireball", "Mage", "Warrior");
    scenario.Tick(1.0f);
    var report = scenario.Report;
    Assert.IsTrue(report.Events.Any(e => e.Kind == BattleEventKind.AbilityActivated));
    Assert.IsTrue(report.Events.Any(e => e.Kind == BattleEventKind.ModifierApplied));
    Assert.Less(report.GetSnapshot("Warrior").CurrentHp, report.GetSnapshot("Warrior").MaxHp);
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test UnityProject/Assets/Change/Runtime/Tests/EditMode --filter DemoScenario_FireballCast_ProducesDamageAndBurnTick`
Expected: FAIL (missing scenario/demo types).

- [ ] **Step 3: Implement demo components with one clear chain**

```csharp
public sealed class FireballDamageEffect : IGameplayEffect
{
    public void Execute(in EffectContext context)
    {
        context.Target.Attributes.ModifyCurrent("HP", -40f);
    }
}

public sealed class BurnModifier : BaseModifier
{
    protected override void OnPeriodicTick(IAbilitySystem target, float deltaTime)
    {
        target.Attributes.ModifyCurrent("HP", -5f);
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test UnityProject/Assets/Change/Runtime/Tests/EditMode --filter "DemoScenario_|GasTemplateIntegration"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/GameScript/GasTemplate/Demo/DemoEntities.cs \
        UnityProject/Assets/GameScript/GasTemplate/Demo/DemoEffects.cs \
        UnityProject/Assets/GameScript/GasTemplate/Demo/DemoModifiers.cs \
        UnityProject/Assets/GameScript/GasTemplate/Demo/DemoTriggers.cs \
        UnityProject/Assets/GameScript/GasTemplate/Demo/DemoTemplateSource.cs \
        UnityProject/Assets/GameScript/GasTemplate/Demo/DemoScenarioBuilder.cs \
        UnityProject/Assets/GameScript/GasTemplate/BattleSimulationEvent.cs \
        UnityProject/Assets/GameScript/GasTemplate/BattleSimulationReport.cs
git commit -m "feat(gas-template): add complete demo scenario chain and reporting"
```

---

### Task 8: Implement Dual Entrypoints (Pure Runner + MonoBehaviour)

**Files:**
- Create: `UnityProject/Assets/GameScript/GasTemplate/Entry/GasTemplateRunner.cs`
- Create: `UnityProject/Assets/GameScript/GasTemplate/Entry/GasTemplateBehaviour.cs`
- Modify: `UnityProject/Assets/Change/Runtime/Tests/EditMode/GasTemplate/GasTemplateIntegrationTests.cs`

- [ ] **Step 1: Write failing tests ensuring both entrypoints share scenario logic**

```csharp
[Test]
public void GasTemplateRunner_Run_ReturnsDeterministicReport()
{
    var reportA = GasTemplateRunner.RunDefault();
    var reportB = GasTemplateRunner.RunDefault();
    Assert.AreEqual(reportA.Events.Count, reportB.Events.Count);
    Assert.AreEqual(reportA.GetSnapshot("Warrior").CurrentHp, reportB.GetSnapshot("Warrior").CurrentHp, 0.001f);
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test UnityProject/Assets/Change/Runtime/Tests/EditMode --filter GasTemplateRunner_`
Expected: FAIL (runner missing).

- [ ] **Step 3: Implement runner and behavior wrappers**

```csharp
public static class GasTemplateRunner
{
    public static BattleSimulationReport RunDefault()
    {
        var scenario = new DemoScenarioBuilder().Build();
        scenario.Cast("demo.fireball", "Mage", "Warrior");
        scenario.Tick(2.0f);
        return scenario.Report;
    }
}
```

```csharp
public sealed class GasTemplateBehaviour : MonoBehaviour
{
    private DemoScenario _scenario;
    private void Start() => _scenario = new DemoScenarioBuilder().Build();
    private void Update() => _scenario.Tick(Time.deltaTime);
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test UnityProject/Assets/Change/Runtime/Tests/EditMode --filter "GasTemplateRunner_|GasTemplateIntegration"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/GameScript/GasTemplate/Entry/GasTemplateRunner.cs \
        UnityProject/Assets/GameScript/GasTemplate/Entry/GasTemplateBehaviour.cs \
        UnityProject/Assets/Change/Runtime/Tests/EditMode/GasTemplate/GasTemplateIntegrationTests.cs
git commit -m "feat(gas-template): add pure-code runner and MonoBehaviour entrypoints"
```

---

### Task 9: Full Verification and Documentation Sync

**Files:**
- Modify: `docs/superpowers/plans/2026-05-07-gas-complete-template-implementation-plan.md` (checklist completion marks only)
- Optional Modify: `docs/superpowers/specs/2026-05-07-gas-complete-template-design.md` (only if acceptance wording needs small sync)

- [ ] **Step 1: Run full framework + runtime test suites**

Run:
`dotnet test UnityProject/Assets/Change/Framework/Tests/EditMode -v minimal`

Run:
`dotnet test UnityProject/Assets/Change/Runtime/Tests/EditMode -v minimal`

Expected: PASS with all new GAS and template tests green.

- [ ] **Step 2: Perform manual Unity runtime sanity check**

Run Unity and attach `GasTemplateBehaviour` in a sandbox scene.
Expected:
- logs include `AbilityActivated`, `ModifierApplied`, `TriggerFired` sequence,
- no runtime exceptions in Console.

- [ ] **Step 3: Update checklist statuses in plan and stage only relevant files**

```bash
git add UnityProject/Assets/Change/Framework/Gas \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Gas \
        UnityProject/Assets/GameScript/GasTemplate \
        UnityProject/Assets/Change/Runtime/Tests/EditMode/GasTemplate
```

- [ ] **Step 4: Final commit for verification/documentation sync**

```bash
git commit -m "test(gas-template): verify end-to-end template flow and coverage"
```

- [ ] **Step 5: Prepare PR summary with acceptance criteria mapping**

Include:
1. Which commit satisfies each design acceptance criterion.
2. Test command outputs and manual run notes.
3. Remaining follow-up for ScriptableObject/config source implementation.

---

## Self-Review Checklist (completed)

1. **Spec coverage:** All approved spec sections map to tasks:
   - Core implementations -> Tasks 1-5
   - Template assembly and extension boundary -> Task 6
   - Demo chain and observability -> Task 7
   - Dual entrypoints -> Task 8
   - Verification gates -> Task 9
2. **Placeholder scan:** No `TBD`/`TODO` placeholders or vague "do later" steps.
3. **Type consistency:** `AbilityTemplateRegistry`, `TemplateBuildContext`, `BattleSimulationReport`, and dual entrypoint names are consistent across tasks.
