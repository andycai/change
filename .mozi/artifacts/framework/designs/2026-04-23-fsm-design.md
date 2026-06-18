# Framework FSM 设计文档（简化生产版）

- 日期：2026-04-23
- 目标目录：`UnityProject/Assets/Fun/Framework/Fsm`
- 适用层级：Framework（与业务无关）
- 设计原则：生产可用、接口清晰、实现简化、可测试

## 1. 背景与目标

本设计用于在 `Fun/Framework` 层提供一套可复用的通用有限状态机（FSM）基础能力，作为后续业务开发的状态流转基建。

目标：

1. 提供纯事件驱动的状态切换能力。
2. 保持业务无关，不依赖 UI、网络、场景对象等上层模块。
3. 在“足够稳”的前提下，控制复杂度，避免过度设计。

## 2. 范围与非目标

### 2.1 范围（第一版）

- 单活跃状态（任意时刻只有一个当前状态）
- 状态注册与初始启动
- 事件投递与状态内处理
- 进入/退出回调
- 显式状态切换
- 状态切换通知（观测钩子）

### 2.2 非目标（第一版明确不做）

- 层级状态机（HSM）
- 并行状态
- 条件迁移 DSL/图编辑器
- 历史状态恢复与持久化快照

## 3. 总体架构

核心由 4 个角色组成：

1. `StateMachine<TStateId, TEvent>`
   - 状态机运行时，负责注册、启动、收事件、串行处理、切换状态。

2. `IFsmState<TStateId, TEvent>`
   - 状态契约接口，定义 `OnEnter` / `OnExit` / `OnEvent`。

3. `StateChange<TStateId, TEvent>`
   - 一次状态切换上下文，包含 `HasFrom`、`From`、`To`、`CauseEvent` 与可选 `Sequence`。

4. `FsmResult<TStateId>`
   - 状态处理事件后的统一结果：`Handled` / `Ignored` / `TransitionTo(next)`。

架构约束：

- 框架层不持有业务对象。
- 状态机核心不引入多线程调度，默认由调用方在 Unity 主线程驱动。
- 切换行为必须可观测（`OnStateChanged` 事件）。

## 4. 组件与 API 设计

### 4.1 状态接口

```csharp
public interface IFsmState<TStateId, TEvent>
{
    TStateId Id { get; }
    void OnEnter(in StateChange<TStateId, TEvent> change);
    void OnExit(in StateChange<TStateId, TEvent> change);
    FsmResult<TStateId> OnEvent(in TEvent evt);
}
```

设计说明：

- `Id` 用于状态注册索引与切换目标定位。
- `OnEvent` 返回 `FsmResult`，而不是直接操作状态机，降低耦合。

### 4.2 结果类型

```csharp
public readonly struct FsmResult<TStateId>
{
    public bool IsHandled { get; }
    public bool HasTransition { get; }
    public TStateId NextStateId { get; }

    public static FsmResult<TStateId> Handled();
    public static FsmResult<TStateId> Ignored();
    public static FsmResult<TStateId> TransitionTo(TStateId next);
}
```

设计说明：

- 统一事件处理分支，减少状态机内部分散判断。
- 保证状态行为表达简单明确。

### 4.3 状态机主类（关键方法）

- `Register(IFsmState<TStateId, TEvent> state)`
- `Start(TStateId initial)`
- `Fire(in TEvent evt)`
- `ChangeState(TStateId next)`
- `CurrentStateId` / `IsStarted`
- `event Action<StateChange<TStateId, TEvent>> OnStateChanged`

约束规则：

- 重复注册同 `Id`：抛出异常。
- 未启动调用 `Fire`：抛出异常。
- 切换到未注册状态：抛出异常。
- 切换到当前状态：no-op（不触发 Enter/Exit）。

## 5. 运行时数据流

### 5.1 启动流程

1. `Start(initial)` 校验目标状态已注册。
2. 设置 `CurrentStateId = initial`。
3. 调用 `initial.OnEnter(change)`（`HasFrom = false`，`To` 为初始状态）。

### 5.2 事件处理流程（纯事件驱动）

1. `Fire(evt)` 将事件写入内部队列（`Queue<TEvent>`）。
2. 若当前未在处理循环，进入 drain；若正在处理，仅入队后返回。
3. 逐个出队并调用当前状态 `OnEvent(evt)`。
4. 根据返回的 `FsmResult` 执行忽略、消费或切换。

### 5.3 切换流程

1. 若 `to == from`，直接返回。
2. 调用 `from.OnExit(change)`。
3. 更新 `CurrentStateId = to`。
4. 调用 `to.OnEnter(change)`。
5. 触发 `OnStateChanged(change)`。

一致性保障：

- 同时只有一个切换在执行。
- 事件按 FIFO 顺序处理。
- 重入 `Fire` 不递归执行，统一排队串行处理。

补充约定：

- 在 `OnEvent` / `OnEnter` / `OnExit` 内再次调用 `Fire` 时，事件统一入队，等待当前处理段结束后继续处理。
- 在状态回调内调用 `ChangeState` 允许，但仍走同一串行切换通道，保证顺序一致。

## 6. 异常与边界策略

### 6.1 Fail-fast 原则

以下视为编程错误，直接抛出 `InvalidOperationException`：

- 重复注册状态
- 未启动先 `Fire`
- 切换至未注册状态

### 6.2 状态回调异常

- `OnEnter` / `OnExit` / `OnEvent` 中异常默认向外抛出。
- 状态机保证不出现“半更新字段”的不可预期状态。

当前切换语义：

- 仅在 `OnExit` 成功后更新 `CurrentStateId`。
- 若 `OnEnter` 抛错，则本次切换失败并抛出异常；恢复策略交由调用方处理（第一版不引入复杂回滚机制）。

### 6.3 可诊断性

- 异常信息需包含关键上下文（当前状态、目标状态、事件信息）。
- `StateChange` 提供切换上下文给日志/埋点系统使用。

## 7. 测试策略与验收标准

### 7.1 单元测试清单

1. 启动流：初始状态 `OnEnter` 仅调用一次。
2. 事件流：`Handled` / `Ignored` / `TransitionTo` 行为正确。
3. 切换流：`OnExit -> OnEnter -> OnStateChanged` 顺序正确。
4. 异常流：非法调用路径抛出预期异常。
5. 队列流：`OnEvent` 内二次 `Fire` 时保持 FIFO，不发生递归栈增长。

### 7.2 验收标准

- 覆盖以上 5 类测试且全部通过。
- 不依赖业务模块与编辑器 API（可在 Runtime 环境独立运行）。
- 不使用反射做核心调度。
- `OnStateChanged` 可作为可观测出口。

## 8. 未来演进（不影响第一版实现）

在保持现有 API 稳定的前提下，后续可按需增加：

- Guard（条件迁移）
- AnyState（全局转移）
- 层级状态机
- 调试可视化支持

第一版不预留复杂机制，仅保留清晰扩展点，遵循 YAGNI。
