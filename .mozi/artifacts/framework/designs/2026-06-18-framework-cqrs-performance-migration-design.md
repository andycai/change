# CQRS 性能验证与迁移 架构设计

> 日期: 2026-06-18 | 状态: 草稿
> FRD: [2026-06-18-framework-cqrs-performance-migration-frd.md](../discover/2026-06-18-framework-cqrs-performance-migration-frd.md)
> 父功能: CQRS 模块重构 - [README.md](../discover/README.md)
> 上游: [2026-05-07-cqrs-architecture-review-and-optimization-design.md](./2026-05-07-cqrs-architecture-review-and-optimization-design.md)

## 上游产出引用

### 来自 FRD（2026-06-18 性能验证与迁移）

| 引用内容 | 如何使用 |
|---------|----------|
| 决策 #1: 严格 0 字节 GC 分配 | 切片 A 的 `AssertZeroGc` 测试基线，切片 B 的监控告警阈值基准 |
| 决策 #2: 单元测试 + Profiler + 运行时监控 | 三层验证架构（切片 A + B + Profiler 手动验证） |
| 决策 #3: 完全不兼容，手动改写 | 切片 C 的 Quest 迁移示例和切片 D 的迁移指南 |
| 决策 #4: 示例优先（Quest）| 切片 C 选择 Quest 模块作为完整迁移示例 |
| 未决问题 #1: 运行时监控数据存储方式 | 切片 B 设计为内存循环缓冲区 + 可选日志，不引入外部系统 |
| 未决问题 #3: 性能基准场景选择 | 切片 C 选择典型 UI 操作（Quest 面板）作为基准场景 |

### 来自 FRD #1（接口层简化与运行时注册）

| 引用内容 | 如何使用 |
|---------|----------|
| 双接口模式：`CqrsBus` 同时实现 `ICqrsRegistry` 和 `ICqrsRuntime` | 切片 B 监控拦截点定位在 `CqrsBus.Send/Ask/Publish` |
| 主线程注册约束 | 切片 B 监控不需要线程安全（单线程访问） |

### 来自 FRD #2（双模式 Command/Query）

| 引用内容 | 如何使用 |
|---------|----------|
| Class 模式：Handler 实现 `IPoolable`，框架自动调用 `Reset()` | 切片 A 测试 Class 路径，切片 C 迁移 Class Handler |
| Struct 模式：Command 实现 `ICommand.Execute()`，栈分配 | 切片 A 测试 Struct 路径，切片 C 迁移简单 Command 到 Struct |
| `Send<TCommand>(in TCommand)` 自动识别 Class/Struct 模式 | 切片 B 监控需要覆盖两种分发路径 |

### 来自 2026-05-07 CQRS 架构设计

| 引用内容 | 如何使用 |
|---------|----------|
| 热路径零分配目标 | 切片 A 性能测试的验收基线 |
| 非捕获静态函数订阅（Event）| 切片 D 迁移指南的 Event 订阅章节 |

