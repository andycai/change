# CQRS Event 双模式订阅 架构设计

> 日期: 2026-06-18 | 状态: 草稿
> FRD: [.mozi/artifacts/framework/discover/2026-06-18-framework-cqrs-dual-mode-event-frd.md](../discover/2026-06-18-framework-cqrs-dual-mode-event-frd.md)
> 上游: [ideas/2025-06-18-change-framework-cqrs-refactor-idea.md](../ideas/2025-06-18-change-framework-cqrs-refactor-idea.md), [designs/2026-05-07-cqrs-architecture-review-and-optimization-design.md](../designs/2026-05-07-cqrs-architecture-review-and-optimization-design.md)

## 上游产出引用

### 来自 FRD

| 引用内容 | 如何使用 |
|---------|----------|
| 决策 #1: Event 订阅双模式 (Class Handler + 静态委托) | 架构决策采用统一存储方案，通过接口抽象两种模式 |
| 决策 #2: 闭包检测 (运行时检测 handler.Target != null) | 切片 B 实现 DelegateSubscription 时执行此检测 |
| 决策 #3: 失败策略 (AggregateException 聚合) | 切片 C 实现 Publish 时采用异常聚合逻辑 |
| 决策 #4: 订阅顺序 (按注册顺序调用) | 切片 C 使用 List 存储保证顺序 |
| 决策 #5: 统一 IEvent (移除 IDomainEvent) | 所有切片使用 IEvent 约束，与现有代码保持一致 |
| 验收条件: 0GC 分配初步验证 | 所有切片设计避免 boxing，使用 `in` 参数传递 struct |

### 来自 Ideas 文档

| 引用内容 | 如何使用 |
|---------|----------|
| 痛点: 每个 Event 都需要 Handler 类 | 通过静态委托模式减少 Handler 类数量 |
| 需求: 支持订阅接口而非强制 Handler 类 | 切片 B 实现 Subscribe(Action<TEvent>) 重载 |
| 简化目标: 减少概念负担 | 统一存储模型，避免多套注册机制 |

### 来自 2026-05-07 设计文档

| 引用内容 | 如何使用 |
|---------|----------|
| 7.2 Domain events (more flexible) | 指导双模式设计：Class handler + Static function subscription |
| 约束: Function subscriptions must be non-capturing | 切片 B 实现闭包检测 |
| 错误处理: Publish aggregates failures as AggregateException | 切片 C 采用相同策略 |
| 语义规则: Domain event handlers 不得 dispatch business commands | 架构约束继承，不在本设计范围内实施 |

## 架构决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 订阅存储模型 | 统一存储 (Option A): 单一 `List<IEventSubscription<TEvent>>`，接口抽象两种订阅类型 | 1. 简化 Publish 逻辑 - 单一迭代循环<br>2. 注册顺序天然保证 - 无需跨集合同步<br>3. Unregister 实现简单 - 单集合查找<br>4. 性能可接受 - Event 场景非 Command 级热路径，虚调用开销可忽略<br>5. 接口包装分配发生在注册时（低频），不在 dispatch 时 |
| 闭包检测时机 | 运行时检测 (注册时) | FRD 决策 #2：编译时检测需要 Roslyn 分析器，运行时检测实现简单且足够 |
| Unregister 生命周期 | Freeze 后禁止 | 与现有 CqrsBus 的 Freeze 语义一致，registration phase 后不可变 |
| IEvent vs IDomainEvent | 使用 IEvent | FRD 决策 #5 + 现有代码已使用 IEvent（见 CqrsBootstrap.Subscribe<TEvent>） |
| Action<TEvent> 参数传递 | 值传递 (不支持 `in`) | C# Action<T> 不支持 `in` 修饰符，委托调用时 struct 会拷贝（权衡：简化 API vs 一次拷贝开销） |

## 文件地图

