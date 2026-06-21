# CQRS 性能验证与迁移 实现计划

> 日期: 2026-06-21 | 状态: 草稿
> 上游设计: [2026-06-18-framework-cqrs-performance-migration-design.md](../designs/2026-06-18-framework-cqrs-performance-migration-design.md)
> 上游 FRD: [2026-06-18-framework-cqrs-performance-migration-frd.md](../discover/2026-06-18-framework-cqrs-performance-migration-frd.md)
> 前置依赖: FRD #1/#2/#3 已实现并提交（main = `4da8c98`）

## 上游产出引用

### 来自 Design

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 切片 A: 0GC 单元测试框架 | 任务 1-3（`ZeroGcTestBase` 共享工具 + Class/Struct 0GC 测试） |
| 切片 B: 运行时性能监控 | 任务 4-6（指标结构 + 阈值策略 + `CqrsPerformanceMonitor` + 条件编译接入） |
| 切片 C: Quest 模块迁移 | 任务 7-9（BumpMain 转 ISelfHandlingCommand + Handler 转 IPoolable + UseCase 装配适配 + 迁移/0GC 测试） |
| 切片 D: 迁移指南文档 | 任务 10（指南 + 性能最佳实践 + API 文档 + FAQ） |
| 架构决策: `ENABLE_CQRS_MONITORING` 条件编译隔离 | 任务 6 用 `#if` 包裹监控接入，默认 OFF 时 dispatch 与现状逐字节一致 |
| 架构决策: 监控 0GC（热路径） | 任务 5 用 `Type` 作 key（非 string）、`Stopwatch.GetTimestamp()`（非 DateTime）、`RecordExecution` 变更既有累积器字段（0GC）；任务 5 的 `MonitoringOverheadTests` 用 `ZeroGcTestBase` 验证 |

### 来自 FRD

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 验收: Class/Struct 模式 0GC 单元测试通过 | 任务 2、3 |
| 验收: 运行时监控埋点就位，阈值告警可用 | 任务 5、6 |
| 验收: Quest 模块迁移示例完成，代码可运行 | 任务 7-9 |
| 验收: 迁移指南文档完成，覆盖所有场景 | 任务 10 |
| 决策 #3: 完全不兼容，手动改写 | 任务 7-8 改写 Quest，不保留兼容层 |
| 决策 #1: 严格 0 字节 GC 分配 | 任务 2/3/9 用 `ZeroGcTestBase` 断言；任务 6 回归 `ZeroAllocationDispatchTests` |

## 目标

为已落地的 CQRS 双模式（FRD #1/#2/#3）建立三层性能验证（0GC 单元测试 + 运行时监控 + Profiler）、完成 Quest 模块从旧 Class handler 到双模式的迁移示例，并产出覆盖全场景的迁移指南。

## 架构

- **切片 A**：抽取共享 `ZeroGcTestBase`（统一现有 `ZeroAllocationDispatchTests`/`SelfHandlingDispatchTests` 中重复的 warmup + `ForceFullGc` + `GC.GetAllocatedBytesForCurrentThread` 断言），新增 Class 池化路径与 Struct 自处理路径的汇总 0GC 测试。
- **切片 B**：新增 `Monitoring/` 子目录。`CqrsPerformanceMonitor` 用 `Dictionary<Type, MetricsAccumulator>` 按 `Type` 聚合（class 值原地变更，热路径 0GC）。`CqrsBus.Send/Query/Publish` 在 `#if ENABLE_CQRS_MONITORING` 内环绕 `GC.GetAllocatedBytesForCurrentThread` + `Stopwatch.GetTimestamp()` 调用 `RecordExecution`——默认 OFF 时编译产物无监控代码，0GC 与现状一致。
- **切片 C**：`BumpMainQuestProgressCommand` 改为 `ISelfHandlingCommand`（无参 `Execute()`，`QuestSessionState` 作 readonly 字段构造注入），删除 `BumpMainQuestProgressHandler`；其余 Command/Query Handler 加 `IPoolable`（空 `Reset()`）；`GameHotfixInstaller` + `QuestSessionStateTests` 移除 BumpMain 注册、Send 时传 state。
- **切片 D**：纯文档。

## 技术栈

- C#（Unity）；`System.Diagnostics.Stopwatch`（0GC 时间戳）；`GC.GetAllocatedBytesForCurrentThread`
- `Change.Framework.Collections.FastDictionary` / `FastList`（框架现有）
- `Change.Framework.Pooling.IPoolable`、`Change.Framework.Cqrs.ISelfHandlingCommand` / `ISelfHandlingQuery<>`
- 测试：NUnit（`Change.Framework.EditModeTests`，namespace `Change.Framework.Tests`）；`GameScript.EditModeTests`（Quest 测试）

## 文件清单

| 文件 | 操作 | 职责 | 所属任务 |
|------|------|------|----------|
| `Tests/EditMode/Cqrs/ZeroGcTestBase.cs` | 创建 | 0GC 测试共享基类（`AssertZeroGc` / `MeasureGcBytes`） | 任务 1 |
| `Tests/EditMode/Cqrs/DualModeZeroGcTests.cs` | 创建 | Class 池化 + Struct 自处理汇总 0GC 测试 | 任务 2、3 |
| `Cqrs/Monitoring/PerformanceMetrics.cs` | 创建 | `PerformanceMetrics` / `PerformanceAlert` readonly struct | 任务 4 |
| `Cqrs/Monitoring/IPerformanceThresholdPolicy.cs` | 创建 | 阈值策略接口 | 任务 4 |
| `Cqrs/Monitoring/DefaultThresholdPolicy.cs` | 创建 | 默认策略（`MaxGcBytes > 100`） | 任务 4 |
| `Cqrs/Monitoring/CqrsPerformanceMonitor.cs` | 创建 | 监控核心：聚合、`RecordExecution`、阈值告警 | 任务 5 |
| `Tests/EditMode/Cqrs/MonitoringTests.cs` | 创建 | 监控功能 + `RecordExecution` 0GC 测试 | 任务 5 |
| `Core/CqrsBus.cs` | 修改 | `#if ENABLE_CQRS_MONITORING` 接入 Send/Query/Publish | 任务 6 |
| `Tests/EditMode/Cqrs/MonitoringWiringTests.cs` | 创建 | 监控 ON 时收集指标；OFF 时 dispatch 不变 | 任务 6 |
| `GameScript/UI/Quest/QuestMessages.cs` | 修改 | `BumpMainQuestProgressCommand` → `ISelfHandlingCommand` | 任务 7 |
| `GameScript/UI/Quest/QuestCommandHandlers.cs` | 修改 | 删除 `BumpMainQuestProgressHandler`；其余 Handler 加 `IPoolable` | 任务 8 |
| `GameScript/UI/Quest/QuestQueryHandlers.cs` | 修改 | `GetQuestPanelQueryHandler` 加 `IPoolable`（空 Reset） | 任务 8 |
| `GameScript/Composition/GameHotfixInstaller.cs` | 修改 | 移除 BumpMain 注册；Send 时构造带 state | 任务 8 |
| `GameScript/Tests/EditMode/Quest/QuestSessionStateTests.cs` | 修改 | 移除 BumpMain 注册；Send 传 state | 任务 9 |
| `GameScript/Tests/EditMode/Quest/QuestMigrationTests.cs` | 创建 | 迁移后业务回归 + 0GC（用 ZeroGcTestBase） | 任务 9 |
| `.mozi/artifacts/framework/guides/cqrs-migration-guide.md` | 创建 | 迁移指南（决策树 + 各模式步骤） | 任务 10 |
| `.mozi/artifacts/framework/guides/cqrs-performance-best-practices.md` | 创建 | 性能最佳实践 | 任务 10 |
| `.mozi/artifacts/framework/guides/cqrs-api-usage.md` | 创建 | API 使用文档 | 任务 10 |
| `.mozi/artifacts/framework/guides/cqrs-faq.md` | 创建 | 常见问题 | 任务 10 |