## 架构决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 整体验证方案 | 方案 A：分层验证 + 渐进式迁移 | 三层验证（编译时 + 运行时 + 剖析）覆盖全生命周期，Quest 完整迁移示例可直接复制模式，运行时监控是长期资产 |
| GC 测量方式 | `GC.GetAllocatedBytesForCurrentThread()` 前后差值 | 线程局部测量避免其他线程干扰，精度到字节 |
| 运行时监控存储 | 内存循环缓冲区（最多 1000 条）+ 可选 `Debug.LogWarning` | 不引入外部存储系统（FRD 未决问题 #1），内存占用可控，告警走 Unity 日志 |
| 监控隔离方式 | `#if ENABLE_CQRS_MONITORING` 条件编译 | 发布版本零开销，开发版本可随时启用 |
| 监控访问的线程模型 | 单线程（主线程） | 继承 FRD #1 的主线程注册约束，监控无需加锁（避免锁带来 GC） |
| Quest 迁移模式选择 | `BumpMainQuestProgressCommand` → Struct 自处理；所有 Command Handler → Class + `IPoolable`；`GetQuestPanelQueryHandler` → Class 模式（不复用 buffer） | 简单进度 Command 适合栈分配；有依赖注入的领奖 Command 适合 Class；Query 返回值需被调用方持有，buffer 复用会因 `Reset()` 清空而失效 |
| Struct 模式依赖传递 | **FRD #2 已确定 `Execute()` 为无参签名**；依赖通过 struct 的 readonly 字段在构造时注入（如 `BumpMainQuestProgressCommand` 携带 `QuestSessionState` 字段），不引入服务定位器 | FRD #2 的 `ISelfHandlingCommand.Execute()` 实现为无参；构造字段注入保持 0GC 且依赖显式可追踪 |
| Query Handler 的 0GC 策略 | Query 不复用 buffer，改为按调用方持有 snapshot 语义；若需 0GC 由调用方传入预分配 buffer（未来扩展） | snapshot 的 List 引用被调用方持有，复用 buffer 会导致数据失效；Command 无返回值可安全复用 |

## 文件地图

| 文件 | 所属切片 | 职责 |
|------|----------|------|
| `UnityProject/Assets/Change/Framework/Cqrs/Tests/Performance/ZeroGcTestBase.cs` | 切片 A | 0GC 测试基类，提供 `AssertZeroGc` / `MeasureGcBytes` 工具方法 |
| `UnityProject/Assets/Change/Framework/Cqrs/Tests/Performance/ClassModeZeroGcTests.cs` | 切片 A | Class 模式（池化 Handler）热路径 0GC 测试 |
| `UnityProject/Assets/Change/Framework/Cqrs/Tests/Performance/StructModeZeroGcTests.cs` | 切片 A | Struct 模式（自处理 Execute）热路径 0GC 测试 |
| `UnityProject/Assets/Change/Framework/Cqrs/Monitoring/CqrsPerformanceMonitor.cs` | 切片 B | 监控核心，拦截 `Send/Ask/Publish` 收集指标并触发告警 |
| `UnityProject/Assets/Change/Framework/Cqrs/Monitoring/PerformanceMetrics.cs` | 切片 B | 指标数据结构（按消息类型聚合） |
| `UnityProject/Assets/Change/Framework/Cqrs/Monitoring/IPerformanceThresholdPolicy.cs` | 切片 B | 阈值策略接口，可替换告警规则 |
| `UnityProject/Assets/Change/Framework/Cqrs/Monitoring/DefaultThresholdPolicy.cs` | 切片 B | 默认策略：单次 GC > 100 字节触发告警 |
| `UnityProject/Assets/Change/Framework/Cqrs/Tests/Performance/MonitoringOverheadTests.cs` | 切片 B | 监控自身 0GC 验证测试 |
| `UnityProject/Assets/GameScript/UI/Quest/QuestCommandHandlers.cs` | 切片 C | Quest Command Handler 迁移（Class + `IPoolable`） |
| `UnityProject/Assets/GameScript/UI/Quest/QuestMessages.cs` | 切片 C | `BumpMainQuestProgressCommand` 迁移为 Struct 自处理 |
| `UnityProject/Assets/GameScript/UI/Quest/QuestQueryHandlers.cs` | 切片 C | Quest Query Handler 迁移（Class + `IPoolable`，不复用 buffer） |
| `UnityProject/Assets/GameScript/UI/Quest/Tests/QuestMigrationTests.cs` | 切片 C | 迁移后业务逻辑回归测试 |
| `UnityProject/Assets/GameScript/UI/Quest/Tests/QuestPerformanceTests.cs` | 切片 C | Quest 模块 0GC 性能测试 |
| `.mozi/artifacts/framework/guides/cqrs-migration-guide.md` | 切片 D | 迁移指南（决策树 + 步骤清单） |
| `.mozi/artifacts/framework/guides/cqrs-performance-best-practices.md` | 切片 D | 性能最佳实践 |
| `.mozi/artifacts/framework/guides/cqrs-api-usage.md` | 切片 D | API 使用文档（双模式选择） |
| `.mozi/artifacts/framework/guides/cqrs-faq.md` | 切片 D | 常见问题 |

