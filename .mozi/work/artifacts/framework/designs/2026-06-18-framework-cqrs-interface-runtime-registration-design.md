# CQRS 接口层简化与运行时注册 架构设计

> 日期: 2026-06-18 | 状态: 草稿
> FRD: [接口层简化与运行时注册](../discover/2026-06-18-framework-cqrs-interface-runtime-registration-frd.md)
> 上游: [CQRS 架构评审与优化设计 (2026-05-07)](./2026-05-07-cqrs-architecture-review-and-optimization-design.md)

## 上游产出引用

### 来自 FRD

| 引用内容 | 如何使用 |
|---------|----------|
| 决策 #1: 统一为 `IEvent`，移除 `IDomainEvent` | 切片 A 删除 `IDomainEvent`/`IDomainEventHandler`，更新 `ICqrsRuntime.Publish` 约束 |
| 决策 #2: 放弃硬分离，采用双接口模式 | 切片 B 让 `CqrsBus` 同时实现 `ICqrsRegistry` + `ICqrsRuntime`，删除 `CqrsRuntime` 适配器 |
| 决策 #3: 主线程注册，调用者保证线程安全 | 切片 C 移除 `Freeze()` 阻塞语义，移除注册锁，线程安全以文档约束 |
| 决策 #4: Command/Query 禁止重复注册，Event 追加 | 切片 C 实现差异化重复检测逻辑 |
| 决策 #5: 支持 Unregister API | 切片 C 新增 `UnregisterCommand`/`UnregisterQuery`/`Unsubscribe` |
| 未决问题 #1: 注册时正在执行 Command 的行为 | 设计决策：注册立即生效，不影响正在执行的调度（字典独立） |
| 未决问题 #2: 反注册时异步 Command 的行为 | 设计决策：反注册立即从字典移除，已在执行的不中断 |
| 验收条件: 单元测试覆盖注册/反注册场景 | 切片 D 编写 RuntimeRegistrationTests |

### 来自现有设计文档 (2026-05-07 CQRS Architecture Review)

| 引用内容 | 如何使用 |
|---------|----------|
| `ICqrsBootstrap` / `ICqrsRuntime` 接口分离 | 保留 `ICqrsBootstrap` 作为启动期便捷包装，`ICqrsRuntime` 由 `CqrsBus` 直接实现 |
| 三注册制（ICommandRegistry / IQueryRegistry / IDomainEventRegistry）概念 | 保留概念但在 `ICqrsRegistry` 层面扁平化，内部字典维持 3 个独立容器 |
| 零分配调度保证 | 不改变调度热路径，仅注册/反注册路径允许分配 |
| 命名约定：Command 用 `*Command`，Event 用 `*Evt`/`*Event` | 不变，继续沿用 |

## 架构决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 接口层次简化策略 | 方案 A：完全简化，CqrsBus 双接口直用 | 与 FRD 决策 #2 对齐；接口从 4 层减到 2 层（Bootstrap→Bus→Runtime→RuntimeProvider 变为 Bootstrap→Bus） |
| Freeze 机制 | 移除 `Freeze()` 公开 API | 运行时注册需求与 Freeze 互斥；线程安全改为调用者保证（主线程约束） |
| CqrsRuntime 适配器 | 删除，CqrsBus 直接实现 ICqrsRuntime | 适配器是纯转发层，无业务价值；删除后减少一层间接调用 |
| ICqrsBootstrap 保留 | 保留但简化 Build() 实现 | 现有引导期代码依赖 ICqrsBootstrap；Build() 直接返回 CqrsBus（同时是 ICqrsRuntime） |
| Unregister 不存在时的行为 | Command/Query 抛异常，Event 静默忽略 | Command/Query 1:1 语义要求精确状态；Event 多订阅者场景下幂等性更实用 |
| 调度期间注册/反注册 | 不阻止，字典操作立即生效 | 主线程保证无并发；已完成 dispatch 不受影响 |

## 文件地图

