# CQRS 双模式 Command/Query 架构设计

> 日期: 2026-06-18 | 状态: 草稿
> FRD: [2026-06-18-framework-cqrs-dual-mode-command-query-frd.md](../discover/2026-06-18-framework-cqrs-dual-mode-command-query-frd.md)

## 上游产出引用

### 来自 FRD

| 引用内容 | 如何使用 |
|---------|----------|
| 决策 #1: Class（DI + 对象池）+ Struct（自处理）双模式并存 | 整体架构基础，Slice A 实现 Struct 模式，Slice B 实现 Class 模式池化 |
| 决策 #2: 使用 `Change.Framework.Pooling`，框架自动 `Reset()` | Slice B 在 Handle() 后自动调用 Reset() |
| 决策 #3: 异步支持仅 Class 模式 | Slice C 仅为 Class Handler 提供 ExecuteAsync |
| 决策 #4: 编译时接口约束 + 运行时检测 | Slice A 通过反射检测 Execute() 方法，Slice D 实现冲突检测 |
| 决策 #5: 用户负责创建和注册 Handler 实例，框架负责 Reset() | Slice B 设计：框架只调用 Reset()，不管理池生命周期 |
| 验收条件: 严格 0 字节 GC 分配（热路径执行） | Slice A 实现 Struct 栈分配路径，Slice D 性能验证 |
| 未决问题 #1: Struct 模式访问外部服务的方式 | Slice D 文档提供推荐方案：服务定位器或 struct 字段传递 |
| 未决问题 #3: Class 和 Struct 模式能否混用 | Slice D 明确：编译时互斥，运行时冲突检测 |

## 架构决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 模式识别机制 | 接口 + 反射检测 | Struct 实现 Execute() 方法即为自处理模式；Class 注册 Handler 即为 DI 模式；运行时反射检测开销可通过缓存优化至 < 5ns |
| Reset() 调用时机 | Handle() 后 finally 块 | 确保即使 Handle() 抛异常也能清理状态；用户负责将 Handler 返回池 |
| 异步支持范围 | 仅 Class 模式 | Struct 自处理场景（高频战斗）不需要异步；异步通常伴随 I/O 和复杂逻辑，适合 Class 模式 |
| 模式冲突策略 | 编译时互斥 + 运行时检测 | 同一 Command 类型不能同时走两种路径；注册时和分发时双重检测，开发阶段暴露问题 |
| 反射性能优化 | 静态字典缓存 | 每个类型仅反射一次，后续查表；JIT 优化接口检查至近乎零开销 |

## 文件地图

| 文件 | 所属切片 | 职责 |
|------|----------|------|
| `Abstractions/ICommand.cs` | Slice A | 扩展接口，支持可选的 Execute() 方法（C# default interface method 或文档约定） |
| `Abstractions/IQuery.cs` | Slice A | 扩展接口，支持可选的 Execute() 方法返回 TResult |
| `Core/CqrsBus.cs` | Slice A, B, C | 核心分发逻辑：模式检测、Handler 调用、Reset() 集成、异步支持 |
| `Tests/EditMode/Cqrs/StructSelfHandlingTests.cs` | Slice A | 测试 Struct 自处理分发路径、0GC 验证 |
| `Abstractions/ICommandHandler.cs` | Slice B | 文档说明可选实现 IPoolable |
| `Abstractions/IQueryHandler.cs` | Slice B | 文档说明可选实现 IPoolable |
| `Tests/EditMode/Cqrs/PoolingIntegrationTests.cs` | Slice B | 测试 Reset() 自动调用、异常处理 |
| `Abstractions/IAsyncCommandHandler.cs` | Slice C | 新接口：Task ExecuteAsync(TCommand) |
| `Abstractions/IAsyncQueryHandler.cs` | Slice C | 新接口：Task<TResult> ExecuteAsync(TQuery) |
| `Abstractions/ICqrsBus.cs` | Slice C | 扩展：Task SendAsync(), Task<TResult> AskAsync() |
| `Tests/EditMode/Cqrs/AsyncHandlerTests.cs` | Slice C | 测试异步 Handler 执行、Reset() 时机 |
| `Exceptions/ModeConflictException.cs` | Slice D | 新异常：模式冲突时抛出 |
| `Tests/EditMode/Cqrs/ModeConflictTests.cs` | Slice D | 测试冲突检测逻辑 |
| `Documentation/CQRS-DualMode-Guide.md` | Slice D | 开发者使用指南、场景选择、性能对比 |

## 切片分解

### Slice A: Struct Self-Handling Mode (Foundation)

