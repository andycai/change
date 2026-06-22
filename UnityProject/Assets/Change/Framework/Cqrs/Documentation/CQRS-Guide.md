# CQRS 使用指南

`Change.Framework.Cqrs` 的 Command/Query 全部为**自处理**——command/query 自身包含执行逻辑，不拆分 message 与 handler。按载体分两种：

| 载体 | 接口 | 分发 | 分配语义 | 适用 |
|------|------|------|---------|------|
| Struct 自处理 | `ICommand` / `IQuery<R>` / `IAsyncCommand` / `IAsyncQuery<R>` | `Send` / `Ask` / `SendAsync` / `AskAsync` | 栈分配，0GC | 高频战斗逻辑、严格 0GC 热路径 |
| Class 池化自处理 | `IPooledCommand` / `IPooledAsyncCommand` | `Send<T>(Action<T>)` / `SendAsync<T>(Action<T>)` | 对象池复用，少量分配 | 复杂可变状态 / 异步 I/O（存档、网络、资源加载） |

事件经 `Subscribe` 注册后由 `Publish` 分发，不在本指南范围。

## Struct 自处理模式（0GC）

```csharp
public readonly struct ApplyDamageCommand : ICommand
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

异步 struct command（`IAsyncCommand`）通过 `await bus.SendAsync(...)` 分发。readonly struct 的异步执行应委托给 `static async` helper，避免在 struct 实例上承载 async 状态机。

## Class 池化自处理模式（对象池复用）

适合实例较重或需要异步 I/O 的 command。command 为 class、自处理，实例由 `Change.Framework.Pooling.Pool<T>` 借出复用：

```csharp
public sealed class SaveGameStateCommand : IPooledAsyncCommand
{
    public string PlayerId;        // per-dispatch 数据
    private ICloudStorage _storage; // 长期依赖（构造期 resolve）

    public SaveGameStateCommand()
    {
        _storage = ServiceLocator.Get<ICloudStorage>(); // 无参构造，依赖自行 resolve
    }

    public async UniTask ExecuteAsync()
    {
        await _storage.UploadAsync(PlayerId);
    }

    public void Reset()
    {
        PlayerId = null; // 只清 per-dispatch 瞬态，不清长期依赖
    }
}

// per-dispatch 数据由 configure 回调设置；bus 负责 Get → configure → Execute → Release
await bus.SendAsync<SaveGameStateCommand>(c => c.PlayerId = "p1");
```

同步版同理：实现 `IPooledCommand`，用 `bus.Send<T>(c => ...)`。

### Reset 契约

- bus 在 `Execute`（同步）或 `await ExecuteAsync` 完成（异步）后调 `Pool<T>.Release`，`Release` 内部调用 `Reset`。
- `Reset` 必须幂等，且容忍“仅 configure 部分 / 未 Execute”的半配置状态（异常路径下可能发生）。
- `Reset` 只清 per-dispatch 瞬态字段，不清长期依赖。

### 异步生命周期

bus 在 `await ExecuteAsync()` 完成后才 `Release` 归还，进行中的实例不会进池，杜绝复用竞态。

## 选型建议

- 默认用 Struct 自处理；只有当 command 需要复杂可变状态、异步 I/O，或实例本身较重时，才用 Class 池化自处理。
- Class 模式非 0GC（configure 回调为 capturing lambda，有少量分配），不要用于高频热路径。
