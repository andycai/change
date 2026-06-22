---
module_name: "Gas"
directory: "UnityProject/Assets/Change/Framework/Gas"
type: "Framework"
confidence: 0.8
keywords:
  - "GAS"
  - "Gameplay Ability System"
  - "abilities"
  - "modifiers"
  - "attributes"
  - "triggers"
  - "tags"
  - "buffs"
  - "debuffs"
  - "targeting"
dependencies: []
description: "Gameplay Ability System (GAS) 核心框架，提供技能、属性、修饰器（Buff/Debuff）、触发器、标签和寻敌系统的抽象接口与默认实现，是战斗系统的底层基础。"
last_updated: "2026-06-22 11:27:31"
---

# Gas

## 概述

Gas（Gameplay Ability System）是一个可扩展的战斗系统核心框架，提供了技能生命周期管理、属性计算（加法/乘法修饰器堆叠）、Buff/Debuff 修饰器（支持堆叠、刷新、替换策略）、事件驱动的触发器系统、GameplayTag 标签系统以及可组合的寻敌（Targeting）机制。所有核心概念均以接口暴露，支持自定义实现替换。

## 公共接口

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-START -->

### 核心接口

| 接口 | 说明 |
|------|------|
| `IGameplayAbility` | 技能接口：Id、Type、Activation、State、Tags、冷却/充能、CanActivate/Activate/Tick 生命周期 |
| `IAbilitySystem` | 实体能力系统：管理技能、修饰器、触发器、属性集和标签集的聚合根 |
| `IAttribute` | 属性接口：Name、BaseValue、CurrentValue、加法/乘法修饰器堆叠与重算 |
| `IAttributeSet` | 属性集接口：按名称存取属性、修改基值/当前值、属性变化事件 |
| `IGameplayEffect` | 效果接口：`void Execute(in EffectContext context)` |
| `IModifier` | 修饰器接口（Buff/Debuff）：Id、Polarity、标签授予、堆叠/过期/生命周期 |
| `ITrigger` | 触发器接口：EventType、Scope、条件评估、效果执行、冷却 Tick |
| `ITargetResolver` | 寻敌接口：Type，`IAbilitySystem[] Resolve(source, allEntities)` |
| `IGameplayTagSet` | 标签集接口：AddTag/RemoveTag/HasTag/GetTagCount（引用计数） |

### 抽象基类

| 类 | 说明 |
|------|------|
| `BaseGameplayAbility` | 技能基类：冷却计时、充能管理、状态切换的通用实现 |
| `BaseModifier` | 修饰器基类：时长追踪、过期检测、堆叠策略（Refresh/AddStack/Replace/Ignore）、虚拟扩展点 |
| `BaseTrigger` | 触发器基类：冷却管理、级联深度限制（MaxCascadeDepth=8）、参数校验、虚拟 Condition/Execute |

### 具体实现

| 类 | 说明 |
|------|------|
| `ActiveGameplayAbility` | 主动技能（AbilityType.Active），继承 BaseGameplayAbility |
| `PassiveGameplayAbility` | 被动技能（AbilityType.Passive），禁止手动激活 |
| `DefaultAbilitySystem` | 默认 IAbilitySystem 实现，Dictionary 存储技能/修饰器/触发器 |
| `DefaultAttribute` | 默认 IAttribute 实现，公式：`CurrentValue = (BaseValue + ΣAdditive) × (1 + ΣMultiplicative)` |
| `DefaultAttributeSet` | 默认 IAttributeSet 实现，Dictionary 存储，变化时触发事件 |
| `DefaultGameplayTagSet` | 默认 IGameplayTagSet 实现，Dictionary 引用计数 |
| `DefaultTargetResolvers` | 静态工厂，为每种 TargetType 创建单例寻敌器（Self/Enemy/Ally/AllEnemies/AllAllies） |

### 值类型

| 类型 | 说明 |
|------|------|
| `GameplayTag` (struct) | 不可变标签，string Value + 预计算 Hash，实现 IEquatable/IComparable，支持 `string` 隐式转换 |
| `EffectContext` (struct) | 只读效果上下文：Source、Target、Ability 引用 |

### 枚举

| 枚举 | 值 |
|------|------|
| `AbilityType` | Active, Passive |
| `ActivationType` | Manual, Auto, OnEvent |
| `AbilityState` | Ready, Casting, Executing, Cooldown |
| `ModifierPolarity` | Buff, Debuff, Neutral |
| `ModifierStacking` | Refresh, AddStack, Replace, Ignore |
| `TargetType` | Self, Enemy, Ally, AllEnemies, AllAllies |
| `TriggerEventType` | OnDealDamage, OnTakeDamage, OnHeal, OnKill, OnDeath, OnAttack, OnBuffApplied, OnBuffRemoved, OnDebuffApplied, OnDebuffRemoved, OnBuffStackChanged, OnAbilityCast, OnAbilityHit, OnAbilityMiss, OnAbilityCooldownEnd, OnBattleStart, OnTurnStart, OnTurnEnd, OnSpawn, OnHPThreshold |
| `TriggerScope` | Self, Source, Target, AllEnemies, AllAllies |

### 委托

| 委托 | 签名 |
|------|------|
| `AttributeChangedHandler` | `void (string attributeName, float oldValue, float newValue)` |

<!-- AUTO-GENERATED-END -->

## 依赖关系

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-DEPS-START -->

本模块为零依赖框架层模块，仅依赖 `System` 和 `System.Collections.Generic`，不依赖任何其他 Change.Framework.* 或 Unity 模块。

<!-- AUTO-GENERATED-DEPS-END -->

## 使用场景

- 创建自定义技能：继承 `BaseGameplayAbility` / `ActiveGameplayAbility` / `PassiveGameplayAbility`
- 创建自定义修饰器：继承 `BaseModifier`，重写 `OnApplied` / `OnTickCore` / `OnRemoved` 等虚拟方法
- 创建自定义触发器：继承 `BaseTrigger`，实现 `Execute` 抽象方法，可选重写 `Condition`
- 组合使用：通过 `IAbilitySystem` 将技能、修饰器、触发器注册到实体上，每帧调用 `Tick(deltaTime)` 驱动
- 自定义寻敌：实现 `ITargetResolver` 接口，或使用 `DefaultTargetResolvers.Create(TargetType)` 获取内置实现

## 注意事项

- `BaseTrigger.MaxCascadeDepth = 8` 防止触发器链式触发导致无限递归
- `BaseModifier` 的 `OnTick` 限定了每帧最大 tick 次数为 10，防止单帧内高频 tick 导致性能问题
- `DefaultGameplayTagSet` 使用引用计数，同一个标签多次 Add 需要对应次数的 Remove
- `GameplayTag` 是值类型（struct），支持从 `string` 隐式转换，方便使用
- `DefaultTargetResolvers` 中的寻敌器是无状态单例，线程安全

## 经验教训

<!-- 由 /record-lesson 自动追加，请勿手工编辑此标题 -->
