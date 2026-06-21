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
| 事件订阅 | `IEventHandler<TEvent>` 类 | `Subscribe(Action<TEvent>)` 静态委托 |

## 关键约束

- `Execute()` **无参**，依赖走 struct 字段（构造注入）。
- Struct 实现 `ICommandHandler`/`IQueryHandler` 被注册时拒绝（`InvalidOperationException`）。
- 为 `ISelfHandlingCommand` 类型注册 Class handler → `ModeConflictException`。
- Struct 自处理不支持异步（`SendAsync`/`AskAsync` → `NotSupportedException`）。

## 双模式事件订阅

- `Subscribe<TEvent>(IEventHandler<TEvent> handler)` — Class handler。
- `Subscribe<TEvent>(Action<TEvent> handler)` — 静态委托，**不得捕获变量**（`Target != null` → `ClosureCaptureException`）。