| 文件 | 所属切片 | 职责 |
|------|----------|------|
| `Abstractions/IEventSubscription.cs` | 切片 A | 统一订阅接口，定义 Invoke 和 Type |
| `Core/EventSubscriptions/HandlerSubscription.cs` | 切片 A | 包装 IEventHandler<TEvent> 的订阅实现 |
| `Core/EventSubscriptions/DelegateSubscription.cs` | 切片 B | 包装 Action<TEvent> 的订阅实现，执行闭包检测 |
| `Exceptions/ClosureCaptureException.cs` | 切片 B | 闭包捕获异常 |
| `Core/EventSubscriptions/EventSubscriptionRegistry.cs` | 切片 C | 管理 List<IEventSubscription<TEvent>>，实现 InvokeAll |
| `Core/CqrsBus.cs` | 切片 C | 重构 Publish 方法使用 EventSubscriptionRegistry |
| `Abstractions/ICqrsBootstrap.cs` | 切片 A, B, D | 添加 Subscribe/Unsubscribe 方法重载 |
| `Core/CqrsBootstrap.cs` | 切片 A, B, D | 实现新的订阅/反订阅方法 |

## 切片分解

### 切片 A: 核心订阅抽象与 Class Handler 模式

**依赖：** 无  
**风险等级：** 低  
**涉及文件：** `Abstractions/IEventSubscription.cs`, `Core/EventSubscriptions/HandlerSubscription.cs`, `Abstractions/ICqrsBootstrap.cs`, `Core/CqrsBootstrap.cs`

**内容：** 定义统一的订阅抽象接口 IEventSubscription<TEvent>，实现 HandlerSubscription<TEvent> 包装现有的 IEventHandler<TEvent>。保持现有 Subscribe<TEvent>(IEventHandler<TEvent>) API 不变，内部迁移到新的订阅模型。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `IEventSubscription<TEvent>` | `void Invoke(in TEvent @event);`<br>`SubscriptionType Type { get; }` | 统一订阅接口：Invoke 执行订阅逻辑，Type 标识订阅类型 (ClassHandler/StaticDelegate) |
| `HandlerSubscription<TEvent>` | `HandlerSubscription(IEventHandler<TEvent> handler)` | 包装 IEventHandler，实现 IEventSubscription |
| `ICqrsBootstrap.Subscribe<TEvent>` | `void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : struct, IEvent` | 现有 API 保持不变，内部创建 HandlerSubscription |

**数据契约：**

```csharp
// 内部抽象
internal interface IEventSubscription<TEvent>
    where TEvent : struct, IEvent
{
    void Invoke(in TEvent @event);
    SubscriptionType Type { get; }
}

internal enum SubscriptionType
{
    ClassHandler,
    StaticDelegate
}

// 实现
internal sealed class HandlerSubscription<TEvent> : IEventSubscription<TEvent>
    where TEvent : struct, IEvent
{
    private readonly IEventHandler<TEvent> _handler;
    
    public HandlerSubscription(IEventHandler<TEvent> handler)
    {
        _handler = handler;
    }
    
    public void Invoke(in TEvent @event) => _handler.Handle(in @event);
    public SubscriptionType Type => SubscriptionType.ClassHandler;
    public IEventHandler<TEvent> Handler => _handler;
}
```

**验收标准：**

- [ ] IEventSubscription<TEvent> 接口定义完成，Invoke 接受 `in TEvent` 参数
- [ ] HandlerSubscription<TEvent> 实现 IEventSubscription，正确包装 IEventHandler
- [ ] 现有 Subscribe<TEvent>(IEventHandler<TEvent>) API 继续工作
- [ ] 单个 class handler 注册后 Publish 正常调用
- [ ] Handler.Handle 方法通过 `in` 传递事件，无 boxing（通过 typeof(TEvent).IsValueType 验证）

**回归风险评估：**

- **影响范围：** 现有 Event 订阅和发布代码
- **风险等级：** 低
- **缓解措施：**
  - 保持 ICqrsBootstrap.Subscribe<TEvent>(IEventHandler<TEvent>) 签名不变
  - 内部重构不影响外部 API
  - 运行现有 Event 测试套件验证行为一致性

---

### 切片 B: 静态委托模式与闭包检测

**依赖：** 切片 A (需要 IEventSubscription 接口)  
**风险等级：** 高 (闭包检测是 0GC 保证的核心)  
**涉及文件：** `Core/EventSubscriptions/DelegateSubscription.cs`, `Exceptions/ClosureCaptureException.cs`, `Abstractions/ICqrsBootstrap.cs`, `Core/CqrsBootstrap.cs`