| 文件 | 所属切片 | 职责 | 操作 |
|------|----------|------|------|
| `Abstractions/IDomainEvent.cs` | A | 领域事件标记接口（冗余） | **删除** |
| `Abstractions/IDomainEventHandler.cs` | A | 领域事件 Handler 接口（冗余） | **删除** |
| `Abstractions/ICqrsRuntime.cs` | A | 运行时调度面接口定义 | 修改：Publish 约束 `IDomainEvent` → `IEvent` |
| `Abstractions/IEvent.cs` | A | 事件标记接口（保留为唯一事件标记） | 不修改 |
| `Abstractions/IEventHandler.cs` | A | 事件 Handler 接口 | 不修改 |
| `Core/CqrsRuntime.cs` | A, B | CqrsBus → ICqrsRuntime 适配器 | 切片 A 修改 Publish，切片 B **删除** |
| `Abstractions/ICqrsRuntimeProvider.cs` | B | Context 级 Runtime 提供者接口 | **删除** |
| `Core/CqrsContextRuntimeProvider.cs` | B | Context → Runtime 映射实现 | **删除** |
| `Abstractions/ICqrsBootstrap.cs` | B | 引导期注册面接口 | 不修改（签名不变，语义简化） |
| `Core/CqrsBootstrap.cs` | B | 引导期实现 | 修改：Build() 直接返回 CqrsBus |
| `Core/CqrsBus.cs` | A, B, C | CQRS 核心实现 | 切片 A 无修改，切片 B 添加 `: ICqrsRuntime`，切片 C 添加 Unregister 方法 |
| `Abstractions/ICqrsRegistry.cs` | C | 注册面接口定义 | 修改：添加 Unregister 方法，移除 Freeze |
| `Exceptions/DuplicateRegistrationException.cs` | C | 重复注册异常 | 不修改（复用） |
| `Exceptions/HandlerNotRegisteredException.cs` | C | Handler 未注册异常 | 不修改（复用） |
| `Tests/EditMode/Cqrs/DomainEventSemanticsTests.cs` | A | 领域事件语义测试 | 修改：`IDomainEvent` → `IEvent` |
| `Tests/EditMode/Cqrs/ZeroAllocationDispatchTests.cs` | A, B | 零分配调度测试 | 修改：适配接口变更 |
| `Tests/EditMode/Cqrs/CqrsBootstrapLifecycleTests.cs` | A, B | Bootstrap 生命周期测试 | 修改：适配 Build 行为变更 |
| `Tests/EditMode/Cqrs/CqrsArchitectureGuardTests.cs` | A, B | 架构约束测试 | 修改：更新接口引用 |
| `Tests/EditMode/Cqrs/CqrsContextRuntimeProviderTests.cs` | B | RuntimeProvider 测试 | **删除**（随实现一同移除） |
| `Tests/EditMode/Cqrs/RuntimeRegistrationTests.cs` | D | 运行时注册/反注册测试 | **新增** |

## 切片分解

### 切片 A: IEvent 统一（移除 IDomainEvent）

**依赖：** 无
**风险等级：** 低
**涉及文件：** 4 个修改 + 2 个删除

**内容：** 移除 `IDomainEvent` 和 `IDomainEventHandler` 接口，将 `ICqrsRuntime.Publish` 的泛型约束从 `IDomainEvent` 改为 `IEvent`，使 `IEvent` 成为唯一的事件标记接口。所有现有领域事件定义自动兼容（因为 `IDomainEvent : IEvent` 是空继承）。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `ICqrsRuntime.Publish` | `void Publish<TEvent>(in TEvent @event) where TEvent : struct, IEvent` | 将事件发布到所有已订阅的 Handler（约束从 `IDomainEvent` 放宽到 `IEvent`） |
| `CqrsRuntime.Publish` | 同上 | 委托到 `_bus.Publish(in @event)`，签名与接口一致 |

**数据契约：**
```csharp
// 变更前
public interface IDomainEvent : IEvent { }  // 空继承，无附加语义

// 变更后：IEvent 是唯一的事件标记接口
public interface IEvent { }  // 不变

// 所有现有事件自动兼容：
// struct AttributeChangedEvt : IDomainEvent  → 改为 →  struct AttributeChangedEvt : IEvent
// 行为语义不变
```

**行为契约：**
- 输入：实现 `IEvent` 的 readonly struct
- 输出：所有匹配的 `IEventHandler<TEvent>` 被依次调用
- 约束：`IDomainEvent` 类型不再存在，编译期强制所有事件使用 `IEvent`

**验收标准：**
- [ ] `IDomainEvent.cs` 和 `IDomainEventHandler.cs` 已删除
- [ ] `ICqrsRuntime.Publish` 约束为 `IEvent` 而非 `IDomainEvent`
- [ ] 所有现有测试通过（语义等价替换，无行为变化）
- [ ] Gas 模块下的事件定义编译通过（`AttributeChangedEvt` 等改为 `IEvent`）

**回归风险评估：**
- 影响范围：`Gas/Cqrs/` 下事件定义（`AttributeChangedEvt`, `DamageAppliedEvt` 等实现 `IDomainEvent`），以及测试文件中定义的事件
- 缓解措施：`IDomainEvent : IEvent` 为空继承，替换为 `IEvent` 是纯机械操作，无行为变化

