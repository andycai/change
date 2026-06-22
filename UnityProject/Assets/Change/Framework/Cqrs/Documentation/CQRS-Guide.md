# CQRS 使用指南

`Change.Framework.Cqrs` 是一个面向高频战斗逻辑、严格 0-GC 的进程内 CQRS 总线。

## 核心模型

- **Command / Query 自处理**：命令和查询都是 `readonly struct`，自带 `Execute()` / `Query()` 方法。分发时总线直接调用其固有方法，**无需注册任何 Handler**，零装箱、零堆分配。
- **Event 需订阅**：事件代表"已发生的领域事实"，可被多个订阅者监听，必须通过 `Subscribe` 注册。
- **不存在 Class Handler 模式**：早期版本的 `ICommandHandler` / `IQueryHandler` / `RegisterCommand` 等 Class 模式已在自处理简化中移除。命令/查询只走自处理路径。

| 消息 | 接口 | 注册 | 分发 | GC |
|------|------|------|------|----|
| Command | `ICommand` (`Execute()`) | 不需要 | `Send` | 0 |
| Query | `IQuery<TResult>` (`Query()`) | 不需要 | `Ask` | 0 |
| Async Command | `IAsyncCommand` (`ExecuteAsync()`) | 不需要 | `SendAsync` | 异步状态机（非热路径） |
| Async Query | `IAsyncQuery<TResult>` (`QueryAsync()`) | 不需要 | `AskAsync` | 异步状态机（非热路径） |
| Event | `IEvent` | `Subscribe` | `Publish` | 0（分发期） |

## Command（同步自处理）

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

## Query（同步自处理）

`TResult` 为泛型类型参数流过分发，引用类型与值类型结果都不装箱。

```csharp
public readonly struct GetHeroHpQuery : IQuery<int>
{
    private readonly CombatBoard _board;
    private readonly int _heroId;
    public GetHeroHpQuery(CombatBoard board, int heroId) { _board = board; _heroId = heroId; }
    public int Query() => _board.GetHp(_heroId);
}

int hp = bus.Ask<GetHeroHpQuery, int>(new GetHeroHpQuery(board, heroId));
```

## 异步（自处理 + UniTask）

异步命令/查询用于 I/O 等非热路径场景，返回 `UniTask`。`IAsyncCommand` **不**继承 `ICommand`；同一 struct 可同时实现两者以支持同步与异步两种分发。

```csharp
public readonly struct SaveGameStateCommand : IAsyncCommand
{
    private readonly ICloudStorage _storage;
    private readonly string _playerId;
    private readonly byte[] _data;
    public SaveGameStateCommand(ICloudStorage storage, string playerId, byte[] data)
    { _storage = storage; _playerId = playerId; _data = data; }

    public UniTask ExecuteAsync() => _storage.UploadAsync(_playerId, _data);
}

await bus.SendAsync(new SaveGameStateCommand(storage, playerId, data));
```

## Event（订阅 + 发布）

事件分发按**闭合泛型类型**严格匹配：基接口订阅不会被派生事件触发，每个具体事件类型必须单独订阅。

订阅有两种形式：

**1. `IEventHandler<TEvent>`（class 实现）** —— 适合需要实例状态的处理器。

```csharp
public sealed class ScoreChangedHandler : IEventHandler<ScoreChangedEvent>
{
    private readonly ScoreBoard _board;
    public ScoreChangedHandler(ScoreBoard board) { _board = board; }
    public void Handle(in ScoreChangedEvent @event) => _board.Add(@event.Delta);
}

bus.Subscribe(new ScoreChangedHandler(board));
bus.Publish(new ScoreChangedEvent(10));
```

值类型 handler 会在注册时被拒绝并抛 `InvalidOperationException`（避免接口装箱）。

**2. 静态委托（`Action<TEvent>`）** —— 仅允许 **static 方法或非捕获 lambda**。捕获变量的委托会分配闭包对象、破坏 0-GC，注册时即抛 `ClosureCaptureException`。