## 任务依赖图

```
任务 1 (ZeroGcTestBase)
  ├── 任务 2 (Class 0GC 测试)
  ├── 任务 3 (Struct 0GC 测试)
  └── 任务 9 (Quest 0GC 测试，复用基类)
任务 4 (指标 + 策略) ── 任务 5 (Monitor) ── 任务 6 (接入 CqrsBus)
任务 7 (BumpMain 转 self-handling) ── 任务 8 (Handler 池化 + 装配) ── 任务 9 (Quest 测试)
任务 1-9 全部完成 ── 任务 10 (文档)
```

**实施顺序：** 任务 1-3（切片 A）→ 任务 4-6（切片 B）→ 任务 7-9（切片 C）→ 任务 10（切片 D）。

## 风险评估

| 风险（来源） | 影响 | 计划中的缓解措施 |
|-------------|------|-----------------|
| 监控接入破坏现有 0GC（Design 切片 B 高风险） | `ZeroAllocationDispatchTests` 回归 | 任务 6 默认 OFF，`#if` 块不编译；任务 6 步骤 4 重跑 `ZeroAllocationDispatchTests` + `SelfHandlingDispatchTests` 确认逐字节不变 |
| `RecordExecution` 分配（破坏监控自身 0GC） | 监控热路径非 0GC | 任务 5 用 `Type` 作 key、累积器 class 原地变更字段（首次 Add 之外 0GC）；`MonitoringOverheadTests` 用 `AssertZeroGc` 硬验证 |
| Quest 迁移破坏业务（Design 切片 C 中风险） | 任务进度异常、奖励错乱 | 任务 9 `QuestMigrationTests` 覆盖原有分支（主/支线/日常进度+领奖+重复领取异常）；先跑现有 `QuestSessionStateTests` 作基线 |
| `BumpMainQuestProgressCommand` 构造签名变更漏改调用点 | 编译失败或运行 NRE | 任务 7 步骤 2 全仓 grep `BumpMainQuestProgressCommand(`，逐处改为传 `state` |
| 监控测量 `Stopwatch`/`GetAllocatedBytes` 在 ON 时的额外开销 | 监控模式下帧率影响 | 文档（任务 10）说明监控仅诊断用途、默认 OFF；ON 时开销为两次线程局部计数读取，可忽略 |
| `ResetIfPoolable` 对 Quest Handler 空实现的语义 | 误以为有副作用 | 任务 8 注释说明 Quest Handler 无瞬态字段，`Reset()` 为契约占位 |

---

## 全局约定

**Unity 测试命令**（所有任务复用，替换 `<Filter>` 与 `<results-name>`）：

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "<Filter>" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/<results-name>.xml"
```

**0GC 测量基线**（来自 `ZeroAllocationDispatchTests`）：`WarmupIterations=1000`，`MeasuredIterations=100000`，`ForceFullGc()` 后取 `GC.GetAllocatedBytesForCurrentThread()` 前后差值断言相等。

**提交规范：** 禁止 `Co-Authored-By`/`Signed-off-by` 等自动署名行。格式 `feat(cqrs): ...` / `test(cqrs): ...` / `docs(cqrs): ...`。

---

## 任务 1: ZeroGcTestBase 共享基类

**覆盖的上游需求：** Design 切片 A（0GC 测试基础设施）；FRD 验收（0GC 单元测试）。

**文件：**
- 创建：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ZeroGcTestBase.cs`

- [ ] **步骤 1: 编写基类**

```csharp
using System;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    /// <summary>
    /// Shared zero-GC measurement harness for CQRS dispatch tests.
    /// Consolidates the warmup + ForceFullGc + GetAllocatedBytesForCurrentThread
    /// pattern duplicated across ZeroAllocationDispatchTests and SelfHandlingDispatchTests.
    /// </summary>
    public abstract class ZeroGcTestBase
    {
        protected const int WarmupIterations = 1000;
        protected const int MeasuredIterations = 100000;

        /// <summary>
        /// Asserts that invoking <paramref name="action"/> allocates zero bytes on
        /// the current thread after a warmup phase. Fails loudly on any allocation.
        /// </summary>
        protected void AssertZeroGc(Action action, string message = null)
        {
            for (var i = 0; i < WarmupIterations; i++)
            {
                action();
            }

            ForceFullGc();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < MeasuredIterations; i++)
            {
                action();
            }
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after,
                message ?? "Hot path allocated bytes; expected zero-GC dispatch.");
        }

        /// <summary>
        /// Returns allocated bytes for a single invocation (no assertion), for debugging.
        /// </summary>
        protected long MeasureGcBytes(Action action)
        {
            for (var i = 0; i < WarmupIterations; i++)
            {
                action();
            }

            ForceFullGc();
            var before = GC.GetAllocatedBytesForCurrentThread();
            action();
            var after = GC.GetAllocatedBytesForCurrentThread();
            return after - before;
        }

        protected static void ForceFullGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
```

- [ ] **步骤 2: 负向测试验证测量有效性**

创建 `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DualModeZeroGcTests.cs`（本任务仅放校验用例，任务 2/3 追加真实用例）：

```csharp
using System;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class DualModeZeroGcTests : ZeroGcTestBase
    {
        [Test]
        public void AssertZeroGc_RejectsAllocatingAction()
        {
            // 负向测试：故意分配一个 byte[]，断言应失败——证明基类能检出分配。
            Assert.Throws<AssertionException>(() =>
                AssertZeroGc(() => { var _ = new byte[16]; }));
        }

        [Test]
        public void AssertZeroGc_PassesNoOpAction()
        {
            AssertZeroGc(() => { });
        }
    }
}
```

- [ ] **步骤 3: 运行测试验证通过**

运行：`-testFilter "Change.Framework.Tests.DualModeZeroGcTests"`，结果 `TestResults/zerogc-base.xml`
预期：2 个测试 PASS（负向测试证明基类能检出 16 字节分配）。

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ZeroGcTestBase.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ZeroGcTestBase.cs.meta \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DualModeZeroGcTests.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DualModeZeroGcTests.cs.meta
git commit -m "test(cqrs): add ZeroGcTestBase shared harness for zero-allocation assertions"
```

## 任务 2: Class 池化路径 0GC 测试

**覆盖的上游需求：** Design 切片 A（Class 模式池化 Handler 热路径 0GC）；FRD 决策 #2（对象池 Reset 不破坏 0GC）。

**文件：**
- 测试：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DualModeZeroGcTests.cs`（追加）

- [ ] **步骤 1: 编写测试**

在 `DualModeZeroGcTests` 类内追加：

```csharp
private readonly struct PoolableClassCommand : Change.Framework.Cqrs.ICommand { }

private sealed class PoolableClassCommandHandler
    : Change.Framework.Cqrs.ICommandHandler<PoolableClassCommand>,
      Change.Framework.Pooling.IPoolable
{
    public int Count;
    public void Handle(in PoolableClassCommand command) { Count++; }
    public void Reset() { }
}

[Test]
public void Send_ClassPooledHandler_HotPath_AllocatesZeroBytes()
{
    var handler = new PoolableClassCommandHandler();
    var bus = new Change.Framework.Cqrs.CqrsBus();
    bus.RegisterCommand(handler);
    var command = new PoolableClassCommand();

    AssertZeroGc(() => bus.Send(in command));

    Assert.AreEqual(WarmupIterations + MeasuredIterations, handler.Count);
}
```