**内容：** 实现 DelegateSubscription<TEvent> 包装 Action<TEvent>，在构造时检测 handler.Target != null 并抛出 ClosureCaptureException。添加 ICqrsBootstrap.Subscribe<TEvent>(Action<TEvent>) 重载，支持静态方法引用和非捕获 lambda。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `DelegateSubscription<TEvent>` | `DelegateSubscription(Action<TEvent> handler)` | 构造时检测闭包：如果 handler.Target != null 则抛出 ClosureCaptureException |
| `ClosureCaptureException` | `ClosureCaptureException(string message)` | 继承 InvalidOperationException，表示委托捕获了变量 |
| `ICqrsBootstrap.Subscribe<TEvent>` | `void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IEvent` | 新增重载，注册静态委托订阅 |

**数据契约：**

```csharp
internal sealed class DelegateSubscription<TEvent> : IEventSubscription<TEvent>
    where TEvent : struct, IEvent
{
    private readonly Action<TEvent> _handler;
    
    public DelegateSubscription(Action<TEvent> handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
            
        // 闭包检测：Target != null 表示委托捕获了实例或局部变量
        if (handler.Target != null)
        {
            throw new ClosureCaptureException(
                "Delegate captures variables; only static methods or non-capturing lambdas are allowed to maintain 0GC guarantee. " +
                $"Delegate type: {handler.GetType().FullName}, Target: {handler.Target.GetType().FullName}");
        }
        
        _handler = handler;
    }
    
    // 注意：Action<TEvent> 不支持 in 修饰符，此处 TEvent 会被拷贝
    public void Invoke(in TEvent @event) => _handler(@event);
    public SubscriptionType Type => SubscriptionType.StaticDelegate;
    public Action<TEvent> Handler => _handler;
}
```

**行为契约：**

**闭包检测规则：**
- 静态方法引用: `handler.Target == null` → 允许
- 非捕获 lambda: `handler.Target == null` → 允许 (编译器优化为静态)
- 捕获局部变量: `handler.Target != null` → 抛出 ClosureCaptureException
- 捕获 `this`: `handler.Target != null` → 抛出 ClosureCaptureException
- 捕获实例方法: `handler.Target != null` → 抛出 ClosureCaptureException

**验收标准：**

- [ ] DelegateSubscription 实现 IEventSubscription
- [ ] Subscribe<TEvent>(Action<TEvent>) API 添加到 ICqrsBootstrap
- [ ] 静态方法引用 (如 `MyClass.HandleEvent`) 注册成功
- [ ] 非捕获 lambda (如 `evt => Console.WriteLine(evt)`) 注册成功（如果 Target == null）
- [ ] 捕获局部变量的 lambda 抛出 ClosureCaptureException
- [ ] 捕获 `this` 的 lambda 抛出 ClosureCaptureException
- [ ] 实例方法引用 (如 `instance.HandleEvent`) 抛出 ClosureCaptureException
- [ ] Publish 正常调用已注册的静态委托

**回归风险评估：**

- **影响范围：** 无 (新增 API，无现有调用者)
- **风险等级：** 低
- **缓解措施：** N/A (纯新增功能)

---

### 切片 C: 统一 Dispatch 与异常聚合

**依赖：** 切片 A, 切片 B (需要两种订阅类型都可注册)  
**风险等级：** 中 (涉及核心 Publish 逻辑重构)  
**涉及文件：** `Core/EventSubscriptions/EventSubscriptionRegistry.cs`, `Core/CqrsBus.cs`

**内容：** 创建 EventSubscriptionRegistry<TEvent> 管理混合订阅列表，重构 CqrsBus.Publish 使用新 registry。实现按注册顺序迭代所有订阅，异常聚合逻辑：捕获每个订阅的异常，继续执行后续订阅，最后抛出 AggregateException。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `EventSubscriptionRegistry<TEvent>` | `void Add(IEventSubscription<TEvent> subscription)` | 添加订阅到内部 List |
| | `void Remove(IEventSubscription<TEvent> subscription)` | 移除订阅（切片 D 实现） |
| | `int Count { get; }` | 返回订阅数量 |
| | `void InvokeAll(in TEvent @event)` | 按注册顺序调用所有订阅，聚合异常 |
| `CqrsBus.Publish<TEvent>` | `void Publish<TEvent>(in TEvent @event) where TEvent : struct, IEvent` | 委托给 EventSubscriptionRegistry.InvokeAll |

**数据契约：**