## 切片分解

### 切片 A: 0GC 单元测试框架

**依赖：** 无
**风险等级：** 低
**涉及文件：** `ZeroGcTestBase.cs`, `ClassModeZeroGcTests.cs`, `StructModeZeroGcTests.cs`

**内容：** 建立 0GC 测试基础设施，提供测量线程局部 GC 分配的工具方法，并编写覆盖 Class 模式（池化 Handler）和 Struct 模式（自处理 `Execute`）两条热路径的基准测试。该框架是后续切片验证性能的依赖基础。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `ZeroGcTestBase.AssertZeroGc` | `protected void AssertZeroGc(Action action)` | 执行 action 一次预热 + 多次测量，断言线程 GC 分配增量为 0，否则抛 `AssertionException` |
| `ZeroGcTestBase.MeasureGcBytes` | `protected long MeasureGcBytes(Action action)` | 返回单次执行 action 的线程 GC 字节增量（不含断言，供调试） |

**数据契约：**

```csharp
// 切片 A 内部使用的测试桩（Class 路径）
public class TestClassCommand : ICommand { }
public sealed class TestClassCommandHandler
    : ICommandHandler<TestClassCommand>, IPoolable
{
    public void Handle(in TestClassCommand cmd) { /* no-op */ }
    public void Reset() { }
}

// 切片 A 内部使用的测试桩（Struct 路径）
public readonly struct TestStructCommand : ICommand
{
    public int Value { get; }
    public void Execute() { /* no-op */ }
}
```

**验收标准：**

- [ ] `AssertZeroGc` 对无分配操作通过，对故意分配的操作失败（负向测试验证测量有效性）
- [ ] Class 模式测试覆盖：同步 Command、同步 Query、异步 Command（`ExecuteAsync`）
- [ ] Struct 模式测试覆盖：同步 Command、同步 Query
- [ ] 测试套件在 Unity Test Runner（EditMode）中全部通过

**回归风险评估：**
- 影响范围：仅新增测试文件，不修改现有代码
- 缓解措施：无需缓解

### 切片 B: 运行时性能监控系统

**依赖：** 切片 A（用 `ZeroGcTestBase` 验证监控自身零开销）
**风险等级：** 高（监控本身不能成为性能瓶颈或引入 GC）
**涉及文件：** `CqrsPerformanceMonitor.cs`, `PerformanceMetrics.cs`, `IPerformanceThresholdPolicy.cs`, `DefaultThresholdPolicy.cs`, `MonitoringOverheadTests.cs`

**内容：** 在 `CqrsBus.Send/Ask/Publish` 热路径插入监控拦截点，按消息类型聚合执行次数、耗时、GC 分配，超过阈值时触发告警事件。监控代码用条件编译隔离，确保发布版本零开销；监控逻辑自身必须 0GC（用切片 A 框架验证）。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `CqrsPerformanceMonitor.RecordExecution` | `void RecordExecution(string messageType, long durationTicks, long gcBytes)` | 按 `messageType` 累加指标到内存缓冲区，超阈值则触发 `OnThresholdExceeded` |
| `CqrsPerformanceMonitor.GetMetrics` | `IReadOnlyList<PerformanceMetrics> GetMetrics()` | 返回当前所有聚合指标快照（只读视图） |
| `CqrsPerformanceMonitor.ClearMetrics` | `void ClearMetrics()` | 清空内存缓冲区 |
| `CqrsPerformanceMonitor.OnThresholdExceeded` | `event Action<PerformanceAlert>` | 阈值告警事件，供外部订阅（如写入日志） |
| `IPerformanceThresholdPolicy.ShouldAlert` | `bool ShouldAlert(in PerformanceMetrics metrics)` | 判定是否触发告警，可替换实现 |
| `DefaultThresholdPolicy.ShouldAlert` | `bool ShouldAlert(in PerformanceMetrics metrics)` | 默认实现：`MaxGcBytes > 100` 时返回 true |