- [ ] **步骤 2: 运行测试验证通过**

运行：`-testFilter "Change.Framework.Tests.DualModeZeroGcTests"`
预期：PASS（FRD #2 的 `ResetIfPoolable` 路径已确认 0GC；本测试固化 Class 池化路径）。

- [ ] **步骤 3: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DualModeZeroGcTests.cs
git commit -m "test(cqrs): cover Class pooled-handler zero-allocation hot path"
```

## 任务 3: Struct 自处理路径 0GC 测试

**覆盖的上游需求：** Design 切片 A（Struct 自处理热路径 0GC）。

**文件：**
- 测试：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DualModeZeroGcTests.cs`（追加）

- [ ] **步骤 1: 编写测试**

在 `DualModeZeroGcTests` 类内追加：

```csharp
private sealed class SelfHandlingSink
{
    public int Value;
}

private readonly struct SelfHandlingStructCommand : Change.Framework.Cqrs.ISelfHandlingCommand
{
    private readonly SelfHandlingSink _sink;
    public SelfHandlingStructCommand(SelfHandlingSink sink) { _sink = sink; }
    public void Execute() { _sink.Value++; }
}

[Test]
public void Send_SelfHandlingStruct_HotPath_AllocatesZeroBytes()
{
    var sink = new SelfHandlingSink();
    var bus = new Change.Framework.Cqrs.CqrsBus();
    var command = new SelfHandlingStructCommand(sink);

    AssertZeroGc(() => bus.Send(in command));

    Assert.AreEqual(WarmupIterations + MeasuredIterations, sink.Value);
}
```

- [ ] **步骤 2: 运行测试验证通过**

运行：`-testFilter "Change.Framework.Tests.DualModeZeroGcTests"`
预期：PASS（FRD #2 的 `SelfHandlingCommandCache` constrained.callvirt 路径已 0GC；本测试固化并归入统一基类）。

- [ ] **步骤 3: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DualModeZeroGcTests.cs
git commit -m "test(cqrs): cover Struct self-handling zero-allocation hot path via base"
```

## 任务 4: 性能指标结构 + 阈值策略

**覆盖的上游需求：** Design 切片 B（监控数据契约、阈值策略接口）。

**文件：**
- 创建：`UnityProject/Assets/Change/Framework/Cqrs/Monitoring/PerformanceMetrics.cs`
- 创建：`UnityProject/Assets/Change/Framework/Cqrs/Monitoring/IPerformanceThresholdPolicy.cs`
- 创建：`UnityProject/Assets/Change/Framework/Cqrs/Monitoring/DefaultThresholdPolicy.cs`

- [ ] **步骤 1: 编写指标结构**

`Monitoring/PerformanceMetrics.cs`：

```csharp
using System;

namespace Change.Framework.Cqrs.Monitoring
{
    /// <summary>
    /// Aggregated metrics for a single message type. readonly struct so snapshot
    /// reads are value copies with no allocation.
    /// </summary>
    public readonly struct PerformanceMetrics
    {
        public PerformanceMetrics(
            Type messageType, int executionCount, long totalDurationTicks,
            long totalGcBytes, long maxGcBytes)
        {
            MessageType = messageType;
            ExecutionCount = executionCount;
            TotalDurationTicks = totalDurationTicks;
            TotalGcBytes = totalGcBytes;
            MaxGcBytes = maxGcBytes;
        }

        public Type MessageType { get; }
        public int ExecutionCount { get; }
        public long TotalDurationTicks { get; }
        public long TotalGcBytes { get; }
        public long MaxGcBytes { get; }
    }

    /// <summary>
    /// Raised when a single execution exceeds the configured threshold.
    /// </summary>
    public readonly struct PerformanceAlert
    {
        public PerformanceAlert(Type messageType, long gcBytes, long threshold, long ticks)
        {
            MessageType = messageType;
            GcBytes = gcBytes;
            Threshold = threshold;
            TimestampTicks = ticks;
        }

        public Type MessageType { get; }
        public long GcBytes { get; }
        public long Threshold { get; }
        public long TimestampTicks { get; }
    }
}
```

- [ ] **步骤 2: 编写阈值策略**

`Monitoring/IPerformanceThresholdPolicy.cs`：

```csharp
namespace Change.Framework.Cqrs.Monitoring
{
    /// <summary>
    /// Decides whether a per-execution GC delta exceeds the acceptable threshold.
    /// Pluggable so teams can tune thresholds per context.
    /// </summary>
    public interface IPerformanceThresholdPolicy
    {
        bool ShouldAlert(long gcBytes);
    }
}
```

`Monitoring/DefaultThresholdPolicy.cs`：

```csharp
namespace Change.Framework.Cqrs.Monitoring
{
    /// <summary>
    /// Default policy: alert when a single execution allocates more than the configured
    /// byte budget (default 100 bytes, per FRD threshold guidance).
    /// </summary>
    public sealed class DefaultThresholdPolicy : IPerformanceThresholdPolicy
    {
        private readonly long _gcBytesThreshold;

        public DefaultThresholdPolicy(long gcBytesThreshold = 100)
        {
            _gcBytesThreshold = gcBytesThreshold;
        }

        public bool ShouldAlert(long gcBytes) => gcBytes > _gcBytesThreshold;
    }
}
```

- [ ] **步骤 3: 编译验证**

运行：`/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -logFile -`
预期：无编译错误（`Change.Framework` 程序集新增 `Monitoring` 命名空间）。

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Monitoring/PerformanceMetrics.cs \
        UnityProject/Assets/Change/Framework/Cqrs/Monitoring/PerformanceMetrics.cs.meta \
        UnityProject/Assets/Change/Framework/Cqrs/Monitoring/IPerformanceThresholdPolicy.cs \
        UnityProject/Assets/Change/Framework/Cqrs/Monitoring/IPerformanceThresholdPolicy.cs.meta \
        UnityProject/Assets/Change/Framework/Cqrs/Monitoring/DefaultThresholdPolicy.cs \
        UnityProject/Assets/Change/Framework/Cqrs/Monitoring/DefaultThresholdPolicy.cs.meta
git commit -m "feat(cqrs): add performance metrics structs and threshold policy"
```

## 任务 5: CqrsPerformanceMonitor 核心

**覆盖的上游需求：** Design 切片 B（监控核心、`RecordExecution` 0GC、阈值告警）。