**依赖：** 无  
**风险等级：** 中  
**涉及文件：** `Abstractions/ICommand.cs`, `Abstractions/IQuery.cs`, `Core/CqrsBus.cs`, `Tests/EditMode/Cqrs/StructSelfHandlingTests.cs`

**内容：** 实现 Struct 自处理模式的核心机制。为 `ICommand` 和 `IQuery<TResult>` 接口添加可选的 `Execute()` 方法。修改 `CqrsBus.Send/Ask` 以检测 Struct 是否实现了自处理方法，如果有则直接调用，否则走现有的 Handler 查找路径。这是 0GC 路径的基础。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `ICommand` | 添加可选 `void Execute()` 方法 | Struct 实现此方法时启用自处理模式 |
| `IQuery<TResult>` | 添加可选 `TResult Execute()` 方法 | Struct 实现此方法时直接返回结果 |
| `CqrsBus.Send<TCommand>()` | 扩展现有方法 | 运行时检测 Execute() 存在性，有则调用，否则查找 Handler |
| `CqrsBus.Ask<TQuery, TResult>()` | 扩展现有方法 | 运行时检测 Execute() 存在性，有则调用，否则查找 Handler |

**数据契约：**

```csharp
// Struct 模式 Command 示例
public readonly struct ApplyDamageCommand : ICommand
{
    public readonly int TargetId;
    public readonly int Amount;
    
    public void Execute()
    {
        // 内联逻辑，无 DI，栈分配
        CombatSystem.ApplyDamage(TargetId, Amount);
    }
}

// Struct 模式 Query 示例
public readonly struct GetEntityHealthQuery : IQuery<int>
{
    public readonly int EntityId;
    
    public int Execute()
    {
        return CombatSystem.GetHealth(EntityId);
    }
}
```

**行为契约：**

```csharp
// 分发逻辑伪代码
public void Send<TCommand>(in TCommand command) where TCommand : struct, ICommand
{
    // 运行时检测：TCommand 是否有 Execute() 方法？
    if (HasExecuteMethod<TCommand>())  // 缓存反射结果
    {
        // Struct 自处理路径
        command.Execute();  // 直接调用，0 boxing，0 GC
        return;
    }
    
    // 现有 Class Handler 路径
    var handler = LookupHandler<TCommand>();
    handler.Handle(in command);
}
```

**验收标准：**
- [ ] Struct 实现 `Execute()` 可无需 Handler 注册直接分发
- [ ] 调用路径零堆分配（通过 Unity Profiler 验证）
- [ ] Struct 不实现 `Execute()` 时回退到 Handler 查找
- [ ] 混合模式（同类型同时有 Handler 和 Execute()）抛 `ModeConflictException`

**回归风险评估：**
- **影响范围：** `CqrsBus.Send/Ask` 增加分支逻辑（每次分发需检测 Execute() 方法）
- **缓解措施：** 使用静态字典缓存反射结果，每个类型仅反射一次；性能影响 < 5ns；现有 Handler 模式完全不变

---

### Slice B: Class Handler Object Pooling Integration

**依赖：** 无（与 Slice A 并行）  
**风险等级：** 高  
**涉及文件：** `Abstractions/ICommandHandler.cs`, `Abstractions/IQueryHandler.cs`, `Core/CqrsBus.cs`, `Tests/EditMode/Cqrs/PoolingIntegrationTests.cs`

**内容：** 为 Class 模式的 Handler 集成对象池。Handler 类可选实现 `IPoolable` 接口。`CqrsBus` 在 `Handle()` 执行后自动检测 Handler 是否实现 `IPoolable`，如果是则调用 `Reset()`。用户负责从池中获取 Handler 实例并注册，框架仅负责执行后清理。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `ICommandHandler<TCommand>` | 可选实现 `IPoolable` | 标记 Handler 需要 Reset() 回调 |
| `IQueryHandler<TQuery, TResult>` | 可选实现 `IPoolable` | 标记 Handler 需要 Reset() 回调 |
| `CqrsBus.Send<TCommand>()` | 添加 finally 块调用 Reset() | Handle() 后检测 IPoolable 并调用 Reset() |
| `CqrsBus.Query<TQuery, TResult>()` | 添加 finally 块调用 Reset() | Handle() 后检测 IPoolable 并调用 Reset() |

**数据契约：**

