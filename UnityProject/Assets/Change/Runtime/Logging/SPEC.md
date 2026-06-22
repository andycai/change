---
module_name: "Logging"
directory: "UnityProject/Assets/Change/Runtime/Logging"
type: "Library"  # Framework | Business | Library | Application | Utility
confidence: 0.8  # 0.0-1.0, 类型推断置信度
keywords:
  - "Logging"
  - "Log"
  - "Sink"
dependencies:
  - "Change.Framework.Logging"
  - "UnityEngine"
description: "Runtime logging sinks that implement the ILogSink interface. UnityLogSink forwards log messages to Unity's debug console with level-appropriate channels (Log/LogWarning/LogError), while FileLogSink writes timestamped, formatted log entries to disk via StreamWriter."
last_updated: "2026-06-22 11:31:00"
---

# Logging

## 概述

Runtime 层的日志输出 sink 模块，提供两种 ILogSink 实现：

- **UnityLogSink** — 将日志消息转发到 Unity 的 `Debug.Log` / `Debug.LogWarning` / `Debug.LogError`，支持可选前缀。
- **FileLogSink** — 将带时间戳的格式化日志写入磁盘文件，支持追加/覆盖模式，实现 `IDisposable`。

两个 sink 均实现 `Change.Framework.Logging.ILogSink` 接口，可注册到 `LogRouter` 中使用。

## 公共接口

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-START -->

### UnityLogSink (class)

```csharp
public sealed class UnityLogSink : ILogSink
```

将日志输出到 Unity 调试控制台，根据 `LogLevel` 自动选择对应的 Unity 日志通道。

| 签名 | 说明 |
|------|------|
| `UnityLogSink(string prefix = "")` | 构造函数，prefix 会附加在每条日志消息前 |
| `void Write(LogLevel level, string message)` | 写入日志；Warn→LogWarning, Error→LogError, 其余→Log |

### FileLogSink (class)

```csharp
public sealed class FileLogSink : ILogSink, IDisposable
```

将带 ISO 8601 时间戳的格式化日志行写入磁盘文件。构造函数自动创建目录。`AutoFlush` 开启以确保每次写入立即刷盘。

| 签名 | 说明 |
|------|------|
| `FileLogSink(string filePath, bool append = true)` | 构造函数，filePath 不能为 null/空白；append=false 时覆盖已有文件 |
| `string FilePath { get; }` | 获取输出文件路径 |
| `void Write(LogLevel level, string message)` | 写入 `{ISO时间戳} [{level}] {message}` 格式的日志行 |
| `void Dispose()` | 释放 StreamWriter，可安全重复调用 |

<!-- AUTO-GENERATED-END -->

## 依赖关系

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-DEPS-START -->

| 依赖模块 | 引用类型 |
|-----------|----------|
| `Change.Framework.Logging` (ILogSink, LogLevel) | 接口实现 |
| `UnityEngine` (Debug) | 类型引用 |
| `System.IO` (StreamWriter, FileStream) | 类型引用 |

<!-- AUTO-GENERATED-DEPS-END -->

## 使用场景

<!-- TODO: 描述典型使用场景 -->

## 注意事项

<!-- TODO: 记录重要的设计决策、约束或陷阱 -->

## 经验教训

<!-- 由 /record-lesson 自动追加，请勿手工编辑此标题 -->