**数据契约：**

```csharp
// 切片 B 对外暴露的数据结构（readonly struct 保证 0GC 传递）
public readonly struct PerformanceMetrics
{
    public string MessageType { get; }   // Command/Query/Event 的类型名
    public int ExecutionCount { get; }
    public long TotalDurationTicks { get; }
    public long TotalGcBytes { get; }
    public long MaxGcBytes { get; }      // 单次最大 GC 分配（阈值判断依据）
}

public readonly struct PerformanceAlert
{
    public string MessageType { get; }
    public long GcBytes { get; }
    public long Threshold { get; }
    public DateTime Timestamp { get; }
}
```

**行为契约：**

- 输入：`RecordExecution(messageType, durationTicks, gcBytes)`，`messageType` 为消息类型全名
- 输出：更新内存循环缓冲区（按 `messageType` 聚合），缓冲区上限 1000 个类型条目，超出按 LRU 淘汰
- 副作用：超阈值时触发 `OnThresholdExceeded`；默认策略下同步调用 `Debug.LogWarning`（可通过订阅覆盖）
- 集成点：`CqrsBus.Send/Ask/Publish` 内部，`#if ENABLE_CQRS_MONITORING` 包裹，未定义时整段代码不编译
- 线程模型：单线程访问（主线程），不加锁

**验收标准：**

- [ ] 监控正确收集执行次数、总耗时、GC 分配、单次最大 GC
- [ ] 阈值告警在 `MaxGcBytes > 100` 时触发 `OnThresholdExceeded` 事件
- [ ] `MonitoringOverheadTests` 验证 `RecordExecution` 本身 0GC（用切片 A 的 `AssertZeroGc`）
- [ ] `ENABLE_CQRS_MONITORING` 未定义时，`CqrsBus` 编译产物中无监控代码（IL 无监控调用）
- [ ] 现有 `CqrsBus` 分发测试全部通过（不破坏分发语义）

**回归风险评估：**
- 影响范围：修改 `CqrsBus` 核心执行路径，所有 CQRS 调用方间接受影响
- 缓解措施：
  1. 条件编译隔离，默认不启用（发布版本零开销）
  2. 监控逻辑自身通过 0GC 测试
  3. 保留现有分发测试作为回归基线
  4. 监控失败（如告警订阅抛异常）不影响正常分发（try-catch 隔离）

### 切片 C: Quest 模块迁移示例

**依赖：** 切片 A（性能测试框架）、切片 B（可选，迁移后启用监控验证）
**风险等级：** 中（迁移逻辑可能破坏 Quest 业务功能）
**涉及文件：** `QuestCommandHandlers.cs`, `QuestMessages.cs`, `QuestQueryHandlers.cs`, `Tests/QuestMigrationTests.cs`, `Tests/QuestPerformanceTests.cs`

**内容：** 将 Quest 模块从旧模式迁移到 FRD #2 的双模式：`BumpMainQuestProgressCommand` 改为 Struct 自处理（栈分配，无 Handler 类），其余有依赖注入的 Command Handler 改为 Class + `IPoolable`，`GetQuestPanelQueryHandler` 改为 Class 模式但不复用 buffer（因 snapshot 被调用方持有）。迁移后用业务回归测试和 0GC 性能测试双重验证。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `BumpMainQuestProgressCommand.Execute` | `void Execute()`（无参）| Struct 自处理：直接调用携带的 `QuestSessionState` 字段的 `BumpMainProgress(Delta)`。依赖通过 readonly 字段在构造时注入 |
| `ClaimSideQuestRewardHandler` | `: ICommandHandler<...>, IPoolable` | Class 模式不变，新增 `Reset()`（当前无字段需重置，空实现） |
| `GetQuestPanelQueryHandler` | `: IQueryHandler<...>, IPoolable` | Class 模式，保留每次 new List 的现有实现（不复用 buffer）；`Reset()` 为空（Handler 无状态字段需重置） |
| `GetQuestPanelQueryHandler.Handle` | `QuestPanelSnapshot Handle(in GetQuestPanelQuery query)` | 维持现有逻辑：构造 List 填充并返回 snapshot。snapshot 被 Presenter 持有，复用 buffer 会因下次 `Reset()` 清空而失效，故 Query 路径暂不追求 0GC |