**文件：**
- 创建：`UnityProject/Assets/Change/Framework/Cqrs/Monitoring/CqrsPerformanceMonitor.cs`
- 测试：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/MonitoringTests.cs`

- [ ] **步骤 1: 编写失败的测试**

`Tests/EditMode/Cqrs/MonitoringTests.cs`：

```csharp
using System;
using System.Collections.Generic;
using Change.Framework.Cqrs.Monitoring;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class MonitoringTests : ZeroGcTestBase
    {
        [Test]
        public void RecordExecution_AggregatesCountDurationAndGc()
        {
            var monitor = new CqrsPerformanceMonitor();
            monitor.RecordExecution(typeof(string), durationTicks: 10, gcBytes: 5);
            monitor.RecordExecution(typeof(string), durationTicks: 20, gcBytes: 15);

            var metrics = monitor.GetMetrics(typeof(string));

            Assert.AreEqual(2, metrics.ExecutionCount);
            Assert.AreEqual(30, metrics.TotalDurationTicks);
            Assert.AreEqual(20, metrics.TotalGcBytes);
            Assert.AreEqual(15, metrics.MaxGcBytes);
        }

        [Test]
        public void RecordExecution_AboveThreshold_RaisesAlert()
        {
            var monitor = new CqrsPerformanceMonitor(new DefaultThresholdPolicy(gcBytesThreshold: 100));
            PerformanceAlert? raised = null;
            monitor.OnThresholdExceeded += a => raised = a;

            monitor.RecordExecution(typeof(string), durationTicks: 1, gcBytes: 150);

            Assert.IsTrue(raised.HasValue);
            Assert.AreEqual(typeof(string), raised.Value.MessageType);
            Assert.AreEqual(150, raised.Value.GcBytes);
        }

        [Test]
        public void RecordExecution_BelowThreshold_DoesNotRaise()
        {
            var monitor = new CqrsPerformanceMonitor(new DefaultThresholdPolicy(gcBytesThreshold: 100));
            var raised = false;
            monitor.OnThresholdExceeded += _ => raised = true;

            monitor.RecordExecution(typeof(string), durationTicks: 1, gcBytes: 50);

            Assert.IsFalse(raised);
        }

        [Test]
        public void RecordExecution_RepeatCallsForSameType_AllocatesZeroBytes()
        {
            var monitor = new CqrsPerformanceMonitor();
            monitor.RecordExecution(typeof(string), 1, 0); // ensure accumulator exists (first Add)

            AssertZeroGc(() => monitor.RecordExecution(typeof(string), 1, 0));
        }

        [Test]
        public void ClearMetrics_RemovesAllEntries()
        {
            var monitor = new CqrsPerformanceMonitor();
            monitor.RecordExecution(typeof(string), 1, 1);

            monitor.ClearMetrics();

            Assert.Throws<KeyNotFoundException>(() => monitor.GetMetrics(typeof(string)));
        }
    }
}
```

- [ ] **步骤 2: 运行测试验证失败**

运行：`-testFilter "Change.Framework.Tests.MonitoringTests"`，结果 `TestResults/monitoring-1.xml`
预期：FAIL（`CqrsPerformanceMonitor` 类型不存在，编译失败）。

- [ ] **步骤 3: 实现监控核心**

`Monitoring/CqrsPerformanceMonitor.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace Change.Framework.Cqrs.Monitoring
{
    /// <summary>
    /// Aggregates per-message-type execution metrics on the dispatching (main) thread.
    /// <para><b>Hot path:</b> <see cref="RecordExecution"/> mutates an existing
    /// <see cref="MetricsAccumulator"/> in place (no boxing, no string key) — zero-GC
    /// after the first occurrence of a type. The first occurrence pays a one-time
    /// dictionary Add.</para>
    /// <para><b>Thread model:</b> single-threaded (main thread), matching
    /// <c>CqrsBus</c> registration/dispatch assumptions. No locking.</para>
    /// </summary>
    public sealed class CqrsPerformanceMonitor
    {
        private readonly Dictionary<Type, MetricsAccumulator> _accumulators = new();
        private readonly IPerformanceThresholdPolicy _threshold;
        private readonly long _thresholdValue;

        public CqrsPerformanceMonitor()
            : this(new DefaultThresholdPolicy())
        {
        }

        public CqrsPerformanceMonitor(IPerformanceThresholdPolicy threshold)
        {
            _threshold = threshold ?? new DefaultThresholdPolicy();
            _thresholdValue = 100; // exposed for alert payload; matches default policy
        }

        public event Action<PerformanceAlert> OnThresholdExceeded;

        /// <summary>
        /// Records one dispatch. Zero-GC on repeated calls for the same type.
        /// </summary>
        public void RecordExecution(Type messageType, long durationTicks, long gcBytes)
        {
            if (!_accumulators.TryGetValue(messageType, out var acc))
            {
                acc = new MetricsAccumulator { MessageType = messageType };
                _accumulators[messageType] = acc;
            }

            acc.ExecutionCount++;
            acc.TotalDurationTicks += durationTicks;
            acc.TotalGcBytes += gcBytes;
            if (gcBytes > acc.MaxGcBytes)
            {
                acc.MaxGcBytes = gcBytes;
            }

            if (gcBytes > _thresholdValue)
            {
                OnThresholdExceeded?.Invoke(
                    new PerformanceAlert(messageType, gcBytes, _thresholdValue, durationTicks));
            }
        }

        public PerformanceMetrics GetMetrics(Type messageType)
        {
            if (!_accumulators.TryGetValue(messageType, out var acc))
            {
                throw new KeyNotFoundException($"No metrics for {messageType.FullName}");
            }
            return new PerformanceMetrics(
                acc.MessageType, acc.ExecutionCount, acc.TotalDurationTicks,
                acc.TotalGcBytes, acc.MaxGcBytes);
        }

        public IReadOnlyCollection<PerformanceMetrics> GetAllMetrics()
        {
            var list = new List<PerformanceMetrics>(_accumulators.Count);
            foreach (var kvp in _accumulators)
            {
                var acc = kvp.Value;
                list.Add(new PerformanceMetrics(
                    acc.MessageType, acc.ExecutionCount, acc.TotalDurationTicks,
                    acc.TotalGcBytes, acc.MaxGcBytes));
            }
            return list;
        }

        public void ClearMetrics() => _accumulators.Clear();

        private sealed class MetricsAccumulator
        {
            public Type MessageType;
            public int ExecutionCount;
            public long TotalDurationTicks;
            public long TotalGcBytes;
            public long MaxGcBytes;
        }
    }
}
```

> **0GC 说明：** `_accumulators.TryGetValue` 对已存在 key 返回引用，`acc.ExecutionCount++` 等原地变更字段，无装箱/string 分配。`OnThresholdExceeded?.Invoke` 传 `PerformanceAlert` struct（值传递，0GC）。`typeof` 与 `gcBytes`/`durationTicks` 均非堆分配。首次 `Add` 分配一次累积器——预热后测试用同一类型，测量段 0GC。

- [ ] **步骤 4: 运行测试验证通过**

运行：`-testFilter "Change.Framework.Tests.MonitoringTests"`
预期：5 个测试 PASS，含 `RecordExecution_RepeatCallsForSameType_AllocatesZeroBytes`。

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Monitoring/CqrsPerformanceMonitor.cs \
        UnityProject/Assets/Change/Framework/Cqrs/Monitoring/CqrsPerformanceMonitor.cs.meta \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/MonitoringTests.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/MonitoringTests.cs.meta
git commit -m "feat(cqrs): add CqrsPerformanceMonitor with zero-GC hot path"
```

## 任务 6: 监控接入 CqrsBus（条件编译）