```csharp
internal sealed class EventSubscriptionRegistry<TEvent>
    where TEvent : struct, IEvent
{
    private readonly FastList<IEventSubscription<TEvent>> _subscriptions = new();
    
    public void Add(IEventSubscription<TEvent> subscription)
    {
        _subscriptions.Add(subscription);
    }
    
    public int Count => _subscriptions.Count;
    
    public void InvokeAll(in TEvent @event)
    {
        var count = _subscriptions.Count;
        if (count == 0) return;
        
        List<Exception> exceptions = null;
        for (var i = 0; i < count; i++)
        {
            try
            {
                _subscriptions[i].Invoke(in @event);
            }
            catch (Exception ex)
            {
                exceptions ??= new List<Exception>();
                exceptions.Add(ex);
            }
        }
        
        if (exceptions != null)
        {
            throw new AggregateException(exceptions);
        }
    }
}
```

**行为契约：**

**Publish 执行流程：**
1. 查找 TEvent 对应的 EventSubscriptionRegistry
2. 如果 registry 不存在或 Count == 0，直接返回（no-op）
3. 按注册顺序迭代 List<IEventSubscription<TEvent>>
4. 每个订阅调用 Invoke(in @event)，捕获异常但不中断
5. 如果有任何异常，最后抛出 AggregateException

**验收标准：**

- [ ] EventSubscriptionRegistry 存储混合订阅类型 (HandlerSubscription + DelegateSubscription)
- [ ] Publish 按注册顺序调用所有订阅
- [ ] 注册顺序: handler1, delegate1, handler2 → 调用顺序相同
- [ ] 一个订阅抛异常 → 其余订阅仍被调用
- [ ] 多个订阅抛异常 → AggregateException.InnerExceptions 包含所有异常
- [ ] 零订阅 → Publish 返回无错误
- [ ] Publish 热路径无新分配（异常聚合 List 除外，仅在有异常时分配）

**回归风险评估：**

- **影响范围：** 现有多订阅 Event 场景
- **风险等级：** 中
- **缓解措施：**
  - 保持 Publish 签名和外部行为不变
  - 运行现有 Event Handler 测试套件
  - 添加对比测试：旧实现 vs 新实现，验证行为一致性
  - 特别测试异常聚合场景：确保与现有行为匹配

---

### 切片 D: Unregister 支持

**依赖：** 切片 C (需要 EventSubscriptionRegistry.Remove 实现)  
**风险等级：** 低  
**涉及文件：** `Abstractions/ICqrsBootstrap.cs`, `Core/CqrsBootstrap.cs`, `Core/EventSubscriptions/EventSubscriptionRegistry.cs`

**内容：** 添加 Unsubscribe<TEvent> 方法重载（class handler 和 delegate 各一个），实现通过引用相等性匹配并移除订阅。遵循 Freeze 语义：Freeze 后禁止 Unsubscribe。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `ICqrsBootstrap.Unsubscribe<TEvent>` (handler) | `void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : struct, IEvent` | 移除匹配的 class handler 订阅 |
| `ICqrsBootstrap.Unsubscribe<TEvent>` (delegate) | `void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IEvent` | 移除匹配的 delegate 订阅 |
| `EventSubscriptionRegistry.Remove` | `bool Remove(IEventSubscription<TEvent> subscription)` | 从 List 中移除首个匹配项，返回是否找到 |

**数据契约：**

```csharp
// EventSubscriptionRegistry 扩展
public bool Remove(IEventSubscription<TEvent> subscription)
{
    return _subscriptions.Remove(subscription);
}

// CqrsBootstrap 实现
public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
    where TEvent : struct, IEvent
{
    lock (_registrationGate)
    {
        ThrowIfFrozen();
        
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        
        var eventType = typeof(TEvent);
        if (!_eventRegistries.TryGetValue(eventType, out var registry))
            return; // 无订阅，直接返回
        
        var typedRegistry = (EventSubscriptionRegistry<TEvent>)registry;
        // 查找并移除匹配的 HandlerSubscription
        // (需要遍历 List，匹配 HandlerSubscription.Handler == handler)
    }
}

public void Unsubscribe<TEvent>(Action<TEvent> handler)
    where TEvent : struct, IEvent
{
    // 类似逻辑，匹配 DelegateSubscription.Handler == handler
}
```

**行为契约：**

**匹配规则：**
- Class handler: 引用相等性 - `HandlerSubscription.Handler == 传入的 handler 实例`
- Delegate: 委托引用相等性 - `DelegateSubscription.Handler == 传入的 delegate 实例`（注意：委托相等性由 C# 定义，同一方法引用可能不相等）