```csharp
// 带池化支持的 Handler
public sealed class CompleteQuestHandler : ICommandHandler<CompleteQuestCommand>, IPoolable
{
    private readonly QuestSessionState _state;
    private readonly QuestRewardWallet _wallet;
    
    // DI 构造注入
    public CompleteQuestHandler(QuestSessionState state, QuestRewardWallet wallet)
    {
        _state = state;
        _wallet = wallet;
    }
    
    public void Handle(in CompleteQuestCommand command)
    {
        // 复杂业务逻辑，使用注入的服务
        var quest = _state.GetQuest(command.QuestId);
        quest.MarkComplete();
        _wallet.AddGold(quest.Reward);
    }
    
    public void Reset()
    {
        // 清理瞬态状态，准备返回池
        // 注意：框架调用此方法，用户手动返回池
    }
}
```

**行为契约：**

```csharp
// Reset 调用逻辑
public void Send<TCommand>(in TCommand command) where TCommand : struct, ICommand
{
    var handler = LookupHandler<TCommand>();
    try
    {
        handler.Handle(in command);
    }
    finally
    {
        if (handler is IPoolable poolable)
        {
            try
            {
                poolable.Reset();
            }
            catch (Exception ex)
            {
                _logger.Error($"Reset() failed for {typeof(TCommand).Name}: {ex.Message}");
                // 吞掉异常，不让命令执行失败
            }
        }
    }
}
```

**池生命周期：**
1. 用户创建 Handler（通过 DI 容器或工厂）
2. 用户注册 Handler：`bus.RegisterCommand<T>(handler)`
3. 命令分发 → `Handle()` 调用 → `Reset()` 调用（如果实现 IPoolable）
4. 用户手动反注册并返回 Handler 到池

**验收标准：**
- [ ] Handler 实现 IPoolable 时，每次 Handle() 后自动调用 Reset()
- [ ] Handler 不实现 IPoolable 时，正常工作无 Reset() 调用
- [ ] Reset() 抛异常时记录日志但不影响命令执行
- [ ] 多次 Send 同一 Command，每次都触发 Reset()

**回归风险评估：**
- **影响范围：** 每次 Handler 执行都需检测 IPoolable 接口
- **缓解措施：** 接口检测是 JIT 优化的，开销 < 2ns；现有不实现 IPoolable 的 Handler 完全不受影响

---

### Slice C: Async Command/Query Support (Class Mode Only)

**依赖：** Slice B（需要先有池化集成以确保异步场景下的 Reset 调用）  
**风险等级：** 中  
**涉及文件：** `Abstractions/IAsyncCommandHandler.cs`, `Abstractions/IAsyncQueryHandler.cs`, `Abstractions/ICqrsBus.cs`, `Core/CqrsBus.cs`, `Tests/EditMode/Cqrs/AsyncHandlerTests.cs`

**内容：** 为 Class 模式添加异步 Handler 支持。新增 `IAsyncCommandHandler<TCommand>` 和 `IAsyncQueryHandler<TQuery, TResult>` 接口，提供 `Task ExecuteAsync()` 和 `Task<TResult> ExecuteAsync()` 方法。`CqrsBus` 添加 `SendAsync/AskAsync` 方法，异步等待 Handler 执行完成后再调用 `Reset()`。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `IAsyncCommandHandler<TCommand>` | `Task ExecuteAsync(TCommand command)` | 异步命令处理，仅 Class 模式 |
| `IAsyncQueryHandler<TQuery, TResult>` | `Task<TResult> ExecuteAsync(TQuery query)` | 异步查询处理，仅 Class 模式 |
| `ICqrsBus.SendAsync<TCommand>()` | `Task SendAsync(TCommand command)` | 分发异步命令 |
| `ICqrsBus.AskAsync<TQuery, TResult>()` | `Task<TResult> AskAsync(TQuery query)` | 分发异步查询 |

**数据契约：**

```csharp
// 异步 Command Handler
public sealed class SaveGameStateHandler : IAsyncCommandHandler<SaveGameStateCommand>, IPoolable
{
    private readonly ICloudStorage _storage;
    
    public SaveGameStateHandler(ICloudStorage storage) => _storage = storage;
    
    public async Task ExecuteAsync(SaveGameStateCommand command)
    {
        var data = SerializeState(command.State);
        await _storage.UploadAsync(command.PlayerId, data);
    }
    
    public void Reset()
    {
        // 清理瞬态状态
    }
}

// 异步 Query Handler
public sealed class LoadGameStateHandler : IAsyncQueryHandler<LoadGameStateQuery, GameState>
{
    private readonly ICloudStorage _storage;
    
    public LoadGameStateHandler(ICloudStorage storage) => _storage = storage;
    
    public async Task<GameState> ExecuteAsync(LoadGameStateQuery query)
    {
        var data = await _storage.DownloadAsync(query.PlayerId);
        return DeserializeState(data);
    }
}
```

**行为契约：**

