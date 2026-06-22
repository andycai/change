---
module_name: "Collections"
directory: "UnityProject/Assets/Change/Framework/Collections"
type: "Framework"  # Framework | Business | Library | Application | Utility
confidence: 0.8  # 0.0-1.0, 类型推断置信度
keywords:
  - "Collections"
  - "FastList"
  - "FastDictionary"
  - "FastHashSet"
  - "FastPriorityQueue"
  - "RingBuffer"
  - "ZeroGC"
  - "DataStructures"
dependencies: []
  # Collections 仅依赖 System / System.Collections.Generic，无内部模块依赖
description: "高性能、零GC的Unity集合库，提供FastList、FastDictionary、FastHashSet、FastPriorityQueue和RingBuffer等数据结构，专为游戏开发热路径优化。"
last_updated: "2026-06-22 11:19:11"  # 替换为当前时间，格式 YYYY-MM-DD HH:MM:SS
---

# Collections

## 概述

Collections 模块是 Change 框架的基础数据结构层，提供了一套针对 Unity 游戏开发热路径优化的集合类。所有容器均实现 `IClearable` 接口，支持 `Logical`（仅重置计数）和 `ZeroMemory`（清零释放引用）两种清理模式。容器内部通过 `GrowPolicy` 统一管理扩容策略（初始容量 4，2x 增长），并通过 `CollectionMetrics` 暴露扩容诊断计数器。所有容器均为单线程设计，非线程安全。

## 公共接口

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-START -->

### 抽象层 (Abstractions)

| 类型 | 签名 | 说明 |
|------|------|------|
| `interface` | `IClearable` | 可清理接口，定义 `void Clear(ClearMode mode)` |

### 核心类型 (Core)

| 类型 | 签名 | 说明 |
|------|------|------|
| `enum` | `ClearMode` | 清理模式：`Logical`（仅重置计数）、`ZeroMemory`（清零数组释放引用） |
| `static class` | `GrowPolicy` (internal) | 扩容策略：`Next(current, minimum)` — 初始4，2x增长，上限 int.MaxValue |
| `static class` | `CollectionGuards` (internal) | 参数校验工具：`ThrowIfNegativeCapacity`、`ThrowIfIndexOutOfRange`、`ThrowIfNull` |

### 诊断 (Diagnostics)

| 类型 | 签名 | 说明 |
|------|------|------|
| `static class` | `CollectionMetrics` | 扩容计数器：`FastListGrowCount`、`FastDictionaryGrowCount`、`FastPriorityQueueGrowCount`，提供 `Record*Grow()` 和 `Reset()` |

### 容器 (Containers)

#### `FastList<T>` : `IClearable`
动态数组，支持索引访问和两种移除策略（保序/交换回填）。

| 成员 | 签名 |
|------|------|
| ctor | `FastList(int capacity = 4)` |
| property | `int Count`, `int Capacity` |
| indexer | `T this[int index]` |
| method | `void EnsureCapacity(int minimum)` |
| method | `void Add(T value)` |
| method | `void AddNoResize(T value)` |
| method | `bool Contains(T value)` |
| method | `int IndexOf(T value)` |
| method | `void RemoveAt(int index)` |
| method | `void RemoveAtSwapBack(int index)` |
| method | `void Clear(ClearMode mode = Logical)` |
| method | `Enumerator GetEnumerator()` |
| nested | `struct Enumerator` — `T Current`, `bool MoveNext()` |

#### `FastDictionary<TKey, TValue>` : `IClearable`
开放寻址哈希字典，基于 `IEqualityComparer<TKey>`，使用空闲链表复用 Entry 槽位。

| 成员 | 签名 |
|------|------|
| ctor | `FastDictionary(int capacity = 4, IEqualityComparer<TKey> comparer = null)` |
| property | `int Count`, `int Capacity` |
| method | `bool TryAdd(TKey key, TValue value)` |
| method | `bool TryAddNoResize(TKey key, TValue value)` |
| method | `bool TryGetValue(TKey key, out TValue value)` |
| method | `bool ContainsKey(TKey key)` |
| method | `bool Remove(TKey key)` |
| method | `void Clear(ClearMode mode = Logical)` |
| method | `void ForEach(Action<TKey, TValue> action)` |