```csharp
// OK：静态方法
bus.Subscribe<ScoreChangedEvent>(OnScoreChanged);
static void OnScoreChanged(ScoreChangedEvent e) { /* ... */ }

// OK：非捕获 lambda（不引用任何外部变量）
bus.Subscribe<ScoreChangedEvent>(static e => { /* ... */ });

// 抛 ClosureCaptureException：捕获了局部变量
int captured = 0;
bus.Subscribe<ScoreChangedEvent>(e => captured += e.Delta);

// 抛 ClosureCaptureException：实例方法委托 Target 非空
bus.Subscribe<ScoreChangedEvent>(recorder.Record);
```

`Publish` 会调用**所有**订阅者；任一订阅者抛异常时，其余订阅者仍会被调用，最终抛出聚合所有异常的 `AggregateException`。订阅者按注册顺序调用；反订阅使用 `RemoveAt`（保序，非 swap-back）。

## Bootstrap 生命周期

`CqrsBootstrap` 在 `Build()` 前收集事件订阅，`Build()` 返回 `ICqrsBus`（底层即 `CqrsBus`），并保证后续调用返回同一实例（线程安全的双重检查锁）。

```csharp
var bootstrap = new CqrsBootstrap();
bootstrap.Subscribe(new ScoreChangedHandler(board));
ICqrsBus runtime = bootstrap.Build();   // 后续 Build() 返回同一实例
runtime.Send(new ApplyDamageCommand(board, 7, 12));
```

## 线程模型

**仅限单线程（主线程）使用。** `Publish` 直接遍历**活跃的**订阅列表，既不加锁也不快照（快照会分配、破坏 0-GC 热路径）。因此：

- 不要在 `SendAsync` / `AskAsync` 的延续（可能落在工作线程）里并发 `Subscribe` / `Unsubscribe` / `Publish`。
- 不要在某个事件的处理器内同步订阅/反订阅**同一事件类型**（会跳过或重复调用、甚至越界）。
- 注册/反订阅请在非分发期间进行。

## 性能监控（可选）

监控默认**关闭**。定义编译符号 `ENABLE_CQRS_MONITORING` 后，`CqrsBus.Send/Ask/Publish/SendAsync/AskAsync` 会用 `GC.GetAllocatedBytesForCurrentThread()`（零分配）测量每次分发的分配增量与耗时，写入活跃监控器。符号未定义时总线零监控开销、相关成员不存在。

```csharp
CqrsBus.SetActiveMonitor(monitor);   // 进程级安装；传 null 关闭
bus.Send(new ApplyDamageCommand(...));
foreach (var m in monitor.GetAllMetrics())
    Debug.Log($"{m.MessageType}: count={m.ExecutionCount} gc={m.TotalGcBytes}");
```

单次分配超过阈值（默认 100 字节，见 `DefaultThresholdPolicy`）会触发 `monitor.OnThresholdExceeded`。**监控器是 0-GC 承诺的唯一自动化回归守护**：建议在 CI / 性能巡检构建中开启此符号。

## 常见问题

**Q：自处理 struct 如何访问外部服务？**
A：通过 `readonly struct` 字段在构造时传入服务引用（如上例 `CombatBoard` / `ICloudStorage`），或使用服务定位器。避免在 `Execute` 内反射查找。

**Q：struct 自处理在 IL2CPP / AOT（移动端）上可用吗？**
A：可用。当前分发是普通的 constrained `callvirt`（`cmd.Execute()`），JIT 与 AOT 均零装箱、零分配，无需 `DynamicMethod` 或 source generator。早期文档提及的 "DynamicMethod / IL2CPP 不可用" 限制属于已废弃的旧实现描述，**不再适用**。

**Q：同步和异步能对同一消息类型共存吗？**
A：可以——让同一 struct 同时实现 `ICommand` 与 `IAsyncCommand`（或 `IQuery<TResult>` 与 `IAsyncQuery<TResult>`）。`Send` 走同步路径，`SendAsync` 走异步路径，二者互不影响。