```csharp
// 异步分发逻辑
public async Task SendAsync<TCommand>(TCommand command) where TCommand : struct, ICommand
{
    // Struct 模式检测 - 抛异常
    if (HasExecuteMethod<TCommand>())
    {
        throw new NotSupportedException(
            "Struct self-handling mode does not support async. Use Send() instead.");
    }
    
    var handler = LookupAsyncHandler<TCommand>();
    try
    {
        await handler.ExecuteAsync(command);
    }
    finally
    {
        if (handler is IPoolable poolable)
        {
            poolable.Reset();
        }
    }
}
```

**注册分离：**
- `RegisterAsyncCommand<TCommand>(IAsyncCommandHandler<TCommand>)` - 独立注册表
- `RegisterAsyncQuery<TQuery, TResult>(IAsyncQueryHandler<TQuery, TResult>)` - 独立注册表
- 同步和异步 Handler 可为同一 Command 类型共存（不同 API）

**验收标准：**
- [ ] 异步 Command Handler 正常执行并完成
- [ ] 异步 Query Handler 返回正确结果
- [ ] 实现 IPoolable 的异步 Handler 在 await 完成后调用 Reset()
- [ ] 对 Struct 自处理模式调用 SendAsync() 抛 NotSupportedException

**回归风险评估：**
- **影响范围：** 无 - 新增 API 表面，不改变现有同步路径
- **缓解措施：** 异步和同步 Handler 分离注册，互不干扰

---

### Slice D: Mode Conflict Detection & Documentation

**依赖：** Slice A, B, C（需要所有模式实现完成后才能测试冲突检测）  
**风险等级：** 低  
**涉及文件：** `Core/CqrsBus.cs`, `Exceptions/ModeConflictException.cs`, `Tests/EditMode/Cqrs/ModeConflictTests.cs`, `Documentation/CQRS-DualMode-Guide.md`

**内容：** 实现模式冲突检测和使用文档。当 Command/Query 既注册了 Class Handler 又尝试走 Struct 自处理路径时，抛出 `ModeConflictException`。编写开发者指南，说明何时使用 Class 模式（复杂业务逻辑、需要 DI、异步操作），何时使用 Struct 模式（高频战斗逻辑、0GC 要求）。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `ModeConflictException` | `new Exception(string message)` | Class/Struct 模式冲突时抛出 |
| `CqrsBus.RegisterCommand<T>()` | 添加冲突检测 | 如果 T 实现 Execute() 则抛异常 |
| `CqrsBus.Send<T>()` | 添加冲突检测 | 如果同时存在 Handler 和 Execute() 则抛异常 |

**数据契约：**

```csharp
public sealed class ModeConflictException : Exception
{
    public ModeConflictException(string message) : base(message) { }
    
    public Type CommandType { get; init; }
    public string ConflictReason { get; init; }
}
```

**行为契约：**

**冲突检测矩阵：**

| 场景 | Handler 已注册？ | Struct 有 Execute()？ | 结果 |
|------|----------------|---------------------|------|
| Class 模式 | 是 | 否 | ✓ 分发到 Handler |
| Struct 模式 | 否 | 是 | ✓ 调用 Execute() |
| 混合（非法） | 是 | 是 | ✗ 抛 ModeConflictException |
| 未配置 | 否 | 否 | ✗ 抛 HandlerNotRegisteredException |

**注册时检测：**

```csharp
public void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
{
    if (HasExecuteMethod<TCommand>())
    {
        throw new ModeConflictException(
            $"Cannot register Class handler for {typeof(TCommand).Name}: " +
            "Command implements Execute() (Struct self-handling mode). Choose one mode.");
    }
    // ... 现有注册逻辑
}
```

**验收标准：**
- [ ] 为 Struct Command 注册 Handler 时抛 ModeConflictException
- [ ] 对混合模式 Command 调用 Send() 抛 ModeConflictException
- [ ] 异常消息清晰说明冲突原因和解决建议
- [ ] 文档包含两种模式的使用场景和代码示例

**文档结构：**

```markdown
# CQRS 双模式使用指南

## 何时使用 Class 模式
- 复杂业务逻辑，多个依赖注入
- 异步操作（网络、I/O）
- 对象池化以复用内存
- 示例：任务完成、背包管理

## 何时使用 Struct 模式
- 高频操作（AI 决策、战斗计算）
- 零 GC 分配要求
- 自包含逻辑，无外部依赖
- 示例：伤害计算、寻路查询

## 迁移指南
- 现有代码继续工作（Class 模式）
- 逐步将高频 Handler 转换为 Struct 模式
- 性能对比：Class vs Struct 基准测试

## 未决问题解决方案
Q: Struct 模式如何访问外部服务？
A: 使用服务定位器模式或通过 struct 字段传递服务引用
```