#### `FastHashSet<T>` : `IClearable`
基于 `FastDictionary<T, byte>` 的哈希集合，O(1) 增删查。

| 成员 | 签名 |
|------|------|
| ctor | `FastHashSet(int capacity = 4, IEqualityComparer<T> comparer = null)` |
| property | `int Count` |
| method | `bool Add(T value)` |
| method | `bool AddNoResize(T value)` |
| method | `bool Contains(T value)` |
| method | `bool Remove(T value)` |
| method | `void Clear(ClearMode mode = Logical)` |
| method | `void ForEach(Action<T> action)` |

#### `FastPriorityQueue<T>` : `IClearable`
二叉最小堆优先队列，基于 `IComparer<T>`。

| 成员 | 签名 |
|------|------|
| ctor | `FastPriorityQueue(int capacity = 4, IComparer<T> comparer = null)` |
| property | `int Count`, `int Capacity` |
| method | `void Enqueue(T value)` |
| method | `bool EnqueueNoResize(T value)` |
| method | `bool TryPeek(out T value)` |
| method | `bool TryDequeue(out T value)` |
| method | `void Clear(ClearMode mode = Logical)` |

#### `RingBuffer<T>` : `IClearable`
固定容量环形缓冲区，FIFO 语义。

| 成员 | 签名 |
|------|------|
| ctor | `RingBuffer(int capacity)` |
| property | `int Count`, `int Capacity` |
| method | `void Enqueue(T value)` |
| method | `bool EnqueueNoResize(T value)` |
| method | `bool TryDequeue(out T value)` |
| method | `bool TryPeek(out T value)` |
| method | `void Clear(ClearMode mode = Logical)` |

<!-- AUTO-GENERATED-END -->

## 依赖关系

<!-- AUTO-GENERATED: 以下内容由工具自动生成，请勿手工编辑 -->
<!-- AUTO-GENERATED-DEPS-START -->

### 内部依赖（本模块依赖的模块）

无 — Collections 仅依赖 `System` 和 `System.Collections.Generic`。

### 被依赖方（使用本模块的模块）

| 模块 | 使用方式 |
|------|----------|
| `Change.Runtime.Gas` | `AttributeSet` 使用 `FastDictionary<string, Attribute>` |
| `Change.Framework.Fsm` | `StateMachine` 使用 `FastDictionary` 存储状态 |
| `Change.Framework.Cqrs` | `CqrsBus` 使用集合容器存储事件处理器 |
| `Change.Framework.Tests` | 测试套件覆盖所有容器 |

<!-- AUTO-GENERATED-DEPS-END -->

## 使用场景

- **热路径数据存储**：在 Update/FixedUpdate 中使用 `FastList`/`FastDictionary` 替代 `List<T>`/`Dictionary<T>` 避免 GC 分配
- **无扩容操作**：已知容量上限时使用 `AddNoResize`/`TryAddNoResize`/`EnqueueNoResize` 系列方法，扩容时抛异常或返回 false
- **对象池配合**：使用 `Clear(ClearMode.Logical)` 快速重置容器复用它，避免重新创建
- **优先级调度**：`FastPriorityQueue` 适用于任务调度、A* 寻路等需要按优先级出队的场景
- **网络/事件缓冲**：`RingBuffer` 适用于固定窗口的消息缓冲、输入录制等 FIFO 场景

## 注意事项

- **非线程安全**：所有容器均为单线程设计，多线程访问需外部同步
- **ClearMode.Logical 保留引用**：仅重置计数，数组中的引用不会被清空；若需立即释放对象使用 `ZeroMemory`
- **GrowPolicy 为 Internal**：扩容策略不可外部自定义，当前为 2x 增长
- **RingBuffer 固定容量**：不支持动态扩容，Enqueue 在满时抛异常，EnqueueNoResize 返回 false
- **Enumerator 版本检测**：`FastList.Enumerator` 在迭代期间检测集合修改，修改后调用 `MoveNext()` 抛 `InvalidOperationException`

## 经验教训

<!-- 由 /record-lesson 自动追加，请勿手工编辑此标题 -->
