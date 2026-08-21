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

**示例参考**：Quest `ClaimSideQuestRewardHandler`（`QuestCommandHandlers.cs`）。

## 3. Struct 自处理迁移

1. Command/Query struct 改为实现 `ISelfHandlingCommand` / `ISelfHandlingQuery<TResult>`。
2. 添加 `void Execute()`（无参）。**依赖通过 readonly 字段构造注入**——`Execute()` 不能带参（FRD #2 签名约束）。
3. **删除**对应的 Class Handler 类。
4. **移除**装配中的 `RegisterCommand(...)`。
5. 发送处构造时传入依赖：`bus.Send(new XxxCommand(state, ...))`。

**示例参考**：Quest `BumpMainQuestProgressCommand`（`QuestMessages.cs`）——携带 `QuestSessionState` 字段。

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

## 7. Quest 迁移示例速览

| 文件 | 变化 |
|------|------|
| `QuestMessages.cs` | `BumpMainQuestProgressCommand` → `ISelfHandlingCommand`，新增 `QuestSessionState` 字段 |
| `QuestCommandHandlers.cs` | **删除** `BumpMainQuestProgressHandler`；其余 5 个 Handler 加 `IPoolable` |
| `QuestQueryHandlers.cs` | `GetQuestPanelQueryHandler` 加 `IPoolable`（空 Reset，不复用 buffer） |
| `GameHotfixInstaller.cs` | 移除 `RegisterCommand(new BumpMainQuestProgressHandler(state))` |