**生命周期规则：**
- Freeze 前: Unsubscribe 允许
- Freeze 后: Unsubscribe 抛出 RegistryFrozenException

**验收标准：**

- [ ] Unsubscribe<TEvent>(IEventHandler<TEvent>) 添加到 ICqrsBootstrap
- [ ] Unsubscribe<TEvent>(Action<TEvent>) 添加到 ICqrsBootstrap
- [ ] Unsubscribe class handler → 该 handler 不再被 Publish 调用
- [ ] Unsubscribe delegate (精确引用) → 该 delegate 不再被调用
- [ ] Unsubscribe 不存在的订阅 → 无错误，幂等操作
- [ ] 多次注册同一 handler，Unsubscribe 仅移除首个匹配
- [ ] Freeze 前 Unsubscribe → 成功
- [ ] Freeze 后 Unsubscribe → 抛出 RegistryFrozenException

**回归风险评估：**

- **影响范围：** 无 (新增 API)
- **风险等级：** 低
- **缓解措施：** N/A

---

## 切片依赖图

```
切片 A (订阅抽象 & Class Handler)
  ├── 切片 B (静态委托 & 闭包检测) ← 高风险，优先验证
  │     └── 切片 C (统一 Dispatch) ← 中风险
  │           └── 切片 D (Unregister) ← 低风险
```

**实施顺序：** A → B → C → D (严格线性依赖)

**风险优先级：** 切片 B 的闭包检测是核心 0GC 保证，虽然依赖切片 A，但应在 A 完成后立即实施并充分测试。

---

## 关键接口

### 切片 A 暴露给切片 B/C

```csharp
internal interface IEventSubscription<TEvent>
    where TEvent : struct, IEvent
{
    void Invoke(in TEvent @event);
    SubscriptionType Type { get; }
}
```

### 切片 C 暴露给 CqrsBus

```csharp
internal sealed class EventSubscriptionRegistry<TEvent>
{
    public void Add(IEventSubscription<TEvent> subscription);
    public void InvokeAll(in TEvent @event);
    public int Count { get; }
}
```

### 对外 API (ICqrsBootstrap)

```csharp
// 切片 A (现有)
void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : struct, IEvent;

// 切片 B (新增)
void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IEvent;

// 切片 D (新增)
void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : struct, IEvent;
void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IEvent;
```

---

## 回归风险评估

| 影响范围 | 风险等级 | 缓解措施 |
|---------|---------|----------|
| 现有 Event Handler 注册 | 低 | API 签名不变，内部重构；运行现有测试 |
| 现有 Publish 行为 | 中 | 保持调用顺序和异常处理语义；添加对比测试 |
| 多订阅场景 | 中 | 验证混合订阅（handler + delegate）的调用顺序 |
| 性能敏感路径 | 低 | Event 非 Command 级热路径；虚调用开销可接受；0GC 保证通过测试验证 |
| Freeze 语义 | 低 | 继承现有 Freeze 逻辑，Unsubscribe 遵循相同规则 |

**整体风险评估：** 中低

**关键验证点：**
1. 切片 B 闭包检测的准确性 - 通过单元测试覆盖各种委托类型
2. 切片 C 异常聚合的正确性 - 通过测试验证所有订阅都被调用
3. 0GC 保证 - 通过性能测试（类似 FRD #4 中提到的验证）确认无分配

---

## 未决问题继承

来自 FRD 的未决问题，需在 implement 阶段或后续迭代解决：

| # | 问题 | 建议解决阶段 | 设计影响 |
|---|------|-------------|----------|
| 1 | 静态委托访问外部服务：服务定位器 vs 强制 Class Handler？ | implement 前与用户确认 | 如允许服务定位器，需在文档中明确说明模式 |
| 2 | 是否支持条件订阅？ | 未来扩展 | 当前设计不支持，需扩展 IEventSubscription 接口 |
| 3 | 是否支持订阅优先级？ | 未来扩展 | 当前设计不支持，需在 EventSubscriptionRegistry 中实现排序 |

**当前设计立场：** 不支持条件订阅和优先级，保持简洁性。如未来需要，可通过扩展 IEventSubscription 接口添加 `bool ShouldInvoke(in TEvent)` 或在 Add 时指定优先级。
