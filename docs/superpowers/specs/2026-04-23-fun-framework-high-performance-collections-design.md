# Fun.Framework 高性能数据结构设计文档（生产级第一期）
> Historical note (2026-04-26): Object pooling is no longer part of `Change.Framework.Collections`.
> `ObjectPool<T>`/`IResettable` were removed during pooling unification; use `Change.Framework.Pooling.Pool<T>` + `IPoolable`.

- 日期：2026-04-23
- 目标目录：`UnityProject/Assets/Fun/Framework/Collections`
- 适用层级：Framework（业务无关）
- 状态：已完成设计评审，待实现规划

## 1. 背景与目标

在日常 Unity 开发中，`List`/`Dictionary` 常见性能问题集中在：

1. 动态扩容导致的瞬时分配和复制成本。
2. 热路径枚举与临时对象造成的 GC 抖动。
3. 不同模块对集合的使用规范不一致，难以系统治理。

本设计的目标是在 `Fun.Framework` 层落一套可复用、生产可用、偏 0GC 的托管高性能容器基建。

核心目标：

1. **0GC 稳定性优先**：运行时热点路径不产生托管分配。
2. **纯托管实现**：不依赖 `Unity.Collections`、`Burst`、`unsafe`。
3. **单线程模型**：默认 Unity 主线程使用，不提供并发安全语义。
4. **API 迁移友好**：接口风格尽量接近 `List`/`Dictionary`。
5. **容器全套首发**：`FastList`、`FastDictionary`、`FastHashSet`、`ObjectPool`、`RingBuffer`、`PriorityQueue`。

## 2. 范围与非目标

## 2.1 第一版范围

- `FastList<T>`
- `FastDictionary<TKey, TValue>`
- `FastHashSet<T>`
- `RingBuffer<T>`
- `PriorityQueue<T>`
- `ObjectPool<T>`
- 统一基础能力（容量策略、参数守卫、枚举器版本一致性）

## 2.2 非目标（第一版不做）

- Native 容器（`NativeArray`/`NativeHashMap` 等）
- 多线程并发容器（锁/无锁）
- 反射扫描、AOT 代码生成、表达式树优化
- 追求与 BCL 100% API 完整兼容
- 会引入分配的便利扩展（例如 LINQ 风格封装）

## 3. 设计约束与关键决策

确认约束：

1. 纯 C# 托管实现，不使用 `unsafe`。
2. 单线程语义。
3. API 偏 BCL 习惯，但只开放 0GC 安全子集。
4. 允许初始化/扩容分配；稳态热路径要求 0GC。
5. 支持 `NoResize` 系列 API 作为性能硬护栏。

选型结论：

- **采用“自研托管容器内核 + 兼容 API 外壳”方案**。
- 理由：在不依赖 Native/unsafe 的前提下，仍可最大化可控性、可诊断性与运行时稳定性，且能统一约束全项目集合使用习惯。

## 4. 架构与目录设计

建议目录结构：

```text
UnityProject/Assets/Fun/Framework/Collections/
  Abstractions/
  Core/
  Containers/
  Diagnostics/
  Tests/
```

模块职责：

1. `Abstractions/`：轻量公共接口（如 `IClearable`、`IResettable`）。
2. `Core/`：共享底层能力（`GrowPolicy`、`CollectionGuards`、版本控制）。
3. `Containers/`：6 个核心容器实现。
4. `Diagnostics/`：开发期开关的统计与断言能力。
5. `Tests/`：NUnit 功能测试 + 分配测试。

命名空间：`Fun.Framework.Collections`

## 5. 统一 API 与 0GC 规范

## 5.1 API 风格

保留高频熟悉接口：

- `Count` / `Capacity`
- `Add` / `Remove` / `Contains`
- `TryGetValue`
- `Clear`
- `EnsureCapacity`

新增性能护栏接口：

- `AddNoResize`
- `TryAddNoResize`
- `EnqueueNoResize`

## 5.2 0GC 运行时规则

1. 所有容器支持显式预热（构造容量或 `EnsureCapacity`）。
2. 热路径调用优先使用 `NoResize` API。
3. 枚举统一使用 `struct Enumerator`，避免装箱与迭代器分配。
4. `Clear()` 默认只重置逻辑长度，不默认清零整块内存。
5. 禁止在核心路径暴露委托闭包必经接口。

## 5.3 行为一致性

- 与 BCL 一致处保持一致（命名、常见异常语义）。
- 与 BCL 不一致处（如 `NoResize` 约束）通过文档明确标注，避免误用。

## 6. 容器详细设计

## 6.1 FastList<T>

内部结构：