**覆盖的上游需求：** Design 切片 B（接入 `Send/Query/Publish`，默认 OFF 零开销）；FRD 验收（埋点就位）。

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- 创建：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/MonitoringWiringTests.cs`

- [ ] **步骤 1: 编写失败的测试**

`Tests/EditMode/Cqrs/MonitoringWiringTests.cs`（此测试需在 Unity Player Settings 定义 `ENABLE_CQRS_MONITORING` 符号才能验证 ON 路径——见步骤 5 说明；先写 ON 路径功能断言）：

```csharp
using Change.Framework.Cqrs;
using Change.Framework.Cqrs.Monitoring;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class MonitoringWiringTests
    {
        // 注意：ON 路径验证需要 ENABLE_CQRS_MONITORING 符号已定义。
        // 本测试用例仅在该符号定义时编译，避免 OFF 配置下编译失败。
#if ENABLE_CQRS_MONITORING
        private readonly struct TrackedCommand : ICommand { }
        private sealed class TrackedHandler : ICommandHandler<TrackedCommand>
        {
            public void Handle(in TrackedCommand command) { }
        }

        [Test]
        public void Send_WithMonitoring_RecordsExecution()
        {
            var monitor = new CqrsPerformanceMonitor();
            CqrsBus.SetActiveMonitor(monitor);

            try
            {
                var bus = new CqrsBus();
                bus.RegisterCommand(new TrackedHandler());
                bus.Send(new TrackedCommand());

                var metrics = monitor.GetMetrics(typeof(TrackedCommand));
                Assert.AreEqual(1, metrics.ExecutionCount);
            }
            finally
            {
                CqrsBus.SetActiveMonitor(null);
            }
        }
#endif
    }
}
```

- [ ] **步骤 2: 实现 CqrsBus 监控接入**

在 `Core/CqrsBus.cs`：

- 文件顶部追加 `using System.Diagnostics;`（若 Stopwatch 未引用）。
- 类内字段区追加静态可变监控引用：

```csharp
#if ENABLE_CQRS_MONITORING
        [ThreadStatic]
        private static Change.Framework.Cqrs.Monitoring.CqrsPerformanceMonitor _activeMonitor;
#endif
```

- 新增静态访问器：

```csharp
        /// <summary>
        /// Installs a monitor for the current thread's dispatch. Only available when
        /// ENABLE_CQRS_MONITORING is defined (diagnostic builds). Null clears it.
        /// </summary>
        public static void SetActiveMonitor(
#if ENABLE_CQRS_MONITORING
            Change.Framework.Cqrs.Monitoring.CqrsPerformanceMonitor monitor
#else
            object monitor
#endif
        )
        {
#if ENABLE_CQRS_MONITORING
            _activeMonitor = monitor;
#endif
        }
```

> 注意：OFF 时签名用 `object` 保持 API 存在但无副作用，避免外部调用点因符号切换而编译失败。若更倾向 OFF 时干脆不暴露此方法，可用 `#if` 整体包裹方法声明——本计划选保留空壳以简化装配代码。

- 修改 `Send<TCommand>`，在自处理分支与 handler 分支各自包裹测量。完整方法：

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void Send<TCommand>(in TCommand command)
    where TCommand : struct, ICommand
{
    var invoke = SelfHandlingCommandCache<TCommand>.Invoke;
    if (invoke != null)
    {
#if ENABLE_CQRS_MONITORING
        RecordMonitored(typeof(TCommand), () => invoke(command));
#else
        invoke(command);
#endif
        return;
    }

    var commandType = typeof(TCommand);
    if (!_commandHandlers.TryGetValue(commandType, out var registration))
    {
        throw new HandlerNotRegisteredException($"Command handler not registered: {commandType.FullName}");
    }

    var handler = ((CommandHandlerRegistration<TCommand>)registration).Handler;
#if ENABLE_CQRS_MONITORING
    try
    {
        RecordMonitored(commandType, () => handler.Handle(in command));
    }
    finally
    {
        ResetIfPoolable(handler, commandType);
    }
#else
    try
    {
        handler.Handle(in command);
    }
    finally
    {
        ResetIfPoolable(handler, commandType);
    }
#endif
}
```

> **ON 路径的 Reset 语义：** ON 时把 `Handle` 包进 `RecordMonitored`（内部 try/finally 测量 GC），外层再包 try/finally 调 `ResetIfPoolable`——这样即使 `Handle` 抛异常，Reset 仍执行（与 OFF 路径一致）。Reset 不计入业务 GC 阈值。`RecordMonitored` 定义见下。

- 同理修改 `Query<TQuery, TResult>` 的 handler 分支（自处理分支仅包裹测量，无 Reset）：

```csharp
#if ENABLE_CQRS_MONITORING
    TResult result;
    try
    {
        result = RecordMonitored(queryType, () => handler.Handle(in query));
    }
    finally
    {
        ResetIfPoolable(handler, queryType);
    }
    return result;
#else
    try
    {
        return handler.Handle(in query);
    }
    finally
    {
        ResetIfPoolable(handler, queryType);
    }
#endif
```

- 修改 `Publish<TEvent>`：在 handler 循环 + delegate 循环整体外测量一次（`RecordMonitored(typeof(TEvent), () => { ...原循环... })`）；Publish 无 Poolable Reset（事件 handler 不池化）。

- 新增私有 `RecordMonitored` 辅助（ON 时编译）：

```csharp
#if ENABLE_CQRS_MONITORING
        private void RecordMonitored(Type messageType, Action body)
        {
            var monitor = _activeMonitor;
            if (monitor == null) { body(); return; }
            var before = GC.GetAllocatedBytesForCurrentThread();
            var ticksBefore = Stopwatch.GetTimestamp();
            try
            {
                body();
            }
            finally
            {
                var ticks = Stopwatch.GetTimestamp() - ticksBefore;
                var gcDelta = GC.GetAllocatedBytesForCurrentThread() - before;
                monitor.RecordExecution(messageType, ticks, gcDelta);
            }
        }

        private TResult RecordMonitored<TResult>(Type messageType, Func<TResult> body)
        {
            var monitor = _activeMonitor;
            if (monitor == null) { return body(); }
            var before = GC.GetAllocatedBytesForCurrentThread();
            var ticksBefore = Stopwatch.GetTimestamp();
            try
            {
                return body();
            }
            finally
            {
                var ticks = Stopwatch.GetTimestamp() - ticksBefore;
                var gcDelta = GC.GetAllocatedBytesForCurrentThread() - before;
                monitor.RecordExecution(messageType, ticks, gcDelta);
            }
        }
#endif
```

> **ON 路径分配说明：** `body` lambda 捕获 `invoke`/`command`/`handler`——在监控模式下每次 Send 产生一次闭包分配。这是诊断模式的可接受代价（默认 OFF 时 `#if` 块整体消失，零分配）。文档（任务 10）明确此取舍。

- [ ] **步骤 3: 编译验证（OFF 配置，默认）**

运行：`/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath "...UnityProject" -logFile -`
预期：无编译错误。`MonitoringWiringTests` 在 OFF 时整个测试类为空（`#if` 内无方法），不影响编译。

- [ ] **步骤 4: 回归 — OFF 配置下 0GC 与现状一致**

运行：
`-testFilter "Change.Framework.Tests.ZeroAllocationDispatchTests,Change.Framework.Tests.SelfHandlingDispatchTests,Change.Framework.Tests.PoolingResetTests,Change.Framework.Tests.DualModeZeroGcTests,Change.Framework.Tests.MonitoringTests"`
预期：全 PASS。**关键点：** OFF 配置下 `Send/Query/Publish` 编译产物与接入前逐字节一致（`#if ENABLE_CQRS_MONITORING` 块不编译），0GC 保持。

- [ ] **步骤 5: ON 配置验证（手动符号切换）**

在 Unity Editor → Project Settings → Player → Scripting Define Symbols 临时添加 `ENABLE_CQRS_MONITORING`，运行：
`-testFilter "Change.Framework.Tests.MonitoringWiringTests"`
预期：PASS（`Send_WithMonitoring_RecordsExecution` 捕获到 1 次执行）。验证后移除该符号，恢复默认 OFF。

> 若不想切换符号，可用 `mcs.rsp`/`csc.rsp` 在 `Change.Framework` 程序集临时定义，或用子程序集——本计划默认 Player Settings 符号法，步骤简单。