**数据契约：**

```csharp
// Struct 自处理迁移（QuestMessages.cs）
// Execute() 为无参签名（FRD #2 ISelfHandlingCommand）；QuestSessionState 作为
// readonly 字段在构造时注入，保持 0GC 且依赖显式可追踪。
public readonly struct BumpMainQuestProgressCommand : ISelfHandlingCommand
{
    private readonly QuestSessionState _state;
    public int Delta { get; }

    public BumpMainQuestProgressCommand(QuestSessionState state, int delta)
    {
        _state = state;
        Delta = delta;
    }

    public void Execute() => _state.BumpMainProgress(Delta);
}

// Class + IPoolable 迁移（QuestQueryHandlers.cs）
// 注意：Query 路径不复用 buffer，因为返回的 snapshot 被 Presenter 持有，
// 复用 buffer 会因下次 Reset() 清空导致 snapshot 数据失效。
// Handler 本身无状态字段需重置，Reset() 为空实现。
public sealed class GetQuestPanelQueryHandler
    : IQueryHandler<GetQuestPanelQuery, QuestPanelSnapshot>, IPoolable
{
    private readonly QuestSessionState _state;
    private readonly QuestRewardWallet _wallet;

    public QuestPanelSnapshot Handle(in GetQuestPanelQuery query)
    {
        // 维持现有逻辑：每次构造新的 List（Query 路径暂不追求 0GC）
        var sides = new List<SideQuestVm>(_state.Sides.Count);
        foreach (var row in _state.Sides)
            sides.Add(new SideQuestVm(...));
        // ... 同理构造 dailies
        return new QuestPanelSnapshot { Sides = sides, /* ... */ };
    }

    public void Reset() { /* 无状态字段需重置 */ }
}
```

**行为契约：**

- Struct 模式：`CqrsBus.Send(in BumpMainQuestProgressCommand)` 检测到该 Command 实现 `ISelfHandlingCommand`（每类型缓存的 constrained.callvirt 委托，FRD #2 已实现），直接调用无参 `Execute()`，无 Handler 实例化、无 GC。`QuestSessionState` 由 struct 的 readonly 字段携带（构造注入）。
- Class 模式：`CqrsBus` 从对象池获取 Handler 实例，调用 `Handle`，执行后调用 `Reset()`；Command Handler 的 `Reset()` 清理状态（Quest 的 Command Handler 无字段需重置，空实现）；Query Handler 因返回值被持有不复用 buffer。Handler 实例归还由 DI 容器或用户管理（FRD #2 决策 #5）
- 业务语义不变：任务进度更新、奖励发放、异常抛出（重复领取）行为与迁移前一致

**验收标准：**

