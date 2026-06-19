# CQRS 双模式使用指南

`Change.Framework.Cqrs` 提供两种 Command/Query 执行模式，按场景选择。

## 模式一览

| 维度 | Class 模式 | Struct 自处理模式 |
|------|-----------|-----------------|
| 适用 | 复杂业务逻辑、需 DI、异步 I/O | 高频战斗逻辑、严格 0GC |
| 实现 | `ICommandHandler<T>` / `IQueryHandler<T,R>` 类 | `ISelfHandlingCommand` / `ISelfHandlingQuery<R>` readonly struct |
| 依赖注入 | 支持（构造注入） | 不支持（自包含） |
| 对象池 | 可选实现 `IPoolable`，框架在 Handle 后调 `Reset()` | 不适用 |
| 异步 | `SendAsync` / `AskAsync`（`IAsyncCommandHandler` / `IAsyncQueryHandler`） | 不支持（用同步 `Send` / `Ask`） |
| GC | 复用 handler 实例 | 栈分配，0GC |

## Class 模式

```csharp
public readonly struct CompleteQuestCommand : ICommand
{
    public int QuestId { get; }
    public CompleteQuestCommand(int id) { QuestId = id; }
}

public sealed class CompleteQuestHandler
    : ICommandHandler<CompleteQuestCommand>, IPoolable
{
    private readonly QuestSessionState _state;
    public CompleteQuestHandler(QuestSessionState state) { _state = state; }

    public void Handle(in CompleteQuestCommand command)
    {
        _state.CompleteQuest(command.QuestId);
    }

    public void Reset()
    {
        // 清理瞬态字段；框架在每次 Handle 后调用，不负责归还池。
    }
}

// 注册
bus.RegisterCommand(new CompleteQuestHandler(state));
bus.Send(new CompleteQuestCommand(42));
```

注：`IPoolable` 位于 `Change.Framework.Pooling` 命名空间。

## Struct 自处理模式

```csharp
public readonly struct ApplyDamageCommand : ISelfHandlingCommand
{
    private readonly CombatBoard _board;
    private readonly int _targetId;
    private readonly int _amount;

    public ApplyDamageCommand(CombatBoard board, int targetId, int amount)
    {
        _board = board; _targetId = targetId; _amount = amount;
    }

    public void Execute() => _board.ApplyDamage(_targetId, _amount);
}

// 无需注册，直接分发（0GC）
bus.Send(new ApplyDamageCommand(board, 7, 12));
```

## 异步（仅 Class 模式）

```csharp
public sealed class SaveGameStateHandler : IAsyncCommandHandler<SaveGameStateCommand>
{
    private readonly ICloudStorage _storage;
    public SaveGameStateHandler(ICloudStorage storage) { _storage = storage; }

    public async Task ExecuteAsync(SaveGameStateCommand command)
    {
        await _storage.UploadAsync(command.PlayerId, command.Data);
    }
}

bus.RegisterAsyncCommand(new SaveGameStateHandler(storage));
await bus.SendAsync(new SaveGameStateCommand(playerId, data));
```

## 模式互斥

同一 Command/Query 类型**只能选一种模式**。若类型实现 `ISelfHandlingCommand`，则注册 Class Handler 会抛 `ModeConflictException`。注册时即拒绝，分发时无需关心混合态。

## 已知限制

- **Struct 自处理分发**当前基于 `DynamicMethod` 实现零装箱约束调用。该机制在 **Mono 运行时（Unity Editor）** 完美工作且零 GC 分配，但在 **IL2CPP/AOT 构建**（移动端设备）上不可用。若需要在 IL2CPP 构建中启用 Struct 自处理模式，需配合 Roslyn source generator 预生成每类型分发代码，这将在后续版本中规划。

## 常见问题

**Q：Struct 自处理如何访问外部服务？**
A：通过 readonly struct 字段在构造时传入服务引用（如上例 `CombatBoard`），或使用服务定位器。避免在 Execute 内反射查找。

**Q：异步执行中反注册 Handler 会怎样？**
A：不支持。请确保 `await SendAsync/AskAsync` 完成后再反注册；运行中反注册会导致 `Reset` 指向已释放的实例。

**Q：同步和异步 Handler 能否为同一 Command 共存？**
A：可以。它们位于不同注册表，分别经 `Send` / `SendAsync` 分发。按场景择一即可。