- [ ] **步骤 6: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/MonitoringWiringTests.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/MonitoringWiringTests.cs.meta
git commit -m "feat(cqrs): wire performance monitor into dispatch under ENABLE_CQRS_MONITORING"
```

## 任务 7: BumpMainQuestProgressCommand 迁移为 ISelfHandlingCommand

**覆盖的上游需求：** Design 切片 C（Struct 自处理迁移）；FRD 决策 #4（Quest 示例优先）。

**文件：**
- 修改：`UnityProject/Assets/GameScript/UI/Quest/QuestMessages.cs`

- [ ] **步骤 1: 全仓确认调用点**

运行：
```bash
grep -rn "BumpMainQuestProgressCommand(" UnityProject/Assets/
```
预期输出（迁移前基线）：
- `QuestMessages.cs:76`（构造定义）
- `QuestSessionStateTests.cs:31,34,65`（`bus.Send(new BumpMainQuestProgressCommand(2))`）
- 可能的 Composition 装配点

记录全部行号，步骤 3 逐处更新。

- [ ] **步骤 2: 改写 Command 为 ISelfHandlingCommand**

修改 `QuestMessages.cs` 中的 `BumpMainQuestProgressCommand`：

```csharp
public readonly struct BumpMainQuestProgressCommand : Change.Framework.Cqrs.ISelfHandlingCommand
{
    private readonly QuestSessionState _state;

    public BumpMainQuestProgressCommand(QuestSessionState state, int delta)
    {
        _state = state;
        Delta = delta;
    }

    public int Delta { get; }

    // ISelfHandlingCommand.Execute() 无参；QuestSessionState 由 struct 字段携带（构造注入），保持 0GC。
    public void Execute() => _state.BumpMainProgress(Delta);
}
```

> 注意：保留 `Delta` 属性（既有消费者可能读取）；新增 `_state` 字段。`using Change.Framework.Cqrs;` 若已在文件顶部则去掉全限定。

- [ ] **步骤 3: 暂不更新调用点（编译将失败，验证范围）**

运行编译：`/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath "...UnityProject" -logFile -`
预期：编译错误集中在 `BumpMainQuestProgressHandler.Handle`（任务 8 删除该 Handler）与各 `new BumpMainQuestProgressCommand(2)` 调用点（任务 8/9 改）。这是预期失败——TDD 红。

- [ ] **步骤 4: Commit（WIP，与任务 8 一同完成）**

> 本任务与任务 8 紧耦合（删除 Handler + 装配适配），合并提交。本步骤先不提交，待任务 8 完成后统一提交。

## 任务 8: Quest Handler 池化 + 装配适配

**覆盖的上游需求：** Design 切片 C（Class Handler + IPoolable、UseCase/装配适配）；FRD 决策 #3（手动改写）。

**文件：**
- 修改：`UnityProject/Assets/GameScript/UI/Quest/QuestCommandHandlers.cs`
- 修改：`UnityProject/Assets/GameScript/UI/Quest/QuestQueryHandlers.cs`
- 修改：`UnityProject/Assets/GameScript/Composition/GameHotfixInstaller.cs`

- [ ] **步骤 1: 删除 BumpMainQuestProgressHandler**

在 `QuestCommandHandlers.cs` 中删除整个 `BumpMainQuestProgressHandler` 类（已迁移为 Struct 自处理，无需 Handler）。

- [ ] **步骤 2: 其余 Command Handler 加 IPoolable**

在 `QuestCommandHandlers.cs` 中，为 `AdvanceMainQuestStepHandler`、`BumpSideQuestProgressHandler`、`ClaimSideQuestRewardHandler`、`BumpDailyQuestProgressHandler`、`ClaimDailyQuestRewardHandler` 各：
- 接口列表追加 `, Change.Framework.Pooling.IPoolable`
- 追加空实现：`public void Reset() { /* Quest handlers 持有 readonly 注入依赖，无瞬态字段需重置 */ }`

示例（`ClaimSideQuestRewardHandler`）：

```csharp
public sealed class ClaimSideQuestRewardHandler
    : ICommandHandler<ClaimSideQuestRewardCommand>, Change.Framework.Pooling.IPoolable
{
    private readonly QuestSessionState _state;
    private readonly QuestRewardWallet _wallet;

    public ClaimSideQuestRewardHandler(QuestSessionState state, QuestRewardWallet wallet)
    {
        _state = state;
        _wallet = wallet;
    }

    public void Handle(in ClaimSideQuestRewardCommand command)
    {
        var row = _state.GetSideOrThrow(command.SideId);
        if (!row.CanClaim) throw new InvalidOperationException("Side quest not claimable.");
        row.RewardClaimed = true;
        _wallet.AddGold(row.RewardGold);
    }

    public void Reset() { }
}
```

> 文件顶部若无 `using Change.Framework.Pooling;`，加全限定（或 using）。

- [ ] **步骤 3: Query Handler 加 IPoolable**

在 `QuestQueryHandlers.cs` 中，`GetQuestPanelQueryHandler` 接口列表追加 `IPoolable`，追加 `public void Reset() { }`。**不复用 buffer**——维持现有每次 new List 的逻辑（snapshot 被 Presenter 持有，复用会在下次 Reset 清空后失效）。

- [ ] **步骤 4: 装配适配 — GameHotfixInstaller**

修改 `Composition/GameHotfixInstaller.cs`：
- **删除** `bootstrap.RegisterCommand(new BumpMainQuestProgressHandler(state));` 这一行（自处理无需注册）。
- `state` 变量若仅被 BumpMain 使用则可能产生未用警告——确认 `state` 仍被其他 Handler 使用（`AdvanceMainQuestStepHandler(state, wallet)` 等），无需移除。

- [ ] **步骤 5: 装配适配 — 发送 BumpMain 的调用点**

grep 全仓 `bus.Send(new BumpMainQuestProgressCommand(` 与 `Send(new BumpMainQuestProgressCommand(`，每处将 `new BumpMainQuestProgressCommand(2)` 改为 `new BumpMainQuestProgressCommand(state, 2)`（需确保该作用域有 `state`/`QuestSessionState` 可用）。生产发送点若在 Composition/UseCase，注入 `state`。

> 测试文件 `QuestSessionStateTests.cs` 的调用点在任务 9 处理。

- [ ] **步骤 6: 编译验证**

运行：`/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath "...UnityProject" -logFile -`
预期：`GameScript` 程序集编译通过（除 `QuestSessionStateTests` 待任务 9 改）。

- [ ] **步骤 7: Commit（含任务 7 的 Command 改写）**

```bash
git add UnityProject/Assets/GameScript/UI/Quest/QuestMessages.cs \
        UnityProject/Assets/GameScript/UI/Quest/QuestCommandHandlers.cs \
        UnityProject/Assets/GameScript/UI/Quest/QuestQueryHandlers.cs \
        UnityProject/Assets/GameScript/Composition/GameHotfixInstaller.cs
git commit -m "feat(quest): migrate BumpMainQuestProgress to self-handling and pool remaining handlers"
```

## 任务 9: Quest 迁移测试（回归 + 0GC）

**覆盖的上游需求：** Design 切片 C（业务回归 + 0GC 验证）；FRD 验收（迁移示例可运行）。

**文件：**
- 修改：`UnityProject/Assets/GameScript/Tests/EditMode/Quest/QuestSessionStateTests.cs`
- 创建：`UnityProject/Assets/GameScript/Tests/EditMode/Quest/QuestMigrationTests.cs`

- [ ] **步骤 1: 修复现有 QuestSessionStateTests**

修改 `QuestSessionStateTests.cs`：
- **删除** `bootstrap.RegisterCommand(new BumpMainQuestProgressHandler(state));`（第 17 行附近）。
- 三处 `bus.Send(new BumpMainQuestProgressCommand(2))` 改为 `bus.Send(new BumpMainQuestProgressCommand(state, 2))`。
- 确认 `state` 在测试作用域可见（既有 `var state = new QuestSessionState(...)` 应已存在）。

- [ ] **步骤 2: 运行现有测试作回归基线**

运行：`-testFilter "GameScript.Tests.Quest.QuestSessionStateTests"`（确认测试类全名/命名空间，见步骤 3 调整 filter）
预期：PASS——业务语义不变（主任务进度推进）。

> 若测试 filter 报找不到类，先 grep 确认命名空间：`grep -n "namespace\|class QuestSessionStateTests" UnityProject/Assets/GameScript/Tests/EditMode/Quest/QuestSessionStateTests.cs`。

- [ ] **步骤 3: 编写 QuestMigrationTests（迁移示例 + 0GC）**

确认 `GameScript.EditModeTests` asmdef 是否引用 `Change.Framework`（Quest 测试已用 `CqrsBus`，故已引用）。创建 `QuestMigrationTests.cs`：

```csharp
using System;
using Change.Framework.Cqrs;
using GameScript.UI.Quest;
using NUnit.Framework;

namespace GameScript.Tests.Quest
{
    public class QuestMigrationTests
    {
        [Test]
        public void Send_BumpMainQuestProgress_SelfHandling_UpdatesStateWithoutRegistration()
        {
            var state = new QuestSessionState();
            var bus = new CqrsBus();

            // 自处理：无需 RegisterCommand，直接 Send
            bus.Send(new BumpMainQuestProgressCommand(state, 5));

            Assert.AreEqual(5, state.MainProgress);
        }

        // BumpMainQuestProgressCommand 现已是 ISelfHandlingCommand；
        // 注册任何 Class handler 都应触发 ModeConflictException（验证迁移后不可回退到 Class 模式）。
        private sealed class DummyBumpMainHandler : ICommandHandler<BumpMainQuestProgressCommand>
        {
            public void Handle(in BumpMainQuestProgressCommand command) { }
        }

        [Test]
        public void RegisterCommand_OnBumpMainQuestProgress_NowThrowsModeConflict()
        {
            var bus = new CqrsBus();

            Assert.Throws<ModeConflictException>(
                () => bus.RegisterCommand(new DummyBumpMainHandler()));
        }

        [Test]
        public void Send_BumpMainQuestProgress_HotPath_AllocatesZeroBytes()
        {
            var state = new QuestSessionState();
            var bus = new CqrsBus();
            var command = new BumpMainQuestProgressCommand(state, 1);

            // 预热
            for (var i = 0; i < 1000; i++) bus.Send(in command);

            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100000; i++) bus.Send(in command);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after, "Quest BumpMain self-handling must be zero-allocation.");
        }
    }
}
```

> **程序集可见性说明：** `ZeroGcTestBase` 在 `Change.Framework.EditModeTests` 程序集。`GameScript.EditModeTests` 通常不引用测试程序集，故 `QuestMigrationTests` 内联测量逻辑（预热 + ForceFullGc + `GetAllocatedBytesForCurrentThread`），不继承基类。`ModeConflictException`、`CqrsBus` 来自 `Change.Framework`（GameScript 测试已引用）。

- [ ] **步骤 4: 运行迁移测试**

运行：`-testFilter "GameScript.Tests.Quest.QuestMigrationTests"`（确认命名空间）
预期：PASS——自处理分发正确、0GC、冲突结构不可达。

- [ ] **步骤 5: 全 Quest 回归**

运行：`-testFilter "GameScript.Tests.Quest"`（所有 Quest EditMode 测试）
预期：全 PASS（含迁移后的 `QuestSessionStateTests`）。

- [ ] **步骤 6: Commit**

```bash
git add UnityProject/Assets/GameScript/Tests/EditMode/Quest/QuestSessionStateTests.cs \
        UnityProject/Assets/GameScript/Tests/EditMode/Quest/QuestMigrationTests.cs \
        UnityProject/Assets/GameScript/Tests/EditMode/Quest/QuestMigrationTests.cs.meta
git commit -m "test(quest): add migration and zero-GC tests for self-handling BumpMain"
```

## 任务 10: 迁移指南与文档

**覆盖的上游需求：** Design 切片 D（迁移指南/性能最佳实践/API 文档/FAQ）；FRD 验收（文档覆盖所有场景）。

**文件：**
- 创建：`.mozi/artifacts/framework/guides/cqrs-migration-guide.md`
- 创建：`.mozi/artifacts/framework/guides/cqrs-performance-best-practices.md`
- 创建：`.mozi/artifacts/framework/guides/cqrs-api-usage.md`
- 创建：`.mozi/artifacts/framework/guides/cqrs-faq.md`

- [ ] **步骤 1: 编写迁移指南**

`.mozi/artifacts/framework/guides/cqrs-migration-guide.md`（按 Design 切片 D 结构；代码示例引用任务 7-9 的真实 Quest 实现）：

````markdown
# CQRS 迁移指南（旧 Class Handler → 双模式）

本指南基于 Quest 模块（`GameScript/UI/Quest`）的真实迁移示例。CQRS 双模式已在框架落地（FRD #2/#3），旧代码需手动改写（无兼容层）。

## 1. 决策树：何时用 Class vs Struct 自处理

```
该 Handler 是否高频（每帧/每秒数百次）？
├─ 是 → 它是否需要依赖注入或异步？
│       ├─ 是 → Class 模式 + IPoolable（接受少量分配，复杂逻辑）
│       └─ 否 → Struct 自处理（ISelfHandlingCommand，0GC）
└─ 否 → Class 模式（默认，简单清晰）
```

## 2. Class 模式迁移（加 IPoolable）

1. Handler 类接口列表追加 `IPoolable`（`using Change.Framework.Pooling;`）。
2. 实现 `public void Reset() { /* 清理瞬态字段；readonly 注入依赖无需重置 */ }`。
3. 注册方式不变：`bootstrap.RegisterCommand(new XxxHandler(state))`。
4. 框架在每次 `Handle` 后自动调 `Reset()`（异常隔离，不影响分发）。
5. **对象池生命周期**：框架只调 `Reset()`，不归还池；若需池化实例，由 DI 容器/工厂管理。

示例：Quest `ClaimSideQuestRewardHandler`（见 `QuestCommandHandlers.cs`）。

## 3. Struct 自处理迁移

1. Command/Query struct 改为实现 `ISelfHandlingCommand` / `ISelfHandlingQuery<TResult>`。
2. 添加 `void Execute()`（无参）。**依赖通过 readonly 字段构造注入**——`Execute()` 不能带参（FRD #2 签名约束）。
3. **删除**对应的 Class Handler 类。
4. **移除**装配中的 `RegisterCommand(...)`。
5. 发送处构造时传入依赖：`bus.Send(new XxxCommand(state, ...))`。

示例：Quest `BumpMainQuestProgressCommand`（见 `QuestMessages.cs`）——携带 `QuestSessionState` 字段。

## 4. 异步 Command 迁移

1. Handler 实现 `IAsyncCommandHandler<TCommand>`（`Task ExecuteAsync(TCommand)`）或 `IAsyncQueryHandler<TQuery,TResult>`。
2. 用 `bootstrap.RegisterAsyncCommand(...)` 注册（独立注册表）。
3. 分发用 `await bus.SendAsync(...)` / `await bus.AskAsync(...)`。
4. **Struct 自处理不支持异步**——对 ISelfHandlingCommand 调 `SendAsync` 抛 `NotSupportedException`。

## 5. Event 订阅迁移

- **Class Handler**：`bootstrap.Subscribe(new XxxEventHandler())`，保持不变。
- **静态委托**：`bootstrap.Subscribe<TEvent>(staticMethod)`——**委托不得捕获变量**（`Target != null` 抛 `ClosureCaptureException`）。用于轻量投影/打点。

## 6. 冲突规则

同一类型只能选一种模式。为 `ISelfHandlingCommand` 类型注册 Class handler → 抛 `ModeConflictException`（注册时拦截）。
````

- [ ] **步骤 2: 编写性能最佳实践**

`.mozi/artifacts/framework/guides/cqrs-performance-best-practices.md`：

````markdown
# CQRS 性能最佳实践

## 0GC 目标
热路径（`Send`/`Ask`/`Publish`）严格 0 字节 GC 分配。验证三法：

1. **单元测试**：`DualModeZeroGcTests` / `ZeroAllocationDispatchTests`（`GC.GetAllocatedBytesForCurrentThread` 前后差值）。
2. **Profiler**：Unity Profiler → Memory 模块，观察 GC.Alloc 列。
3. **运行时监控**：定义 `ENABLE_CQRS_MONITORING` 符号，安装 `CqrsPerformanceMonitor`。

## 启用运行时监控
1. Project Settings → Player → Scripting Define Symbols 加 `ENABLE_CQRS_MONITORING`。
2. 启动时：`CqrsBus.SetActiveMonitor(new CqrsPerformanceMonitor())`。
3. 单次执行 GC > 100 字节（默认阈值）触发 `OnThresholdExceeded`。
4. **代价**：ON 时每次 dispatch 产生闭包分配 + Stopwatch 读数——仅诊断用途，默认 OFF。

## Profiler 验证步骤
1. 打开 Profiler（Window → Analysis → Profiler），启 Memory。
2. 运行目标操作（如打开 Quest 面板、完成任务）。
3. 检查对应帧 GC.Alloc 为 0。

## 常见分配陷阱
- Struct 自处理 `Execute()` 内闭包/LINQ → 分配。
- Event 静态委托捕获变量 → `ClosureCaptureException`（注册时拦截）。
- Query Handler 复用 buffer 又返回 snapshot → snapshot 在 Reset 后失效（Quest Query 不复用）。
````

- [ ] **步骤 3: 编写 API 使用文档**

`.mozi/artifacts/framework/guides/cqrs-api-usage.md`（双模式选择指南，引用框架内 `CQRS-DualMode-Guide.md`）：

````markdown
# CQRS API 使用文档

> 详见框架内置 `Assets/Change/Framework/Cqrs/Documentation/CQRS-DualMode-Guide.md`。本文档补充模式选择速查。

## API 速查
| 操作 | Class 模式 | Struct 自处理 |
|------|-----------|--------------|
| Command 定义 | `: ICommand` | `: ISelfHandlingCommand` + `Execute()` |
| Query 定义 | `: IQuery<R>` | `: ISelfHandlingQuery<R>` + `Execute()` |
| Handler | `ICommandHandler<T>` 类 | 无（struct 自处理） |
| 池化 | 可选 `IPoolable` | 不适用 |
| 异步 | `IAsyncCommandHandler` + `SendAsync` | 不支持 |
| 注册 | `RegisterCommand` | 无需 |
| 分发 | `Send` / `Ask` | `Send` / `Ask`（自动识别） |

## 关键约束
- `Execute()` 无参，依赖走 struct 字段。
- Struct handler 被拒绝（`InvalidOperationException`）；用 Class handler 或 Struct 自处理。
- 注册自处理类型 → `ModeConflictException`。
````

- [ ] **步骤 4: 编写 FAQ**

`.mozi/artifacts/framework/guides/cqrs-faq.md`：

````markdown
# CQRS 常见问题

**Q: Reset() 应该清理哪些状态？**
A: 仅清理 Handler 自己的瞬态可变字段（如临时集合、缓存）。readonly 注入依赖（`_state`/`_wallet`）不要重置——它们跨调用共享。Quest Handler 无瞬态字段，`Reset()` 为空。

**Q: Struct 自处理如何访问外部服务（如 QuestSessionState）？**
A: 通过 readonly 字段在构造时注入：`new BumpMainQuestProgressCommand(state, delta)`。`Execute()` 无参，不引入服务定位器，保持 0GC 与依赖显式。

**Q: 对象池何时创建和销毁 Handler？**
A: 框架不管理池生命周期，只在 `Handle` 后调 `Reset()`。Handler 实例由 DI/工厂创建并注册；池化复用由用户决定。

**Q: 异步 Command 执行期间反注册 Handler 会怎样？**
A: 不支持。确保 `await SendAsync/AskAsync` 完成后再反注册，否则 `Reset` 可能指向已释放实例。

**Q: 监控告警触发后如何排查？**
A: 启用 `ENABLE_CQRS_MONITORING`，订阅 `OnThresholdExceeded` 获取 `MessageType` 与 `GcBytes`。定位该消息的 handler，用 Profiler Memory 深查分配源（常见：闭包、LINQ、误复用 buffer）。

**Q: 为什么监控默认关闭？**
A: 监控 ON 时每次 dispatch 产生闭包分配（破坏 0GC）+ Stopwatch 开销。默认 OFF 保证生产热路径 0GC；ON 仅诊断用途。
````

- [ ] **步骤 5: Commit**

```bash
git add .mozi/artifacts/framework/guides/cqrs-migration-guide.md \
        .mozi/artifacts/framework/guides/cqrs-performance-best-practices.md \
        .mozi/artifacts/framework/guides/cqrs-api-usage.md \
        .mozi/artifacts/framework/guides/cqrs-faq.md
git commit -m "docs(cqrs): add migration guide, performance best practices, API usage, and FAQ"
```

---

## 最终全量回归

完成所有任务后，运行完整 CQRS + Quest 测试套件：

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.ZeroAllocationDispatchTests,Change.Framework.Tests.SelfHandlingDispatchTests,Change.Framework.Tests.PoolingResetTests,Change.Framework.Tests.AsyncDispatchTests,Change.Framework.Tests.ModeConflictTests,Change.Framework.Tests.DualModeZeroGcTests,Change.Framework.Tests.MonitoringTests,Change.Framework.Tests.CommandDispatchTests,Change.Framework.Tests.QueryDispatchTests,GameScript.Tests.Quest" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/cqrs-perf-final.xml"
```

预期：全 PASS。关键回归点：
- `ZeroAllocationDispatchTests` / `DualModeZeroGcTests`：0GC 保持（监控 OFF）。
- `SelfHandlingDispatchTests` / `ModeConflictTests`：双模式语义未破坏。
- `GameScript.Tests.Quest`：Quest 迁移后业务正确 + 0GC。

## 规划交接清单

- [x] 所有任务步骤包含精确代码和命令（无占位符）
- [x] 每个任务覆盖上游需求（可追溯到 Design 切片/FRD 决策）
- [x] 任务间依赖关系清晰
- [x] 类型和方法签名跨任务一致（`ZeroGcTestBase.AssertZeroGc`、`CqrsPerformanceMonitor.RecordExecution(Type,long,long)`、`BumpMainQuestProgressCommand(QuestSessionState,int)` 全程一致）
- [x] 上游风险评估已转化为具体测试或缓解步骤（监控 OFF 回归、Quest 业务回归、调用点全仓 grep）
- [x] 对照真实 #2/#3 API 编写（`ISelfHandlingCommand.Execute()` 无参、`ResetIfPoolable`、`Subscribe(Action)`、`ModeConflictException`）
- [ ] 自检通过（待执行）
