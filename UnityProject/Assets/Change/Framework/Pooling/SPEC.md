---
module_name: "Pooling"
directory: "UnityProject/Assets/Change/Framework/Pooling"
type: "Framework"  # Framework | Business | Library | Application | Utility
confidence: 0.8  # 0.0-1.0, 类型推断置信度
keywords:
  - "Pooling"
  # TODO: 添加更多关键字以便搜索
dependencies: []
  # TODO: 填写依赖的模块名称列表
  # - "OtherModule"
description: "通用对象池系统，提供 static 泛型池 Pool<T> 和实例池 InstancePool<T> 两种模式，支持对象复用、预热、容量控制和统计监控。"
last_updated: "2026-06-22 11:24:33"  # 替换为当前时间，格式 YYYY-MM-DD HH:MM:SS
---

# Pooling

## 概述

通用对象池系统，为 Unity 项目提供高性能对象复用机制。支持两种使用模式：静态泛型池 `Pool<T>`（需 `new()` 约束，零配置开箱即用）和实例池 `InstancePool<T>`（通过工厂函数构造，支持自定义创建逻辑）。核心引擎 `PoolEngine<T>` 基于 `Stack<T>` 实现，单线程设计，提供预热（Prewarm）、容量控制（SetMaxSize）和完整的统计监控（PoolStats）。在 Editor/Development 构建中内置双重释放和外借检测。

## 公共接口

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-START -->

### `IPoolable` (interface)
池化对象必须实现的接口，定义 `Reset()` 方法用于归还时重置对象状态。

```csharp
public interface IPoolable
{
    void Reset();
}
```

### `IPool<T>` (interface)
泛型对象池的公共接口契约，定义池的标准操作。

```csharp
public interface IPool<T> where T : class, IPoolable
{
    int InactiveCount { get; }
    int MaxSize { get; }
    T Get();
    void Release(T item);
    void Prewarm(int count);
    void SetMaxSize(int maxSize);
    void Clear();
    PoolStats GetStats();
}
```

### `Pool<T>` (static class)
静态泛型对象池。要求 `T : class, IPoolable, new()`，全局单例，通过 `PoolDefaults.DefaultMaxSize` 控制默认容量（默认 128）。

- `static T Get()` — 获取或创建一个池化实例
- `static void Release(T item)` — 归还实例到池中（自动调用 `Reset()`）
- `static void Prewarm(int count)` — 预热创建指定数量的实例
- `static void SetMaxSize(int maxSize)` — 动态调整池容量上限（缩减时丢弃超出部分）
- `static void Clear()` — 清空所有不活跃实例
- `static PoolStats GetStats()` — 获取池的统计快照
- `static int InactiveCount` — 当前可用的不活跃实例数
- `static int MaxSize` — 当前容量上限

### `InstancePool<T>` (sealed class)
实例级对象池，通过工厂函数创建实例，适合需要自定义构造逻辑的场景。

- `InstancePool(Func<T> factory)` — 使用默认容量构造
- `InstancePool(Func<T> factory, int maxSize)` — 指定容量构造
- 成员方法与 `Pool<T>` 一致：`Get()`, `Release(T)`, `Prewarm(int)`, `SetMaxSize(int)`, `Clear()`, `GetStats()`
- `int InactiveCount`, `int MaxSize`

### `PoolDefaults` (static class)
池的全局默认配置。

- `static int DefaultMaxSize { get; set; }` — 默认最大容量，初始值 128

### `PoolStats` (readonly struct)
池的统计信息快照（不可变）。

- `long Created` — 累计创建实例数
- `long Rented` — 累计借出次数
- `long Released` — 累计归还次数
- `long Dropped` — 因池满被丢弃的实例数
- `int MaxSize` — 快照时的容量上限
- `int InactiveCount` — 快照时的不活跃实例数

<!-- AUTO-GENERATED-END -->

## 依赖关系

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-DEPS-START -->

本模块无外部模块依赖，仅依赖 .NET 标准库（`System`, `System.Collections.Generic`, `System.Runtime.CompilerServices`）。

其他模块对 Pooling 的依赖：
- **Cqrs** — `IPooledCommand`、`IPooledAsyncCommand` 继承 `IPoolable`

<!-- AUTO-GENERATED-DEPS-END -->

## 使用场景

<!-- TODO: 描述典型使用场景 -->

## 注意事项

<!-- TODO: 记录重要的设计决策、约束或陷阱 -->

## 经验教训

<!-- 由 /record-lesson 自动追加，请勿手工编辑此标题 -->
