# Change.Framework GAS Complete Template Design

- Date: 2026-05-07
- Project: `UnityProject/Assets/Change/Framework/Gas` and `UnityProject/Assets/GameScript`
- Scope: Deliver a complete, reusable GAS template implementation with GameScript demo integration
- Status: Approved design baseline for planning

## 1. Background and Problem Statement

The current `Change.Framework.Gas` module only provides interfaces and enums. There is no reusable concrete implementation and no end-to-end sample under `GameScript` to demonstrate how to compose an ability system, run battle flow, and integrate with Unity runtime loop.

User-selected direction:

1. Build a complete template close to production reuse level.
2. Provide dual entrypoints:
   - pure code runner for deterministic execution and test usage,
   - `MonoBehaviour` runner for scene-level demonstration.
3. Use code-first declarative template registration now, while reserving extension points for future config-driven sources.

## 2. Design Goals and Non-goals

### 2.1 Goals

1. Provide business-agnostic concrete GAS core implementations under `Change/Framework/Gas`.
2. Keep module boundaries clear: framework handles mechanics; `GameScript` handles template assembly and sample scenarios.
3. Deliver a complete, readable battle chain that includes attributes, tags, abilities, effects, modifiers, triggers, target resolution, cooldown, and tick.
4. Keep fail-fast behavior consistent with framework conventions.
5. Ensure future compatibility with ScriptableObject/config-table driven template sources without redesigning runtime core.

### 2.2 Non-goals

1. Introduce ScriptableObject or external table loading in this iteration.
2. Add networking, prediction, rollback, or deterministic lockstep concerns.
3. Implement editor tooling for template authoring.
4. Expand into a full combat game domain model beyond demonstration templates.

## 3. Architecture Overview

Adopt a layered split:

1. `Change.Framework.Gas` (core mechanics, reusable and business-agnostic)
2. `GameScript/GasTemplate` (template assembly, scenario composition, and outputs)
3. `GameScript/GasTemplate/Demo` (example units and complete ability chains)
4. `GameScript/GasTemplate/Entry` (pure-code and `MonoBehaviour` entrypoints)

Key principle: framework internals are reusable across domains; demo domain logic and sample orchestration remain outside framework.

## 4. Component Design

### 4.1 Framework Core Implementations (`Change/Framework/Gas`)

1. `DefaultAbilitySystem` (`IAbilitySystem`)
   - Owns abilities, modifiers, triggers, attributes, and tags.
   - Implements lifecycle operations (`AddAbility`, `AddModifier`, `RemoveModifier`, `Tick`, trigger retrieval).
   - Enforces duplicate id checks and fail-fast exceptions.
2. `DefaultAttribute` and `DefaultAttributeSet`
   - Implements additive and multiplicative stacks with explicit `Recalculate()`.
   - Raises `OnAttributeChanged` only when resulting values actually change.
3. `DefaultGameplayTagSet`
   - Maintains tag counts and supports add/remove/has/count semantics.
4. Ability base classes
   - `BaseGameplayAbility`: common state machine, cooldown and charge handling.
   - `ActiveGameplayAbility`: explicit manual activation path.
   - `PassiveGameplayAbility`: event-driven activation path via trigger integration.
5. Modifier base class
   - `BaseModifier`: duration, stacking policy, expiration state, tag grant/revoke, periodic tick hook.
6. Trigger base class
   - `BaseTrigger`: condition evaluation, cooldown handling, `TryFire` orchestration, cascade depth guard.
7. Target resolver implementations
   - Concrete resolvers for `Self`, `Enemy`, `Ally`, `AllEnemies`, `AllAllies`.

### 4.2 Template Assembly Layer (`GameScript/GasTemplate`)

1. `AbilityTemplateRegistry`
   - Declaratively registers templates by key.
   - Builds abilities/effects/modifiers/triggers through factory delegates.
   - Enforces missing/duplicate template fail-fast behavior.
2. `ITemplateSource` (extension boundary)
   - Abstraction for template data source.
   - Initial implementation is in-memory code registration.
   - Future ScriptableObject or generated config source can implement this interface.
3. `TemplateBuildContext`
   - Carries runtime dependencies required for template construction.
   - Avoids coupling builders to Unity-specific APIs.

### 4.3 Demo Layer (`GameScript/GasTemplate/Demo`)

Provide a complete sample battle chain:

1. Two demo entities (for example `Warrior` and `Mage`) with explicit attributes and tags.
2. At least one active ability (direct damage + follow-up modifier application).
3. At least one passive/triggered ability (for example counter trigger on `OnTakeDamage`).
4. At least one ticking modifier (for example burn damage over time).
5. Demonstration of target resolver behavior and event-trigger interactions.

### 4.4 Dual Entrypoints (`GameScript/GasTemplate/Entry`)

1. Pure-code runner (`GasTemplateRunner`)
   - Deterministic execution method (for example `Run()`).
   - Returns a structured simulation report object for assertions and diagnostics.
