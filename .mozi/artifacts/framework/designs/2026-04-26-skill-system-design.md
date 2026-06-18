# Skill System Design — GAS-Inspired Architecture

**Date:** 2026-04-26
**Game:** 5V5 Card Battle (半自动：自动战斗 + 手动技能)
**Approach:** GAS-inspired, adapted to Change framework (CQRS, FSM, zero-GC)

---

## 1. Requirements

| Dimension | Choice |
|-----------|--------|
| Game type | 5V5 card battle |
| Combat pace | Semi-auto (auto attack + manual skills) |
| Network | Single-player first, multiplayer later |
| Skill features | Damage/heal, Buff/Debuff, event triggers, skill combos |
| Configuration | JSON data + C# behavior |
| Hot-update | Behavior + data via HybridCLR |

---

## 2. Architecture Overview

Three-layer separation:

```
┌─────────────────────────────────────────────────┐
│ Change.Framework/Skill/  (Interfaces — stable)  │
│ ISkill · IModifier · IAttributeSet · ISkillTag  │
│ ITrigger · ITargetResolver · ISkillEffect       │
│ IAbilitySystem                                  │
└──────────────────────┬──────────────────────────┘
                       │ implements
┌──────────────────────▼──────────────────────────┐
│ GameScript/Skill/  (Concrete — hot-updatable)    │
│ Core/ · Effects/ · Targeting/ · Data/ · Cqrs/  │
└──────────────────────┬──────────────────────────┘
                       │ loads
┌──────────────────────▼──────────────────────────┐
│ Assets/Data/Skill/  (JSON — hot-updatable)       │
│ skills/ · modifiers/ · triggers/                │
└─────────────────────────────────────────────────┘
```

**Existing system integration:**
- **CQRS** — Commands (CastSkillCmd, ApplyModifierCmd), Events (DamageAppliedEvt, ModifierExpiredEvt, AttributeChangedEvt), Queries (GetAttributeValueQry, HasTagQry)
- **FSM** — Skill state machine: Ready → Casting → Executing → Cooldown
- **Pooling** — Reuse effect instances, damage numbers, VFX
- **Timer** — Cooldowns, buff durations, DoT tick intervals
- **Collections** — FastDict/Set for active modifiers, skill registries

---

## 3. Core Concepts

### 3.1 AbilitySystem

Central component attached to each battle entity. Manages all skill-related state:

- `AttributeSet` — entity stats (1 per entity)
- `Skill[]` — available skills (N per entity)
- `Modifier[]` — active buffs/debuffs (dynamic)
- `SkillTagSet` — current tags (dynamic)
- `Trigger[]` — registered passive triggers (N per entity)

### 3.2 Skill

An activatable ability with a lifecycle managed by FSM.

**Properties:**
- **Type:** Active | Passive
- **ActivationType:** Manual (player input) | Auto (AI) | OnEvent (trigger)
- **State:** Ready → Casting → Executing → Cooldown (FSM)
- **Cost:** ResourceType + amount (Mana, Energy, etc.)
- **Cooldown:** duration + charges (multi-charge support)
- **Tags:** categorization tags (e.g., `skill.fire`, `skill.ultimate`)
- **Target:** target resolution rule
- **Effects:** SkillEffect[] — what happens when executed

### 3.3 Modifier (Buff/Debuff)

Persistent effect on an entity with stack management.

**Properties:**
- **Polarity:** Buff | Debuff | Neutral
- **Duration:** Timed | Infinite | Stack-based
- **Stacking:** Refresh (reset duration) | AddStack (increment, up to max) | Replace (remove old, apply new) | Ignore (skip if exists)
- **GrantedTags:** tags applied to entity while modifier is active
- **Effects:** OnApply / OnTick / OnRemove effect lists
- **TickInterval:** periodic execution interval (DoT/HoT)
- **DispelRules:** priority level, tag-based dispel matching

### 3.4 AttributeSet

Character stats container. Each attribute has:
- **BaseValue** — base stat value
- **AdditiveMods** — sum of flat modifier adjustments (e.g., ATK +30)
- **MultiplicativeMods** — product of percentage modifier multipliers (e.g., ATK ×1.5)
- **CurrentValue** — computed as (BaseValue + AdditiveMods) × MultiplicativeMods
- **OnChange** — fires event when value changes (HP→0 triggers death)

Standard attributes: HP, MaxHP, ATK, DEF, SPD, CritRate, CritDmg
Resources: Mana, Energy, Rage (game-specific)

### 3.5 SkillTag

Hierarchical dot-separated categorization system.

**Format:** `category.subcategory.specific`
**Examples:**
- `skill.fire.fireball` — specific skill tag
- `buff.damage-over-time` — DoT buff tag
- `state.stunned` — state tag
- `element.fire` — element tag

**Matching:** Parent tag matches all children. `skill.fire` matches `skill.fire.fireball`.

**Uses:**
- Condition checking (immunity, requirements)
- Dispel rules (remove all `buff.negative`)
- Skill combo conditions (target has `element.wet` → lightning 2x)

