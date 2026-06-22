---
module_name: "Timer"
directory: "UnityProject/Assets/Change/Runtime/Timer"
type: "Library"  # Framework | Business | Library | Application | Utility
confidence: 0.8  # 0.0-1.0, 类型推断置信度
keywords:
  - "Timer"
  # TODO: 添加更多关键字以便搜索
dependencies: []
  # TODO: 填写依赖的模块名称列表
  # - "OtherModule"
description: ""
last_updated: "2026-06-22 11:34:00"  # 替换为当前时间，格式 YYYY-MM-DD HH:MM:SS
---

# Timer

## 概述

<!-- TODO: 用 2-3 句话描述该模块的职责和用途 -->

## 公共接口

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-START -->

### `Timer` (static class)

| 方法 | 签名 | 说明 |
|------|------|------|
| `Delay` | `(float seconds, Action callback, CancellationToken ct = default, bool scaled = true) → IDisposable` | 延迟 `seconds` 秒后执行一次性回调，返回可取消的句柄 |
| `Repeat` | `(float interval, Action callback, CancellationToken ct = default, bool scaled = true) → IDisposable` | 每隔 `interval` 秒重复执行回调，返回可取消的句柄 |
| `EveryFrame` | `(Action<float, CancellationToken> onFrame, CancellationToken ct = default, bool scaled = true) → IDisposable` | 每帧执行回调，回调参数为 deltaTime 和取消令牌 |
| `DelayAsync` | `(float seconds, CancellationToken ct = default, bool scaled = true) → TimerAwaiter` | 返回可 await 的延迟对象，支持 CancellationToken 取消 |

### `TimerAwaiter` (class)

实现 `INotifyCompletion`，支持 `await` 异步延迟。

| 成员 | 签名 | 说明 |
|------|------|------|
| `GetAwaiter` | `() → TimerAwaiter` | 返回自身，支持 `await` 模式 |
| `IsCompleted` | `bool` | 当 CancellationToken 已取消时返回 true |
| `GetResult` | `() → void` | 检查取消状态，若已取消则抛出 `OperationCanceledException` |
| `OnCompleted` | `(Action continuation) → void` | 注册延迟完成后的续执行动作 |

<!-- AUTO-GENERATED-END -->

## 依赖关系

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-DEPS-START -->

| 依赖 | 类型 | 说明 |
|------|------|------|
| `System` / `System.Threading` | BCL | CancellationToken、CancellationTokenSource 等 |
| `System.Runtime.CompilerServices` | BCL | INotifyCompletion，async/await 基础设施 |
| `UnityEngine` | 引擎 | Time.deltaTime、Time.unscaledDeltaTime、GameObject、MonoBehaviour |

<!-- AUTO-GENERATED-DEPS-END -->

## 使用场景

<!-- TODO: 描述典型使用场景 -->

## 注意事项

<!-- TODO: 记录重要的设计决策、约束或陷阱 -->

## 经验教训

<!-- 由 /record-lesson 自动追加，请勿手工编辑此标题 -->