---

### 切片 B: 架构扁平化（移除 RuntimeProvider + CqrsBus 双接口）

**依赖：** 切片 A
**风险等级：** 中
**涉及文件：** 4 个修改 + 3 个删除 + 1 个删除（CqrsRuntime）

**内容：** 删除 `ICqrsRuntimeProvider` / `CqrsContextRuntimeProvider` 中间层，让 `CqrsBus` 直接实现 `ICqrsRuntime`，删除 `CqrsRuntime` 适配器类。`CqrsBootstrap.Build()` 直接返回 `CqrsBus` 实例（作为 `ICqrsRuntime`）。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `CqrsBus` 类声明 | `public sealed class CqrsBus : ICqrsBus, ICqrsRegistry, ICqrsRuntime` | 新增实现 `ICqrsRuntime`（Send/Ask/Publish 方法已存在，仅增加接口声明） |
| `CqrsBootstrap.Build()` | `public ICqrsRuntime Build()` | 返回 `_bus`（CqrsBus 实例），不再创建 CqrsRuntime 适配器 |
| `ICqrsRuntime.Send` | `void Send<TCommand>(in TCommand command) where TCommand : struct, ICommand` | CqrsBus 已有同名同签方法，零修改 |
| `ICqrsRuntime.Ask` | `TResult Ask<TQuery, TResult>(in TQuery query) where TQuery : struct, IQuery<TResult>` | CqrsBus 已有同名同签方法，零修改 |
| `ICqrsRuntime.Publish` | `void Publish<TEvent>(in TEvent @event) where TEvent : struct, IEvent` | CqrsBus 已有同名同签方法（切片 A 已统一约束），零修改 |

**数据契约：**
```
变更前的对象图：
  CqrsBootstrap → CqrsBus → Freeze() → CqrsRuntime(ICqrsBus) → ICqrsRuntimeProvider(contextId)
  
变更后的对象图：
  CqrsBootstrap → CqrsBus : ICqrsRegistry + ICqrsRuntime
  直接使用 CqrsBus 作为注册和调度面，无需中间层
```

**行为契约：**
- `CqrsBootstrap.Build()` 首次调用返回 CqrsBus，后续调用返回同一实例（保持幂等语义）
- `CqrsBus` 作为 `ICqrsRuntime` 提供 Send/Ask/Publish，行为与原有 `CqrsRuntime` 适配器完全一致
- 删除 `CqrsContextRuntimeProvider` 后，多 Context 场景直接持有不同的 `CqrsBus` 实例

**验收标准：**
- [ ] `ICqrsRuntimeProvider.cs` 和 `CqrsContextRuntimeProvider.cs` 已删除
- [ ] `CqrsRuntime.cs` 已删除
- [ ] `CqrsBus` 类声明包含 `ICqrsRuntime`
- [ ] `CqrsBootstrap.Build()` 返回 CqrsBus 实例（通过 `ICqrsRuntime` 接口）
- [ ] 所有现有调度测试通过
- [ ] `CqrsContextRuntimeProviderTests.cs` 已删除

**回归风险评估：**
- 影响范围：任何通过 `ICqrsRuntimeProvider.Get("context")` 获取 Runtime 的代码（当前仅框架内部和测试使用，无外部消费者）
- 缓解措施：多 Context 场景改用直接持有不同 CqrsBus 实例；现有测试覆盖调度路径验证零回归

---

### 切片 C: 运行时注册与反注册 API

**依赖：** 切片 B
**风险等级：** 中
**涉及文件：** 2 个修改

**内容：** 在 `ICqrsRegistry` 添加 `UnregisterCommand`/`UnregisterQuery`/`Unsubscribe` 方法，在 `CqrsBus` 实现这些方法，移除 `Freeze()` 阻塞。注册/反注册可在 `Build()` 之后随时调用。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `ICqrsRegistry.RegisterCommand` | `void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler) where TCommand : struct, ICommand` | 保留。移除 Freeze 检查，移除 lock。重复注册抛 `DuplicateRegistrationException` |
| `ICqrsRegistry.RegisterQuery` | `void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler) where TQuery : struct, IQuery<TResult>` | 保留。移除 Freeze 检查，移除 lock。重复注册抛 `DuplicateRegistrationException` |
| `ICqrsRegistry.Subscribe` | `void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : struct, IEvent` | 保留。移除 Freeze 检查，移除 lock。重复订阅追加到 Handler 列表 |
| `ICqrsRegistry.UnregisterCommand` | `void UnregisterCommand<TCommand>() where TCommand : struct, ICommand` | **新增。** 移除 Command Handler 注册。未注册时抛 `HandlerNotRegisteredException` |
| `ICqrsRegistry.UnregisterQuery` | `void UnregisterQuery<TQuery, TResult>() where TQuery : struct, IQuery<TResult>` | **新增。** 移除 Query Handler 注册。未注册时抛 `HandlerNotRegisteredException` |
| `ICqrsRegistry.Unsubscribe` | `void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : struct, IEvent` | **新增。** 从订阅列表移除指定 Handler。Handler 不在列表中时静默忽略（幂等） |

