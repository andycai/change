---
module_name: "Gas"
directory: "UnityProject/Assets/Change/Runtime/Gas"
type: "Library"
confidence: 0.8
keywords:
  - "gas"
  - "gameplay"
  - "ability"
  - "system"
  - "modifier"
  - "effect"
  - "trigger"
  - "targeting"
dependencies:
  - "Change.Framework.Gas"
  - "Change.Framework.Collections"
  - "Change.Framework.Cqrs"
description: "Gameplay Ability System (GAS) — a runtime library providing ability activation with casting/cooldown/charges, attribute management with modifiers, hierarchical gameplay tags, reactive triggers with scope filtering, and a suite of composable effects (damage, heal, buff/debuff, cost, cooldown)."
last_updated: "2026-06-22 11:34:00"
---

# Gas

## 概述

Gas (Gameplay Ability System) 是一个运行时游戏能力系统库，提供技能激活与管理、属性与修饰器、层级标签、触发器引擎以及可组合的效果系统。它支持施法时间、冷却、充能次数等技能状态机，以及修饰器的叠加/刷新/替换策略。

## 公共接口

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-START -->

### Core (`Change.Runtime.Gas`)

| 类型 | 签名 | 说明 |
|------|------|------|
| `AbilitySystem` | `class : IAbilitySystem` | 核心系统，管理技能、修饰器、触发器，驱动 Tick 更新 |
| `AbilitySystem(string entityId, int teamId = 0)` | 构造 | 创建实体能力系统，初始化属性和标签集 |
| `AbilitySystem.AddAbility(IGameplayAbility)` | 方法 | 注册技能 |
| `AbilitySystem.GetAbility(string abilityId)` | 方法 | 按 ID 获取技能 |
| `AbilitySystem.AddModifier(IModifier)` | 方法 | 添加修饰器，按叠加规则处理冲突 |
| `AbilitySystem.RemoveModifier(string modifierId)` | 方法 | 移除指定修饰器 |
| `AbilitySystem.RemoveModifierByTag(GameplayTag)` | 方法 | 按标签移除所有匹配修饰器 |
| `AbilitySystem.AddTrigger(ITrigger)` | 方法 | 注册触发器 |
| `AbilitySystem.GetTriggers(TriggerEventType)` | 方法 | 按事件类型获取触发器列表 |
| `AbilitySystem.Tick(float deltaTime)` | 方法 | 每帧更新：过期修饰器清理、技能 Tick、触发器冷却 |
| `GameplayAbility` | `class : IGameplayAbility` | 技能实例，管理状态机 (Ready/Casting/Executing/Cooldown) |
| `GameplayAbility(AbilityConfig config)` | 构造 | 从配置创建技能，验证配置并初始化效果列表 |
| `GameplayAbility.CanActivate(IAbilitySystem source)` | 方法 | 检查源是否满足激活条件（状态、消耗、阻挡标签） |
| `GameplayAbility.Activate(IAbilitySystem source, IAbilitySystem[] targets)` | 方法 | 激活技能，支持施法时间延迟执行 |
| `GameplayAbility.Tick(float deltaTime)` | 方法 | 驱动施法计时器和冷却计时器 |
| `AbilityConfig` | `class` | 技能配置：ID、类型、激活方式、冷却、充能、消耗、标签、效果列表 |
| `AbilityConfig.Validate()` | 方法 | 校验配置合法性 |
| `AttributeSet` | `class : IAttributeSet` | 属性容器，使用 FastDictionary 存储 |
| `AttributeSet.GetAttribute(string name)` | 方法 | 获取属性对象 |
| `AttributeSet.GetCurrentValue(string name)` | 方法 | 获取属性当前值 |
| `AttributeSet.SetBaseValue(string name, float value)` | 方法 | 设置基础值，触发 OnAttributeChanged 事件 |
| `Attribute` | `class : IAttribute` | 单个属性，维护 BaseValue、CurrentValue 和修饰器列表 |
| `Modifier` | `class : IModifier` | 修饰器（Buff/Debuff），支持叠加规则和持续时间 |
| `Modifier.OnApply(IAbilitySystem target)` | 方法 | 应用时：授予标签 + 执行 ApplyEffects |
| `Modifier.OnTick(IAbilitySystem target, float deltaTime)` | 方法 | 每帧：累加时间 + 按间隔执行 TickEffects |
| `Modifier.OnRemove(IAbilitySystem target)` | 方法 | 移除时：撤销标签 + 执行 RemoveEffects |
| `Modifier.AddStack()` | 方法 | 叠加层数（不超过 MaxStack） |
| `Modifier.RefreshDuration()` | 方法 | 刷新持续时间（用于 Refresh 叠加规则） |
| `ModifierConfig` | `class` | 修饰器配置：ID、极性、叠加规则、持续时间、标签、效果列表 |
| `GameplayTagSet` | `class : IGameplayTagSet` | 层级标签集，支持引用计数和层级缓存 |
| `GameplayTagSet.AddTag(GameplayTag)` | 方法 | 添加标签及所有父层级标签 |
| `GameplayTagSet.RemoveTag(GameplayTag)` | 方法 | 移除标签及所有父层级标签 |
| `GameplayTagSet.HasTag(GameplayTag)` | 方法 | 检查是否拥有标签 |
| `GameplayTagSet.GetTagCount(GameplayTag)` | 方法 | 获取标签引用计数 |
| `Trigger` | `class : ITrigger` | 触发器：绑定事件类型、条件标签、响应效果和冷却 |
| `TriggerEngine` | `class` | 触发器调度引擎，支持级联深度限制和范围过滤 |
| `TriggerEngine.DispatchEvent(TriggerEventType, IAbilitySystem source, IAbilitySystem target, IAbilitySystem[] allEntities, int cascadeDepth)` | 方法 | 向所有实体分发事件，按 Scope 过滤匹配的触发器 |