### 3.6 Trigger

Event-driven passive activation mechanism.

**Properties:**
- **EventType:** which event activates this trigger
- **Condition:** additional filter (tag check, HP threshold, source check)
- **Cooldown:** internal cooldown per trigger (prevent rapid re-fire)
- **Effects:** SkillEffect[] to execute when triggered
- **Scope:** Self | Source | AllEnemies | AllAllies

### 3.7 SkillEffect

Atomic outcome of skill execution or trigger activation.

**Built-in effect types:**
- **Damage** — flat / percentage / formula-based damage. Scaling formula uses attribute references (e.g., `ATK*1.5+100`) resolved at execution time via AttributeSet query.
- **Heal** — flat / percentage / formula-based healing. Same formula resolution as Damage.
- **ApplyModifier** — grant buff/debuff to target
- **RemoveModifier** — dispel by tag/priority
- **GrantTag / RemoveTag** — direct tag manipulation
- **Chance** — conditional wrapper (rate + onSuccess/onFailure effects)
- **Conditional** — check condition, branch to effects
- **SpawnVFX** — visual effect (via object pool)

---

## 4. Skill Lifecycle

FSM states with CQRS integration:

```
Ready ──[activation request]──→ Validation ──[pass]──→ Casting ──→ Executing ──→ Cooldown ──→ Ready
                                    │                                              ↑
                                    │ [fail: cooldown/ resource/ tags/ target]      │
                                    └──────────→ Ready (no cost deducted) ─────────┘
```

1. **Ready** — Skill available. Activation from: player input, auto-battle AI, or trigger event.
2. **Validation** — Check cooldown ready, resource sufficient, no blocking tags (stunned/silenced), target valid. Fail → back to Ready, no cost deducted.
3. **Casting** — CommitCost() deducts resource. Play cast animation. Grant "casting" tag. Most card skills are instant (no cast time).
4. **Executing** — Resolve targets. Apply SkillEffect[] in order. Each effect may cascade (ApplyModifier triggers OnApply effects, which may trigger Triggers).
5. **Cooldown** — Start cooldown timer via Timer module. Supports charge-based (multiple uses before full cooldown) and flat duration. When expired → Ready.

---

## 5. Modifier Lifecycle

```
Apply Request → Immunity Check → Stack Resolution → OnApply → Active(Tick) → OnRemove
```

1. **Apply Request** — A SkillEffect wants to add a modifier to a target.
2. **Immunity Check** — Check if target has immunity tags (e.g., `immune.debuff`).
3. **Stack Resolution** — Handle existing modifier with the same **modifier ID** on target:
   - **Refresh:** Reset duration timer.
   - **AddStack:** Increment stack count (up to maxStack). Execute per-stack effects.
   - **Replace:** Remove old modifier, apply new one.
   - **Ignore:** Don't apply if already exists.
4. **OnApply** — Apply attribute modifications. Grant tags. Execute OnApply effects (e.g., initial burst damage).
5. **Active (Tick)** — Periodic effects via Timer (DoT damage, HoT healing). Attribute modifications remain active. Tags remain granted.
6. **OnRemove** — Triggered by: duration expired, manually dispelled, source died, stack count reached 0. Cleanup: revert attribute modifications, revoke granted tags, execute OnRemove effects, return to object pool.

---

## 6. Trigger System

### Event Dispatch Flow

```
Game Event → TriggerEngine → Condition Check → Execute Effects
```

All CQRS events flow through the TriggerEngine, which matches registered triggers against event types.

### Built-in Event Types

| Category | Events |
|----------|--------|
| Combat | OnDealDamage, OnTakeDamage, OnHeal, OnKill, OnDeath, OnAttack |
| Modifier | OnBuffApplied, OnBuffRemoved, OnDebuffApplied, OnDebuffRemoved, OnBuffStackChanged |
| Skill | OnSkillCast, OnSkillHit, OnSkillMiss, OnSkillCooldownEnd |
| Battle | OnBattleStart, OnTurnStart, OnTurnEnd, OnSpawn, OnHPThreshold |

### Skill Combo Patterns

1. **Chain Combo** — Skill A triggers Skill B on event. Example: Fireball hits → 30% chance → ApplyModifier(Ignite DoT).
2. **Conditional Enhance** — Active Buff modifies Skill execution. Example: "Rage" buff → Ultimate deals +50% damage. Mechanism: Skill execution checks source SkillTagSet during damage calculation.
3. **Elemental Reaction** — Target tags interact with skill element. Example: Target has `element.wet` → Lightning skill deals 2x → Remove wet tag → Apply "electrified" modifier.

### Cascade Guard

Each Trigger execution increments a cascade depth counter. **Max depth = 5** (configurable). Events exceeding max depth are logged but not processed. This prevents infinite trigger loops (A→B→C→D→E stops at depth 5).

---

## 7. Data Configuration

### SkillConfig (JSON)