**移除的方法：**

| 接口 | 签名 | 理由 |
|------|------|------|
| `ICqrsRegistry.Freeze` | `void Freeze()` | 运行时注册需求与 Freeze 互斥；线程安全改为调用者保证 |

**数据契约：**
```csharp
// ICqrsRegistry 变更后签名
public interface ICqrsRegistry
{
    // === 保留（移除 lock + Freeze 检查） ===
    void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
        where TCommand : struct, ICommand;
    void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
        where TQuery : struct, IQuery<TResult>;
    void Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : struct, IEvent;

    // === 新增 ===
    void UnregisterCommand<TCommand>()
        where TCommand : struct, ICommand;
    void UnregisterQuery<TQuery, TResult>()
        where TQuery : struct, IQuery<TResult>;
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : struct, IEvent;
}
```

**行为契约：**

| 操作 | 前提条件 | 输出 | 副作用 |
|------|---------|------|--------|
| `RegisterCommand<T>(h)` | T 未注册 | 成功 | 字典添加 `T → h` |
| `RegisterCommand<T>(h)` | T 已注册 | `DuplicateRegistrationException` | 无 |
| `UnregisterCommand<T>()` | T 已注册 | 成功 | 字典移除 `T` |
| `UnregisterCommand<T>()` | T 未注册 | `HandlerNotRegisteredException` | 无 |
| `RegisterQuery<T,R>(h)` | `<T,R>` 未注册 | 成功 | 字典添加 `QueryKey(T,R) → h` |
| `RegisterQuery<T,R>(h)` | `<T,R>` 已注册 | `DuplicateRegistrationException` | 无 |
| `UnregisterQuery<T,R>()` | `<T,R>` 已注册 | 成功 | 字典移除 `QueryKey(T,R)` |
| `UnregisterQuery<T,R>()` | `<T,R>` 未注册 | `HandlerNotRegisteredException` | 无 |
| `Subscribe<T>(h)` | 任意 | 成功 | `h` 追加到 `T` 的 Handler 列表 |
| `Unsubscribe<T>(h)` | `h` 在列表中 | 成功 | 从列表移除 `h` |
| `Unsubscribe<T>(h)` | `h` 不在列表 | 成功（幂等） | 无 |

**验收标准：**
- [ ] `RegisterCommand` 重复注册抛 `DuplicateRegistrationException`
- [ ] `RegisterQuery` 重复注册抛 `DuplicateRegistrationException`
- [ ] `Subscribe` 重复订阅追加成功（不抛异常）
- [ ] `UnregisterCommand` 未注册时抛 `HandlerNotRegisteredException`
- [ ] `UnregisterQuery` 未注册时抛 `HandlerNotRegisteredException`
- [ ] `Unsubscribe` 未订阅时静默成功（幂等）
- [ ] 注册 → 发送 → 反注册 → 发送（抛 HandlerNotRegisteredException）
- [ ] 注册 → 反注册 → 重新注册 → 发送（正常执行）

**回归风险评估：**
- 影响范围：移除 `lock (_registrationGate)` 后，如果调用者未遵守主线程约束会出现竞态条件
- 缓解措施：文档明确约束"注册/反注册必须在主线程调用"；`Freeze()` 移除后现有 Bootstrap 代码中的 `Freeze()` 调用需一并删除

---

### 切片 D: 测试套件与线程安全文档

**依赖：** 切片 B, C
**风险等级：** 低
**涉及文件：** 1 个新增 + 4 个修改

**内容：** 编写 `RuntimeRegistrationTests.cs` 覆盖所有注册/反注册场景。更新现有测试适配接口变更。编写线程安全使用约束文档。

**接口契约：** 本切片不定义新接口，仅验证切片 B/C 的契约。

**验收标准：**
- [ ] `RuntimeRegistrationTests.cs` 覆盖：
  - Command 注册→发送→反注册→发送（异常）
  - Query 注册→查询→反注册→查询（异常）
  - Event 多订阅者→Publish→Unsubscribe→Publish
  - 重复注册 Command/Query 抛异常
  - 重复订阅 Event 追加成功
  - 反注册后重新注册正常执行
  - Unsubscribe 幂等性