### Effects (`Change.Runtime.Gas.Effects`)

| 类型 | 签名 | 说明 |
|------|------|------|
| `AttributeModifyEffect` | `class : IGameplayEffect` | 属性修改效果，支持固定值 + 属性缩放 |
| `ApplyModifierEffect` | `class : IGameplayEffect` | 向目标施加修饰器 |
| `ChanceEffect` | `class : IGameplayEffect` | 概率效果包装器，按几率执行子效果 |
| `CooldownEffect` | `class : IGameplayEffect` | 冷却效果，触发技能的 StartCooldown |
| `CostEffect` | `class : IGameplayEffect` | 消耗效果，从源扣除属性值 |
| `DamageEffect` | `class : IGameplayEffect` | 伤害效果（继承 AttributeModifyEffect） |
| `HealEffect` | `class : IGameplayEffect` | 治疗效果（继承 AttributeModifyEffect） |
| `RemoveModifierEffect` | `class : IGameplayEffect` | 从目标移除指定修饰器 |
| `TagEffect` | `class : IGameplayEffect` | 标签效果，向目标添加或移除标签 |

### Cqrs (`Change.Runtime.Gas.Cqrs`)

| 类型 | 签名 | 说明 |
|------|------|------|
| `ApplyModifierCmd` | `class : ICommand` | 施加修饰器命令 |
| `CastAbilityCmd` | `class : ICommand` | 施放技能命令 |
| `AttributeChangedEvt` | `class : IEvent` | 属性变化事件 |
| `DamageAppliedEvt` | `class : IEvent` | 伤害施加事件 |
| `HealAppliedEvt` | `class : IEvent` | 治疗施加事件 |
| `ModifierAppliedEvt` | `class : IEvent` | 修饰器施加事件 |
| `ModifierExpiredEvt` | `class : IEvent` | 修饰器过期事件 |
| `GetAttributeValueQry` | `class : IQuery` | 查询属性值 |
| `HasTagQry` | `class : IQuery` | 查询标签是否存在 |

### Targeting (`Change.Runtime.Gas.Targeting`)

| 类型 | 签名 | 说明 |
|------|------|------|
| `SelfTargetResolver` | `class : ITargetResolver` | 自身目标解析器 |
| `AllyTargetResolver` | `class : ITargetResolver` | 友方目标解析器 |
| `EnemyTargetResolver` | `class : ITargetResolver` | 敌方目标解析器 |
| `AoETargetResolver` | `class : ITargetResolver` | 范围目标解析器，支持按 TargetType 过滤 |

<!-- AUTO-GENERATED-END -->

## 依赖关系

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-DEPS-START -->

| 依赖模块 | 用途 |
|----------|------|
| `Change.Framework.Gas` | 框架层接口定义：`IAbilitySystem`、`IGameplayAbility`、`IGameplayEffect`、`IAttributeSet`、`IAttribute`、`IModifier`、`IGameplayTagSet`、`ITrigger`、`ITargetResolver`、`EffectContext`、`GameplayTag`、枚举类型 |
| `Change.Framework.Collections` | `FastDictionary<K,V>` 用于 `AttributeSet` 的高性能属性存储 |
| `Change.Framework.Cqrs` | CQRS 基类：`ICommand`、`IEvent`、`IQuery`，用于 Gas.Cqrs 子命名空间的命令/查询/事件 |

<!-- AUTO-GENERATED-DEPS-END -->

## 使用场景

- **技能系统**：通过 `AbilityConfig` 配置技能属性，创建 `GameplayAbility` 实例并注册到 `AbilitySystem`
- **Buff/Debuff**：通过 `ModifierConfig` 配置修饰器，使用 `ApplyModifierEffect` 施加到目标
- **伤害/治疗**：使用 `DamageEffect` / `HealEffect` 进行属性修改，支持属性缩放（如攻击力加成）
- **触发器**：配置 `Trigger` 响应特定事件（如受伤时触发反击），由 `TriggerEngine` 统一调度
- **CQRS 集成**：通过 `CastAbilityCmd`、`ApplyModifierCmd` 等命令解耦技能调用

## 注意事项

- `TriggerEngine.DispatchEvent` 有级联深度限制（默认 5），防止无限递归触发
- `GameplayTagSet` 使用全局静态 `HierarchyCache`，注意标签层级的内存管理
- `Modifier.OnTick` 每帧最多执行 10 次 tick 效果，防止帧率过低时的雪崩
- `AttributeSet` 使用 `FastDictionary` 而非标准 `Dictionary`，依赖 `Change.Framework.Collections`

## 经验教训

<!-- 由 /record-lesson 自动追加，请勿手工编辑此标题 -->