**验收标准：**
- [ ] 文档发布在 `Assets/Change/Framework/Cqrs/Documentation/`
- [ ] 包含两种模式的完整代码示例
- [ ] 包含性能基准测试结果（implement 阶段提供）

**回归风险评估：**
- **影响范围：** 无 - 纯验证逻辑
- **缓解措施：** 冲突检测帮助开发者在开发阶段避免误用

---

## 切片依赖图

```
Slice A (Struct 自处理) ───┐
                           ├──> Slice D (冲突检测 & 文档)
Slice B (对象池集成) ──┬───┘
                       └──> Slice C (异步支持)
```

**实施顺序：**
1. Slice A 和 Slice B 可并行开发（无依赖）
2. Slice C 依赖 Slice B（需要确保异步场景下的 Reset 调用）
3. Slice D 最后，需要所有模式就绪后才能完整测试冲突检测

---

## 关键接口

### Struct 自处理模式（Slice A）

```csharp
// ICommand 扩展（C# 8+ default interface method 或文档约定）
public interface ICommand
{
    // 可选：Struct 实现此方法启用自处理
    // void Execute();
}

// 示例 Struct Command
public readonly struct ApplyDamageCommand : ICommand
{
    public readonly int TargetId;
    public readonly int Amount;
    
    public void Execute()
    {
        CombatSystem.ApplyDamage(TargetId, Amount);
    }
}
```

### 池化集成（Slice B）

```csharp
// Handler 可选实现 IPoolable
public sealed class CompleteQuestHandler 
    : ICommandHandler<CompleteQuestCommand>, IPoolable
{
    private readonly QuestSessionState _state;
    
    public CompleteQuestHandler(QuestSessionState state) => _state = state;
    
    public void Handle(in CompleteQuestCommand command)
    {
        _state.CompleteQuest(command.QuestId);
    }
    
    public void Reset()
    {
        // 清理瞬态状态
    }
}
```

### 异步支持（Slice C）

```csharp
// 异步 Command Handler
public interface IAsyncCommandHandler<TCommand> where TCommand : struct, ICommand
{
    Task ExecuteAsync(TCommand command);
}

// 异步 Query Handler
public interface IAsyncQueryHandler<TQuery, TResult> where TQuery : struct, IQuery<TResult>
{
    Task<TResult> ExecuteAsync(TQuery query);
}

// 异步 API
public interface ICqrsBus
{
    Task SendAsync<TCommand>(TCommand command) where TCommand : struct, ICommand;
    Task<TResult> AskAsync<TQuery, TResult>(TQuery query) where TQuery : struct, IQuery<TResult>;
}
```

---

## 回归风险评估

| 影响范围 | 风险等级 | 缓解措施 |
|---------|---------|----------|
| CqrsBus.Send/Ask 分支逻辑增加 | 低 | 反射结果缓存，性能影响 < 5ns；现有 Handler 路径不变 |
| Handler 执行后增加 IPoolable 检测 | 低 | JIT 优化接口检查至近乎零开销；不实现 IPoolable 的 Handler 无影响 |
| 新增异步 API | 无 | 独立 API，不影响现有同步路径 |
| 冲突检测逻辑 | 无 | 纯验证，不改变正常执行流程 |

---

## 实现优先级

1. **Slice A + Slice B（并行）** - 核心功能，分别实现 Struct 和 Class 模式
2. **Slice C** - 异步支持，依赖 Slice B 完成
3. **Slice D** - 冲突检测和文档，依赖所有切片完成

---

## 性能目标

| 指标 | 目标 | 验证方式 |
|------|------|----------|
| Struct 自处理分发开销 | < 5ns（相对直接调用） | Unity Profiler Deep Profile |
| Struct 模式 GC 分配 | 0 字节 | Unity Profiler Memory Profiler |
| IPoolable 接口检测开销 | < 2ns | BenchmarkDotNet 微基准测试 |
| Reset() 异常处理开销 | < 10ns（无异常时） | BenchmarkDotNet 微基准测试 |

---

## 未决问题解决

| 未决问题（来自 FRD） | 解决方案 | 在何处实现 |
|-------------------|---------|-----------|
| Struct 模式访问外部服务 | 推荐：通过 struct 字段传递服务引用或使用服务定位器 | Slice D 文档 |
| 异步 Command 执行时反注册 Handler | 不支持：用户需确保异步执行完成后再反注册 | Slice C 文档说明 |
| Class 和 Struct 模式混用 | 编译时互斥，运行时冲突检测抛异常 | Slice D 实现 |