- [ ] `QuestMigrationTests` 覆盖：主任务进度、支线进度+领奖、日常进度+领奖、重复领取异常
- [ ] `QuestPerformanceTests.BumpProgress_AllocatesZeroBytes` 通过（Struct 路径，切片 A 框架）
- [ ] `QuestPerformanceTests.ClaimReward_AllocatesZeroBytes` 通过（Class 池化路径）
- [ ] Unity Profiler 手动验证：完成任务（BumpProgress），Profiler 的 GC.Alloc 列为 0
- [ ] Unity Profiler 手动验证：领取奖励（ClaimReward，Class 池化），Profiler 的 GC.Alloc 列为 0
- [ ] Query 路径（OpenPanel）允许 GC 分配（snapshot 持有语义决定），不纳入 0GC 验收
- [ ] 调用方适配：`OpenQuestPanelUseCase`（注入 `ICqrsBus`，调用 `Ask`/`Send`）适配 FRD #2 的新 API 签名；`BumpMainQuestProgressCommand` 迁移为 `ISelfHandlingCommand` 后移除对应 Class Handler 注册，并在构造时传入 `QuestSessionState` 依赖
- [ ] `QuestWindowPresenter` 经验证无需改动（它通过 `IOpenQuestPanelUseCase` 间接访问 CQRS，不直接持有 `ICqrsBus`）

**回归风险评估：**
- 影响范围：Quest 模块所有 Handler 接口签名变更；`OpenQuestPanelUseCase`（注入 `ICqrsBus`，调用 `Ask`/`Send`）需适配 FRD #2 的新 API；`BumpMainQuestProgressCommand` 迁移为 `ISelfHandlingCommand` 后须在构造时传入 `QuestSessionState`（无参 `Execute()`，依赖走 struct 字段）。`QuestWindowPresenter` 通过 `IOpenQuestPanelUseCase` 间接访问 CQRS，预期无需改动
- 缓解措施：
  1. 迁移前确认现有 Quest 功能测试（如有）作为基线
  2. 迁移后 `QuestMigrationTests` 覆盖原有业务分支
  3. 手动测试 Quest 面板完整流程（打开→进度→领奖→重复领奖报错）
  4. 单独验证 `OpenQuestPanelUseCase → QuestWindowPresenter` 调用链未断

### 切片 D: 迁移指南与文档

**依赖：** 切片 C（参考实际 Quest 迁移示例编写）
**风险等级：** 低
**涉及文件：** `cqrs-migration-guide.md`, `cqrs-performance-best-practices.md`, `cqrs-api-usage.md`, `cqrs-faq.md`

**内容：** 基于 Quest 模块的实际迁移经验，编写覆盖所有场景的迁移指南、性能最佳实践、API 使用文档和 FAQ，供业务团队自行迁移其他模块。

**数据契约：** 文档结构约定（无代码契约）

```text
cqrs-migration-guide.md
├── 1. 决策树：何时用 Class vs Struct
├── 2. Class 模式迁移步骤（IPoolable / Reset / 注册 / 对象池配置）
├── 3. Struct 模式迁移步骤（移除 Handler / 实现 Execute / 依赖传递 / 移除注册）
├── 4. 异步 Command 迁移（IAsyncCommandHandler）
├── 5. Event 订阅迁移（Class Handler / 非捕获静态委托）
└── 6. Quest 模块迁移示例参考（链接切片 C 的实际代码）

cqrs-faq.md
├── Reset() 应该清理哪些状态？
├── Struct 模式如何访问外部服务？
├── 对象池何时创建和销毁？
├── 异步 Command 执行期间反注册 Handler 会怎样？
└── 监控告警触发后如何排查？
```

**验收标准：**

- [ ] 迁移指南覆盖 FRD 列出的所有场景：Class Handler、Struct 自处理、异步 Command、Event 订阅
- [ ] 每个迁移步骤有代码示例（直接引用 Quest 模块的切片 C 实现）
- [ ] FAQ 覆盖上述 5 个常见问题
- [ ] 性能最佳实践包含：0GC 检查方法、监控启用方式、Profiler 验证步骤
- [ ] 文档 review 通过（用户确认）

**回归风险评估：**
- 影响范围：无（纯文档）
- 缓解措施：无需缓解

## 切片依赖图

```text
切片 A (0GC 测试框架)
  ├── 切片 B (运行时监控) ← 高风险，优先验证监控自身零开销
  └── 切片 C (Quest 迁移) ← 完整用户旅程
         └── 切片 D (迁移指南) ← 参考实际迁移示例
```

