---
module_name: "Cqrs"
directory: "UnityProject/Assets/Change/Framework/Cqrs/"
type: "Framework"  # Framework | Business | Library | Application | Utility
confidence: 0.0  # 0.0-1.0, 类型推断置信度
keywords:
  - "Cqrs"
  - "CQRS"
  # TODO: 添加更多关键字以便搜索
dependencies: []
  # TODO: 填写依赖的模块名称列表
  # - "OtherModule"
description: ""
last_updated: "2026-06-22 11:07:00"  # 替换为当前时间，格式 YYYY-MM-DD HH:MM:SS
---

# Cqrs

## 概述

<!-- TODO: 用 2-3 句话描述该模块的职责和用途 -->

## 公共接口

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-START -->

### Abstractions (消息契约)

| 接口 | 签名 | 说明 |
|------|------|------|
| `ICommand` | `void Execute()` | 同步自处理命令，实现为 `readonly struct` 以获得零分配分发 |
| `IAsyncCommand` | `UniTask ExecuteAsync()` | 异步自处理命令，实现为 `readonly struct` |
| `IQuery<TResult>` | `TResult Query()` | 同步自处理查询，结果类型无约束避免装箱 |
| `IAsyncQuery<TResult>` | `UniTask<TResult> QueryAsync()` | 异步自处理查询 |
| `IEvent` | (marker) | 事件消息标记接口，实现为 `readonly struct` |
| `IEventHandler<TEvent>` | `void Handle(in TEvent @event)` | 事件处理器接口，实现为 class 以避免装箱 |
| `IPooledCommand` | `void Execute()` (extends `IPoolable`) | 池化 class 同步命令，通过 `Pool<T>` 借还 |
| `IPooledAsyncCommand` | `UniTask ExecuteAsync()` (extends `IPoolable`) | 池化 class 异步命令，await 完成后 Release |

### Abstractions (总线契约)

| 接口 | 方法 | 说明 |
|------|------|------|
| `ICqrsBus` | `void Send<TCommand>(in TCommand)` | 分发同步 struct 命令 |
|  | `void Send<TCommand>(Action<TCommand>)` | 分发池化 class 命令 |
|  | `TResult Ask<TQuery, TResult>(in TQuery)` | 分发同步 struct 查询 |
|  | `void Publish<TEvent>(in TEvent)` | 分发事件到所有订阅者 |
|  | `UniTask SendAsync<TCommand>(TCommand)` | 分发异步 struct 命令 |
|  | `UniTask SendAsync<TCommand>(Action<TCommand>)` | 分发池化 class 异步命令 |
|  | `UniTask<TResult> AskAsync<TQuery, TResult>(TQuery)` | 分发异步 struct 查询 |
| `ICqrsRegistry` | `void Subscribe<TEvent>(IEventHandler<TEvent>)` | 订阅事件处理器 |
|  | `void Subscribe<TEvent>(Action<TEvent>)` | 订阅静态委托（禁止捕获变量） |
|  | `void Unsubscribe<TEvent>(IEventHandler<TEvent>)` | 取消订阅事件处理器 |
|  | `void Unsubscribe<TEvent>(Action<TEvent>)` | 取消订阅委托 |
| `ICqrsBootstrap` | `void Subscribe<TEvent>(...)` (×2) | 在 Build 前注册事件处理器 |
|  | `ICqrsBus Build()` | 构建并返回单例 ICqrsBus 实例 |

### Core (具体实现)

| 类 | 说明 |
|------|------|
| `CqrsBus` | `sealed class`，实现 `ICqrsBus` + `ICqrsRegistry`。使用 `FastDictionary<Type, IEventHandlerList>` 存储事件订阅，`FastList` 存储处理器/委托。通过 `#if ENABLE_CQRS_MONITORING` 编译符号可选启用性能监控 |
| `CqrsBootstrap` | `sealed class`，实现 `ICqrsBootstrap`。持有 `CqrsBus` 实例，`Build()` 使用双重检查锁定返回单例 |

### Exceptions

| 类 | 说明 |
|------|------|
| `ClosureCaptureException` | `sealed class`，继承 `InvalidOperationException`，标记 `[Serializable]`。当订阅捕获变量的委托时抛出，保护 0-GC 分发保证 |

### Monitoring (可选性能监控)

| 类型 | 说明 |
|------|------|
| `CqrsPerformanceMonitor` | `sealed class`，按消息类型聚合执行指标（计数、持续时间、GC 分配）。热路径 `RecordExecution` 在原地修改 accumulator，无新增引用 |
| `IPerformanceThresholdPolicy` | 阈值策略接口，`Threshold` 属性 + `ShouldAlert(long gcBytes)` 方法 |
| `DefaultThresholdPolicy` | `sealed class`，默认策略：单次执行 GC 分配 > 100 bytes 时告警 |
| `PerformanceMetrics` | `readonly struct`，聚合指标快照（MessageType, ExecutionCount, TotalDurationTicks, TotalGcBytes, MaxGcBytes） |
| `PerformanceAlert` | `readonly struct`，阈值超限告警（MessageType, GcBytes, Threshold, DurationTicks） |

<!-- AUTO-GENERATED-END -->

## 依赖关系

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-DEPS-START -->

| 依赖模块 | 使用内容 |
|----------|---------|
| `Change.Framework.Logging` | `ILogger`, `NullLogger` |
| `Change.Framework.Pooling` | `Pool<T>`, `IPoolable` |
| `Change.Framework.Collections` | `FastDictionary`, `FastList` |
| `Cysharp.Threading.Tasks` | `UniTask`, `UniTask<T>` (外部包) |

<!-- AUTO-GENERATED-DEPS-END -->

## 使用场景

<!-- TODO: 描述典型使用场景 -->
- 默认用 Struct 自处理；只有当 command 需要复杂可变状态、异步 I/O，或实例本身较重时，才用 Class 池化自处理。
- Class 模式非 0GC（configure 回调为 capturing lambda，有少量分配），不要用于高频热路径。

## 注意事项

<!-- TODO: 记录重要的设计决策、约束或陷阱 -->
- bus 在 `Execute`（同步）或 `await ExecuteAsync` 完成（异步）后调 `Pool<T>.Release`，`Release` 内部调用 `Reset`。
- `Reset` 必须幂等，且容忍“仅 configure 部分 / 未 Execute”的半配置状态（异常路径下可能发生）。
- `Reset` 只清 per-dispatch 瞬态字段，不清长期依赖。

## 经验教训

<!-- 由 /record-lesson 自动追加，请勿手工编辑此标题 -->
