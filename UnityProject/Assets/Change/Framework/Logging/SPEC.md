---
module_name: "Logging"
directory: "UnityProject/Assets/Change/Framework/Logging"
type: "Framework"  # Framework | Business | Library | Application | Utility
confidence: 0.8  # 0.0-1.0, 类型推断置信度
keywords:
  - "Logging"
  - "Log"
  - "Logger"
  - "LogRouter"
  - "LogLevel"
dependencies: []
  # TODO: 填写依赖的模块名称列表
  # - "OtherModule"
description: "轻量级日志框架，提供统一的日志接口和可插拔的日志输出目标。支持四级日志级别（Debug/Info/Warn/Error）和日志路由分发，包含空日志器实现以消除空检查。"
last_updated: "2026-06-22 11:30:00"  # 替换为当前时间，格式 YYYY-MM-DD HH:MM:SS
---

# Logging

## 概述

Logging 是一个轻量级日志框架，通过 `ILogger` 接口提供统一的日志记录抽象，通过 `ILogSink` 接口支持可插拔的日志输出目标。`LogRouter` 作为核心路由器，将日志消息按级别过滤后分发到多个 Sink，并通过 `Freeze()` 机制确保配置完成后的不可变性。`NullLogger` 提供空日志器单例，避免空引用检查。

## 公共接口

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-START -->

### ILogger
日志器接口，定义统一的日志记录抽象。

| 成员 | 签名 | 说明 |
|------|------|------|
| `Debug` | `void Debug(string message)` | 输出调试级别日志 |
| `Info` | `void Info(string message)` | 输出信息级别日志 |
| `Warn` | `void Warn(string message)` | 输出警告级别日志 |
| `Error` | `void Error(string message)` | 输出错误级别日志 |

### ILogSink
日志输出目标接口，接收已分级别的日志消息。

| 成员 | 签名 | 说明 |
|------|------|------|
| `Write` | `void Write(LogLevel level, string message)` | 写入指定级别的日志消息 |

### LogLevel
日志级别枚举，按严重程度递增排列。

| 成员 | 值 | 说明 |
|------|------|------|
| `Debug` | 0 | 调试信息，最详细级别 |
| `Info` | 1 | 一般信息 |
| `Warn` | 2 | 警告信息 |
| `Error` | 3 | 错误信息，最高严重级别 |

### LogRouter
日志路由器，实现 `ILogger` 接口，将日志消息按级别过滤后分发到多个 `ILogSink`。

| 成员 | 签名 | 说明 |
|------|------|------|
| `AddSink` | `void AddSink(ILogSink sink, LogLevel minLevel)` | 注册日志输出目标，指定最低接收级别（冻结前调用） |
| `Freeze` | `void Freeze()` | 冻结路由器，冻结后不可再添加 Sink |
| `Debug` | `void Debug(string message)` | 分发调试级别日志到所有 Sink |
| `Info` | `void Info(string message)` | 分发信息级别日志到所有 Sink |
| `Warn` | `void Warn(string message)` | 分发警告级别日志到所有 Sink |
| `Error` | `void Error(string message)` | 分发错误级别日志到所有 Sink |

### NullLogger
空日志器，实现 `ILogger` 接口，所有日志方法为空操作。通过单例访问，避免 null 检查。

| 成员 | 签名 | 说明 |
|------|------|------|
| `Instance` | `static readonly NullLogger` | 全局单例 |
| `Debug` / `Info` / `Warn` / `Error` | `void *(string message)` | 空操作，丢弃日志消息 |

<!-- AUTO-GENERATED-END -->

## 依赖关系

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-DEPS-START -->

| 依赖模块 | 路径 | 用途 |
|----------|------|------|
| `FastList` | `Change.Framework.Collections` | LogRouter 内部 Sink 注册列表的存储容器 |

<!-- AUTO-GENERATED-DEPS-END -->

## 使用场景

<!-- TODO: 描述典型使用场景 -->

- 游戏运行时日志输出到 Unity Console 和文件
- 开发阶段记录 Debug 日志，发布版本仅记录 Error 日志
- 通过 `NullLogger.Instance` 作为默认值，避免空引用检查

## 注意事项

<!-- TODO: 记录重要的设计决策、约束或陷阱 -->

- `LogRouter.AddSink()` 必须在 `Freeze()` 之前调用，冻结后添加 Sink 将抛出 `InvalidOperationException`
- `LogRouter.AddSink()` 传入 null 会抛出 `ArgumentNullException`
- `ILogSink.Write()` 中抛出的异常会被 `LogRouter` 静默吞掉，确保单个 Sink 故障不影响其他 Sink
- `LogRouter.Debug()` 等方法会将 null 消息规范化为 `string.Empty`

## 经验教训

<!-- 由 /record-lesson 自动追加，请勿手工编辑此标题 -->