2. Unity runner (`GasTemplateBehaviour : MonoBehaviour`)
   - Boots the same scenario in `Start`.
   - Advances simulation in `Update(deltaTime)`.
   - Emits readable logs for manual verification.

Both entrypoints must share the same scenario assembly logic to avoid divergence.

## 5. Data Flow

For an active cast sequence:

1. Entrypoint requests ability activation from source system.
2. Ability validates activatability (`state`, `cooldown`, `charges`, tag constraints).
3. Resolver determines targets.
4. Ability executes ordered effect chain with `EffectContext`.
5. Ability systems publish corresponding trigger events (`OnAbilityCast`, `OnTakeDamage`, etc.).
6. Matching triggers evaluate and fire with cooldown + cascade depth guard.
7. Periodic `Tick` updates:
   - ability cooldowns and charge recovery,
   - modifier lifetime and periodic effects,
   - trigger cooldown progression.

## 6. Error Handling and Fail-fast Rules

Framework and template layers must fail fast using `InvalidOperationException` for programming/configuration errors:

1. Missing ability/template/attribute references.
2. Duplicate registration of ability id, modifier id, or template key.
3. Illegal runtime inputs (for example negative `deltaTime`).
4. Invalid state transitions in ability lifecycle.

Trigger cascade protection:

- Define a maximum cascade depth constant.
- Stop additional cascade execution when depth limit is reached.
- Record a diagnostic warning event in simulation output while keeping the main loop alive.

## 7. Observability and Diagnostics

Define lightweight simulation events consumed by `GameScript`:

1. `AbilityActivated`
2. `EffectExecuted`
3. `ModifierApplied`
4. `ModifierExpired`
5. `TriggerFired`
6. `CascadeDepthLimited`

Pure runner returns a `BattleSimulationReport` containing ordered events and final entity snapshots.
`MonoBehaviour` runner only formats and outputs these events; logging implementation stays outside framework core.

## 8. Testing Strategy

### 8.1 Framework EditMode Tests

1. Ability state transitions, cooldown, and charges.
2. Attribute recalculation and change event emission.
3. Modifier stacking and expiration behavior.
4. Trigger cooldown, condition checks, and cascade guard.
5. Target resolver correctness per `TargetType`.

### 8.2 Template/Demo Integration Tests

1. Registry can build a complete demo entity loadout.
2. End-to-end cast chain produces expected HP and event sequence.
3. Tick progression produces deterministic modifier and cooldown outcomes.

### 8.3 Manual Runtime Verification

Run `GasTemplateBehaviour` in a sample scene to verify human-readable flow logs.
This is a supplement, not a replacement for deterministic edit mode tests.

## 9. File and Namespace Plan

Target namespace boundaries:

1. Framework core: `Change.Framework.Gas`
2. Template and demo: `GameScript.GasTemplate` (and sub-namespaces)

Planned high-level file placement:

1. `UnityProject/Assets/Change/Framework/Gas/`:
   - concrete core implementations and reusable base classes.
2. `UnityProject/Assets/GameScript/GasTemplate/`:
   - registry, template source abstraction, scenario builders, report models.
3. `UnityProject/Assets/GameScript/GasTemplate/Demo/`:
   - sample abilities/effects/modifiers/triggers/entities.
4. `UnityProject/Assets/GameScript/GasTemplate/Entry/`:
   - pure runner and `MonoBehaviour` runner.

## 10. Acceptance Criteria

Design is considered implemented when all are true:

1. A complete ability flow works end-to-end using framework concrete GAS implementations.
2. Both pure-code and `MonoBehaviour` entrypoints can run the same scenario.
3. Template assembly is code-first and routed through a registry abstraction.
4. Extension point exists for future non-code template sources (`ITemplateSource`) without changing runtime core behavior.
5. Core fail-fast and zero-surprise lifecycle behavior are covered by tests.
6. Demo output is deterministic in pure runner and understandable in Unity runtime logs.

## 11. Risks and Mitigations

1. Risk: framework implementation accidentally becomes demo-domain coupled.
   - Mitigation: keep all sample units/effects that imply game lore under `GameScript` namespace and folders.
2. Risk: two entrypoints diverge over time.
   - Mitigation: force both entrypoints to share one scenario assembly and one simulation engine facade.
3. Risk: trigger chains become unstable in complex cases.
   - Mitigation: strict cascade depth guard and dedicated tests for recursive trigger scenarios.
4. Risk: future config-driven migration causes rewrites.
   - Mitigation: isolate source abstraction (`ITemplateSource`) and keep registry APIs stable.

## 12. Planning Handoff

This document is the approved design baseline for the next step:

1. invoke the writing-plans workflow,
2. produce a phased implementation plan for framework core, template assembly, demo scenario, and verification gates,
3. execute implementation only after plan approval.