- [ ] 所有现有 CQRS 测试套件通过
- [ ] 线程安全使用约束文档（README 或代码注释）明确：注册/反注册必须在主线程调用

**回归风险评估：**
- 影响范围：无，纯增量验证
- 缓解措施：所有新增测试为独立测试方法，不修改现有测试的断言逻辑

---

## 切片依赖图

```
切片 A: IEvent 统一（无依赖 | 低风险）
  └── 切片 B: 架构扁平化（依赖 A | 中风险）
        └── 切片 C: 运行时注册 API（依赖 B | 中风险）
              └── 切片 D: 测试套件与文档（依赖 B, C | 低风险）
```

## 关键接口

> 变更后对外暴露的接口签名汇总。

```csharp
// === 标记接口（不变） ===
public interface ICommand { }
public interface IQuery<TResult> { }
public interface IEvent { }                    // 唯一的事件标记（IDomainEvent 已删除）

// === Handler 接口（不变） ===
public interface ICommandHandler<TCommand> where TCommand : struct, ICommand {
    void Handle(in TCommand command);
}
public interface IQueryHandler<TQuery, TResult> where TQuery : struct, IQuery<TResult> {
    TResult Handle(in TQuery query);
}
public interface IEventHandler<TEvent> where TEvent : struct, IEvent {
    void Handle(in TEvent @event);
}

// === 注册面：ICqrsRegistry（变更） ===
public interface ICqrsRegistry {
    void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
        where TCommand : struct, ICommand;
    void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
        where TQuery : struct, IQuery<TResult>;
    void Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : struct, IEvent;

    // 新增
    void UnregisterCommand<TCommand>() where TCommand : struct, ICommand;
    void UnregisterQuery<TQuery, TResult>() where TQuery : struct, IQuery<TResult>;
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : struct, IEvent;
    // Freeze() 已移除
}

// === 调度面：ICqrsRuntime（变更） ===
public interface ICqrsRuntime {
    void Send<TCommand>(in TCommand command) where TCommand : struct, ICommand;
    TResult Ask<TQuery, TResult>(in TQuery query) where TQuery : struct, IQuery<TResult>;
    void Publish<TEvent>(in TEvent @event) where TEvent : struct, IEvent;  // IDomainEvent → IEvent
}

// === 调度面：ICqrsBus（不变） ===
public interface ICqrsBus {
    void Send<TCommand>(in TCommand command) where TCommand : struct, ICommand;
    TResult Query<TQuery, TResult>(in TQuery query) where TQuery : struct, IQuery<TResult>;
    TResult Ask<TQuery, TResult>(in TQuery query) where TQuery : struct, IQuery<TResult>;
    void Publish<TEvent>(in TEvent @event) where TEvent : struct, IEvent;
}

// === 引导面：ICqrsBootstrap（不变） ===
public interface ICqrsBootstrap {
    void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
        where TCommand : struct, ICommand;
    void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
        where TQuery : struct, IQuery<TResult>;
    void Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : struct, IEvent;
    ICqrsRuntime Build();
}

// === 核心实现（变更） ===
public sealed class CqrsBus : ICqrsBus, ICqrsRegistry, ICqrsRuntime {
    // 同时实现三个接口：调度(ICqrsBus) + 注册(ICqrsRegistry) + 运行时(ICqrsRuntime)
    // 无 Freeze 阻塞，无 lock，线程安全由调用者保证
}
```

## 整体回归风险评估

| 影响范围 | 风险等级 | 缓解措施 |
|---------|---------|----------|
| Gas 模块事件定义（`AttributeChangedEvt` 等改为 `IEvent`） | 低 | 纯机械替换，空继承关系，无行为变化 |
| 调度热路径（Send/Ask/Publish） | 低 | CqrsBus 调度方法不修改，仅增加接口声明；删除 CqrsRuntime 适配器消除一层虚调用 |
| 现有 Bootstrap 流程 | 中 | `Build()` 返回 CqrsBus 而非 CqrsRuntime 适配器，但接口签名不变（`ICqrsRuntime`） |
| 多 Context 场景 | 低 | 删除 RuntimeProvider 后，多 Context 改为直接持有不同 CqrsBus 实例（当前无外部消费者） |
| Freeze 移除 | 中 | 现有代码中调用 `Freeze()` 的点需删除（`CqrsBootstrap.Build()` 内部）；线程安全从框架保证改为调用者保证 |
| 注册/反注册并发 | 中 | 移除 lock 后，多线程注册会产生竞态；缓解：文档明确主线程约束，不引入锁开销 |