```json
{
  "id": "fireball",
  "name": "火球术",
  "type": "Active",
  "activation": "Manual",
  "tags": ["skill.fire", "skill.aoe"],
  "cost": { "type": "Mana", "amount": 30 },
  "cooldown": { "duration": 3.0, "charges": 1 },
  "target": { "type": "Enemies", "count": 3 },
  "effects": [
    {
      "type": "Damage",
      "value": 200,
      "scaling": "ATK*1.5+100",
      "element": "Fire"
    },
    {
      "type": "Chance",
      "rate": 0.3,
      "onSuccess": [
        { "type": "ApplyModifier", "modifierId": "ignite" }
      ]
    }
  ]
}
```

### ModifierConfig (JSON)

```json
{
  "id": "ignite",
  "name": "点燃",
  "polarity": "Debuff",
  "tags": ["buff.dot", "buff.fire"],
  "duration": 5.0,
  "stacking": "Refresh",
  "maxStack": 1,
  "tickInterval": 1.0,
  "grantedTags": ["state.burning"],
  "onTick": [
    { "type": "Damage", "value": 50, "scaling": "ATK*0.3" }
  ]
}
```

### TriggerConfig (JSON)

```json
{
  "id": "berserker_rage",
  "eventType": "OnHPThreshold",
  "condition": {
    "operator": "Below",
    "attribute": "HP",
    "thresholdPercent": 0.3
  },
  "cooldown": 10.0,
  "effects": [
    { "type": "ApplyModifier", "modifierId": "berserker_buff" }
  ]
}
```

---

## 8. Module Structure

```
Change/Framework/Skill/          ← Interfaces (stable, not hot-updatable)
├── ISkill.cs
├── IModifier.cs
├── IAttributeSet.cs
├── ISkillTag.cs
├── ITrigger.cs
├── ITargetResolver.cs
├── ISkillEffect.cs
└── IAbilitySystem.cs

GameScript/Skill/                ← Implementations (hot-updatable via HybridCLR)
├── Core/
│   ├── AbilitySystem.cs
│   ├── Skill.cs
│   ├── Modifier.cs
│   ├── AttributeSet.cs
│   ├── SkillTagSet.cs
│   └── TriggerEngine.cs
├── Effects/
│   ├── DamageEffect.cs
│   ├── HealEffect.cs
│   ├── ApplyModifierEffect.cs
│   ├── TagEffect.cs
│   └── ChanceEffect.cs
├── Targeting/
│   ├── SelfTarget.cs
│   ├── EnemyTargetResolver.cs
│   ├── AllyTargetResolver.cs
│   └── AoETargetResolver.cs
├── Data/
│   ├── SkillConfig.cs           ← Nino serialization
│   ├── ModifierConfig.cs
│   └── TriggerConfig.cs
└── Cqrs/
    ├── CastSkillCmd.cs
    ├── ApplyModifierCmd.cs
    ├── SkillExecutedEvt.cs
    ├── DamageAppliedEvt.cs
    ├── ModifierAppliedEvt.cs
    ├── ModifierExpiredEvt.cs
    ├── AttributeChangedEvt.cs
    ├── GetAttributeValueQry.cs
    └── HasTagQry.cs

Assets/Data/Skill/               ← JSON data (hot-updatable via YooAsset)
├── skills/
├── modifiers/
└── triggers/
```

---

## 9. Hot-Update Boundary

| Layer | Location | Updatable | How |
|-------|----------|-----------|-----|
| Interfaces | `Change.Framework/Skill/` | No | Client update only |
| Implementations | `GameScript/Skill/` | Yes | HybridCLR hot patch |
| JSON data | `Assets/Data/Skill/` | Yes | YooAsset bundle update |

**Design rule:** Framework interfaces must be stable. All behavior changes go through GameScript. All numerical/content changes go through JSON. If an interface needs to change, it requires a client version update.

---

## 10. Future: Multiplayer Extension

When adding multiplayer support:

1. **CQRS commands become network messages** — CastSkillCmd sent to server for validation
2. **Server authority** — Server validates skill activation, resolves effects, broadcasts events
3. **Client prediction** — Client predicts skill execution, server corrects if needed
4. **Deterministic logic** — Skill effect resolution must be deterministic (same inputs → same outputs)
5. **State sync** — AttributeSet changes broadcast to relevant clients

The CQRS architecture naturally supports this: commands go through a transport layer, events come back. Swapping local dispatch for network dispatch is a transport change, not a logic change.

---

## 11. Out of Scope

The following are explicitly excluded from this design:

- **VFX/Audio system** — Skill system triggers VFX via events, but VFX management is a separate system
- **AI decision-making** — Auto-battle AI decides which skills to use, but AI is a separate module
- **UI presentation** — Skill panel, cooldown display, buff icons are UI concerns
- **Network implementation** — Architecture is designed for future extension, but netcode is not in scope
- **Skill tree/progression** — Character progression and skill unlocking is a separate system