**实施顺序：** A → (B ∥ C) → D

- 切片 A 先行，为 B 和 C 提供性能验证基础
- 切片 B 和 C 可并行（B 修改框架核心，C 修改业务模块，无文件冲突）
- 切片 D 最后，基于 C 的实际迁移经验编写指南

## 关键接口

> 切片间和对外暴露的接口签名汇总。

```csharp
// 切片 A 暴露给切片 B、C（性能验证工具）
public abstract class ZeroGcTestBase
{
    protected void AssertZeroGc(Action action);
    protected long MeasureGcBytes(Action action);
}

// 切片 B 暴露给外部系统（监控查询）
public class CqrsPerformanceMonitor
{
    void RecordExecution(string messageType, long durationTicks, long gcBytes);
    IReadOnlyList<PerformanceMetrics> GetMetrics();
    void ClearMetrics();
    event Action<PerformanceAlert> OnThresholdExceeded;
}

public interface IPerformanceThresholdPolicy
{
    bool ShouldAlert(in PerformanceMetrics metrics);
}

// 切片 C 暴露给框架（Struct 自处理 + 池化 Handler）
// ISelfHandlingCommand.Execute() 为无参签名（FRD #2）；依赖通过 struct 字段注入
public readonly struct BumpMainQuestProgressCommand : ISelfHandlingCommand
{
    void Execute();
}

// Query Handler 池化但不复用 buffer（snapshot 被持有语义决定）
public sealed class GetQuestPanelQueryHandler
    : IQueryHandler<GetQuestPanelQuery, QuestPanelSnapshot>, IPoolable
{
    QuestPanelSnapshot Handle(in GetQuestPanelQuery query);
    void Reset();  // 空实现，无状态字段需重置
}
```

## 回归风险评估

> 整体评估设计对现有功能的影响。

| 影响范围 | 风险等级 | 缓解措施 |
|---------|---------|----------|
| `CqrsBus` 核心分发路径（切片 B 插入监控） | 高 | 条件编译隔离 + 监控逻辑自身 0GC 测试 + 现有分发测试作为回归基线 + try-catch 隔离监控异常 |
| Quest 模块业务功能（切片 C 接口变更） | 中 | 业务回归测试 + `OpenQuestPanelUseCase` 适配新 API + 手动测试完整 Quest 流程 |
| `OpenQuestPanelUseCase → QuestWindowPresenter` 调用链（切片 C 连锁） | 中 | 切片 C 同步处理 UseCase 适配，验证调用链未断；Presenter 间接访问预期无需改动 |
| 框架服务定位器暴露 `QuestSessionState`（Struct 依赖注入） | 低 | 由框架统一管理，文档说明服务注册方式 |
| 现有 CQRS 测试套件 | 低 | 切片 A/B/C 全部新增文件，不修改现有测试；切片 B 的监控默认不启用 |

## 未决问题的设计阶段答复

> 回应 FRD 中标记为 "建议解决阶段: design" 的未决问题。

| FRD 未决问题 | 设计答复 |
|------------|----------|
| #1 运行时监控数据的存储和查询方式 | 内存循环缓冲区（LRU，上限 1000 条）+ `GetMetrics()` 只读快照查询；告警走 `Debug.LogWarning`，不引入外部存储 |
| #3 性能基准测试的场景选择 | 选择典型 UI 操作（Quest 面板打开 + 任务进度更新）作为基准场景，覆盖 Query 和 Command 两条路径 |

## 规划交接清单

- [ ] 设计文档已完成并经用户确认
- [x] 所有切片定义了接口契约（签名 + 数据 + 行为）
- [x] 每个切片的验收标准可在 implement 阶段独立验证
- [x] 回归风险评估已完成
- [x] 文件地图已明确（17 个文件，含 .meta 由 Unity 自动生成）
- [x] 切片依赖关系已清晰（A → B∥C → D）