- `T[] _items`
- `int _count`
- `int _version`

关键 API：

- `Add` / `AddNoResize`
- `RemoveAt` / `RemoveAtSwapBack`
- `Clear`
- `EnsureCapacity`
- 索引器 `this[int index]`

实现要点：

- `RemoveAtSwapBack` 用于无序高频删除场景。
- 迭代器采用值类型枚举器，修改后旧枚举器失效。

## 6.2 FastDictionary<TKey, TValue>

内部结构：

- `int[] _buckets`
- `Entry[] _entries`（`hashCode`、`next`、`key`、`value`）
- `_count` / `_freeList` / `_freeCount`

关键 API：

- `Add` / `TryAdd` / `TryAddNoResize`
- `TryGetValue`
- `Remove`
- `ContainsKey`

实现要点：

- 链地址法冲突处理。
- 默认支持传入 `IEqualityComparer<TKey>`。
- 保证稳态路径不分配。

## 6.3 FastHashSet<T>

内部结构：

- 与 `FastDictionary` 同构（仅存 key）

关键 API：

- `Add` / `TryAddNoResize`
- `Remove`
- `Contains`

实现要点：

- 复用字典核心策略，降低维护复杂度。

## 6.4 RingBuffer<T>

内部结构：

- `T[] _buffer`
- `int _head` / `int _tail` / `int _count`

关键 API：

- `Enqueue` / `EnqueueNoResize`
- `TryDequeue`
- `TryPeek`
- `Clear`

实现要点：

- 第一版仅支持固定容量模式；`Enqueue` 语义等同 `EnqueueNoResize`。
- 容量满时走显式失败路径（抛异常或 `Try` 风格返回失败），避免运行时隐式扩容。

## 6.5 PriorityQueue<T>

内部结构：

- `T[] _heap`（二叉堆）
- `IComparer<T> _comparer`

关键 API：

- `Enqueue` / `EnqueueNoResize`
- `TryDequeue`
- `TryPeek`

实现要点：

- 比较器在构造阶段固定，运行时不创建临时比较对象。

## 6.6 ObjectPool<T>

内部结构：

- 预分配存储（数组或基于 `FastList` 的栈式结构）
- `Func<T> _factory`
- 可选重置钩子（优先接口化）

关键 API：

- `Rent`
- `Return`
- `Prewarm`
- `TrimExcess`

实现要点：

- `Rent/Return` 稳态不分配。
- 重置策略优先 `IResettable`，避免业务回调误用引入隐藏分配。

## 7. 错误处理与可观测性

错误策略（Fail-fast）：

1. 参数非法：`ArgumentNullException` / `ArgumentOutOfRangeException`。
2. 状态非法（如 `NoResize` 容量不足）：`InvalidOperationException`。
3. 索引越界：按 BCL 语义抛 `ArgumentOutOfRangeException`。

可诊断性：

- 异常文案统一包含 `Count`、`Capacity`、关键索引等上下文。
- `Diagnostics` 提供开发期统计：装载因子、冲突链长度、扩容次数。

## 8. 测试策略与验收标准

## 8.1 功能测试

每个容器至少覆盖：

1. 基本增删查改。
2. 边界输入与异常路径。
3. 枚举器版本失效语义。
4. `EnsureCapacity` 与 `NoResize` 行为。

## 8.2 0GC 分配测试

方法：

- 使用 `GC.GetAllocatedBytesForCurrentThread()`。
- 先 warm-up，再执行高次数循环（如 10k/100k）。
- 对稳态路径做基线扣噪后断言 0 分配。

重点场景：

- `FastList`: `AddNoResize` / `RemoveAtSwapBack`
- `FastDictionary`: `TryGetValue` / `TryAddNoResize`
- `RingBuffer`: `EnqueueNoResize` / `TryDequeue`
- `ObjectPool`: `Rent` / `Return`

## 8.3 DoD（第一版完成定义）

1. 6 个容器实现完成且接口与本文一致。
2. 功能测试全绿。
3. 热路径分配测试达标（稳态 0GC）。
4. 有最小可复现基准脚本与结果文档。
5. 提供容器使用规范（何时预热、何时 `NoResize`、何时 `TrimExcess`）。

## 9. 实施顺序建议

1. `Core` 与 `CollectionGuards` 先行。
2. `FastList`、`FastDictionary`、`FastHashSet`（高频核心）。
3. `RingBuffer`、`PriorityQueue`。
4. `ObjectPool` 与跨容器测试补齐。
5. 基准与文档收尾。

该顺序可以先把最常见热点（List/Dictionary/HashSet）落地，再补队列与池化能力，降低一次性风险。
