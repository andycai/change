---
module_name: "Fsm"
directory: "UnityProject/Assets/Change/Framework/Fsm"
type: "Framework"  # Framework | Business | Library | Application | Utility
confidence: 0.8  # 0.0-1.0, 类型推断置信度
keywords:
  - "Fsm"
  - "StateMachine"
  - "State"
  - "Transition"
dependencies: []
  # TODO: 填写依赖的模块名称列表
  # - "OtherModule"
description: "通用有限状态机框架，提供类型安全的状态定义、事件驱动的状态切换、以及序列化的状态变更通知。支持自转换控制、容错处理和并发事件队列。"
last_updated: "2026-06-22 11:22:24"  # 替换为当前时间，格式 YYYY-MM-DD HH:MM:SS
---

# Fsm

## 概述

Fsm（Finite State Machine）是一个泛型有限状态机框架，用于管理类型安全的状态生命周期。它通过 `IFsmState<TStateId, TEvent>` 接口定义状态行为，使用 `StateMachine<TStateId, TEvent>` 驱动状态切换和事件分发，并通过 `StateChange<TStateId, TEvent>` 提供带序列号的变更上下文。框架内置环形缓冲区队列处理并发事件和转换请求，支持自转换控制、故障隔离和状态变更回调。

## 公共接口

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-START -->

### IFsmState\<TStateId, TEvent\>
状态行为接口，所有状态必须实现此接口。

| 成员 | 签名 | 说明 |
|------|------|------|
| `Id` | `TStateId Id { get; }` | 状态唯一标识符 |
| `OnEnter` | `void OnEnter(in StateChange<TStateId, TEvent> change)` | 进入状态时调用，接收变更上下文 |
| `OnExit` | `void OnExit(in StateChange<TStateId, TEvent> change)` | 退出状态时调用，接收变更上下文 |
| `OnEvent` | `FsmResult<TStateId> OnEvent(in TEvent evt)` | 处理事件，返回转换结果 |

### StateMachine\<TStateId, TEvent\>
状态机引擎，管理状态注册、切换和事件分发。

| 成员 | 签名 | 说明 |
|------|------|------|
| 构造函数 | `StateMachine(IEqualityComparer<TStateId> comparer, int initialCapacity, bool allowSelfTransition)` | 创建状态机，可指定比较器、缓冲区容量和自转换策略 |
| `Register` | `void Register(IFsmState<TStateId, TEvent> state)` | 注册状态（启动前调用），重复注册抛出异常 |
| `Start` | `void Start(TStateId initialStateId)` | 启动状态机并进入初始状态 |
| `ChangeState` | `void ChangeState(TStateId targetStateId)` | 请求切换到目标状态 |
| `ChangeState` | `void ChangeState(TStateId targetStateId, TEvent causeEvent)` | 请求切换到目标状态并记录触发事件 |
| `Fire` | `void Fire(TEvent evt)` | 向当前状态发送事件 |
| `IsStarted` | `bool IsStarted { get; }` | 是否已启动 |
| `IsFaulted` | `bool IsFaulted { get; }` | 是否处于故障状态（OnEnter 抛出异常后进入） |
| `CurrentStateId` | `TStateId CurrentStateId { get; }` | 当前状态 ID |
| `OnStateChanged` | `event Action<StateChange<TStateId, TEvent>>` | 状态变更通知事件，携带完整变更上下文 |

### StateChange\<TStateId, TEvent\>
状态变更上下文，描述一次状态切换的完整信息。

| 成员 | 签名 | 说明 |
|------|------|------|
| `HasFrom` | `bool HasFrom { get; }` | 是否有前一状态（初始切换为 false） |
| `From` | `TStateId From { get; }` | 前一状态 ID |
| `To` | `TStateId To { get; }` | 目标状态 ID |
| `HasCauseEvent` | `bool HasCauseEvent { get; }` | 是否由事件触发 |
| `CauseEvent` | `TEvent CauseEvent { get; }` | 触发切换的事件 |
| `Sequence` | `long Sequence { get; }` | 单调递增的序列号 |
| `Initial` | `static StateChange<TStateId, TEvent> Initial(TStateId to, long sequence)` | 创建初始切换上下文 |
| `Create` | `static StateChange<TStateId, TEvent> Create(TStateId from, TStateId to, TEvent causeEvent, long sequence)` | 创建由事件触发的切换上下文 |
| `CreateWithoutEvent` | `static StateChange<TStateId, TEvent> CreateWithoutEvent(TStateId from, TStateId to, long sequence)` | 创建非事件触发的切换上下文 |

### FsmResult\<TStateId\>
事件处理结果，指示是否触发状态转换。

| 成员 | 签名 | 说明 |
|------|------|------|
| `HasTransition` | `bool HasTransition { get; }` | 是否请求状态转换 |
| `NextStateId` | `TStateId NextStateId { get; }` | 目标状态 ID（HasTransition=false 时抛出异常） |
| `Handled` | `static FsmResult<TStateId> Handled()` | 事件已处理，不转换状态 |
| `Ignored` | `static FsmResult<TStateId> Ignored()` | Handled 的别名，语义上表示事件被忽略 |
| `TransitionTo` | `static FsmResult<TStateId> TransitionTo(TStateId next)` | 请求切换到指定状态 |

<!-- AUTO-GENERATED-END -->

## 依赖关系

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-DEPS-START -->

| 依赖模块 | 路径 | 用途 |
|----------|------|------|
| `FastDictionary` | `Change.Framework.Collections.Containers` | 状态注册表的内部存储 |
| `RingBuffer` | `Change.Framework.Collections.Containers` | 事件队列和转换请求队列的内部缓冲区 |

<!-- AUTO-GENERATED-DEPS-END -->

## 使用场景

<!-- TODO: 描述典型使用场景 -->

- 游戏流程管理（启动 → 登录 → 大厅 → 匹配 → 战斗 → 结算）
- UI 状态管理（菜单页面的显示/隐藏/过渡动画）
- 战斗回合流程控制（加载 → 准备 → 进行 → 暂停 → 结算 → 退出）

## 注意事项

<!-- TODO: 记录重要的设计决策、约束或陷阱 -->

- 状态必须在 `Start()` 之前注册，启动后注册将抛出异常
- `OnEnter` 中抛出异常会导致状态机进入 `IsFaulted` 状态，阻止后续事件处理
- 事件和转换请求通过环形缓冲区序列化执行，容量在构造时指定，超出容量会抛出异常
- `StateChange` 携带单调递增的 `Sequence`，可用于检测丢失或乱序的变更通知
- 默认不允许自转换（切换到当前状态），通过 `allowSelfTransition: true` 启用

## 经验教训

<!-- 由 /record-lesson 自动追加，请勿手工编辑此标题 -->
