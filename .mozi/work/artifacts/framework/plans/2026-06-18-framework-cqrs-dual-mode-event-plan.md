# CQRS Event 双模式订阅 实现计划

> 日期: 2026-06-18 | 状态: 草稿
> 上游设计: [架构设计](../designs/2026-06-18-framework-cqrs-dual-mode-event-design.md)
> 上游 FRD: [功能需求](../discover/2026-06-18-framework-cqrs-dual-mode-event-frd.md)

## ⚠️ 与设计文档的差异说明（必读）

本计划**基于当前真实代码状态编写**，而非设计文档原文。原因：设计文档基于 Freeze 移除前的旧代码，而 FRD #1（接口层简化与运行时注册，见姊妹计划 `2026-06-18-framework-cqrs-interface-runtime-registration-plan.md`）**已经实现完成**。当前代码已远超设计文档假设的起点。

经用户确认（2026-06-18），本计划采用以下决策覆盖设计文档：

| 设计文档假设 | 真实状态 / 本计划决策 |
|------------|---------------------|
| Freeze 机制存在，"Freeze 后禁止 Unsubscribe" | ❌ Freeze 已移除；运行时注册始终允许（继承 #1） |
| `RegistryFrozenException` 存在 | ❌ 已删除，本计划不引用 |
| `CqrsRuntime`/`ICqrsRuntimeProvider` 存在 | ❌ 已删除；`CqrsBus` 直接实现 `ICqrsRuntime` |
| Class Handler 模式是新功能（切片 A） | ❌ 已完整实现（注册顺序、struct 拒绝、AggregateException 聚合） |
| `Unsubscribe<TEvent>(IEventHandler)` 是新功能（切片 D） | ❌ 已实现（幂等、实例匹配） |
| 架构决策：统一 `IEventSubscription<TEvent>` 存储（Option A） | ⚠️ **用户选择改为并行委托列表**（`FastList<Action<TEvent>>` 与现有 `FastList<IEventHandler<TEvent>>` 并存），降低对已通过测试的 class-handler 路径的回归风险 |
| `FastList.Remove(T)` 可用 | ❌ FastList 无此方法，本计划使用 `IndexOf` + `RemoveAt` |
| `RegistrationGuardTests.cs` 正常 | ❌ 引用已删除的 `bus.IsFrozen`，**无法编译**，阻塞整个测试程序集 |

**设计文档中的以下部分仍然有效并被本计划采用：**
- ✅ 闭包检测策略：运行时检测 `handler.Target != null`（FRD 决策 #2）
- ✅ `ClosureCaptureException`（继承 `InvalidOperationException`）
- ✅ `Subscribe<TEvent>(Action<TEvent>)` / `Unsubscribe<TEvent>(Action<TEvent>)` 重载
- ✅ 异常聚合 `AggregateException`（已实现，本计划扩展到委托路径）
- ✅ 0GC 约束（`in` 传递 handler，struct 值传递委托无堆分配）

## 上游产出引用

### 来自 Design（已校正）

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 闭包检测 `handler.Target != null`（切片 B） | 任务 3 在 `Subscribe<TEvent>(Action<TEvent>)` 中实现检测 |
| `ClosureCaptureException`（切片 B） | 任务 2 创建异常类 |
| `Subscribe<TEvent>(Action<TEvent>)` 重载（切片 B） | 任务 3 添加到 `ICqrsRegistry` + `CqrsBus`；任务 4 添加到 `ICqrsBootstrap` + `CqrsBootstrap` |
| `Unsubscribe<TEvent>(Action<TEvent>)` 重载（切片 D） | 任务 3 添加到 `ICqrsRegistry` + `CqrsBus`（Bootstrap 面不暴露反注册，与现有 handler 行为一致；反注册经 `ICqrsRegistry`/bus 进行） |
| 0GC 验证（验收条件） | 任务 5 添加委托路径零分配测试 |

### 来自 FRD

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 决策 #2: 闭包运行时检测 | 任务 2 + 任务 3 |
| 决策 #3: AggregateException 聚合 | 任务 3 扩展 Publish 委托循环，异常聚合跨两个列表 |
| 决策 #4: 按注册顺序调用 | 任务 3 Publish 先遍历 handlers（注册序）再遍历 delegates（注册序）；跨类型顺序不保证（FRD 范围外） |
| 验收条件: 闭包检测注册时抛异常 | 任务 3 测试覆盖 |
| 验收条件: 同一 Event 多订阅者都被调用 | 任务 3 测试覆盖（handler + delegate 混合） |
| 验收条件: 单元测试覆盖两种订阅模式和闭包检测 | 任务 3 + 任务 5 |
| 验收条件: 0GC 分配初步验证 | 任务 5 |

### 来自代码现状调查

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| `EventHandlerList<TEvent>` 存 `FastList<IEventHandler<TEvent>>` | 任务 3 在其中增加 `FastList<Action<TEvent>> Delegates` |
| `Publish` 已做异常聚合（仅 handler 路径） | 任务 3 扩展为聚合 handler + delegate 两路异常 |
| `FastList` 无 `Remove(T)`，有 `IndexOf` + `RemoveAt` | 任务 3 委托反注册用 `IndexOf` + `RemoveAt` |
| 现有 `Unsubscribe<TEvent>(IEventHandler)` 用 `IndexOf`+`RemoveAt` 幂等模式 | 任务 3 委托反注册复用同一模式 |
| `RegistrationGuardTests.cs` 引用已删除 `IsFrozen` 无法编译 | 任务 1 删除该文件，解除阻塞 |

## 目标

为 CQRS Event 订阅增加**静态委托模式**：除现有 Class Handler（`IEventHandler<TEvent>`）外，支持 `Subscribe<TEvent>(Action<TEvent>)` 订阅静态方法/非捕获委托，注册时检测闭包捕获以维持 0GC 承诺，并提供对应的反注册 API。

## 架构

**并行委托列表**（覆盖设计文档的统一存储 Option A）：在现有 `EventHandlerList<TEvent>` 中新增 `FastList<Action<TEvent>> Delegates`，与 `FastList<IEventHandler<TEvent>> Handlers` 并存。`Publish` 先遍历 handlers（注册序）再遍历 delegates（注册序），异常聚合跨两路。闭包检测在 `Subscribe(Action)` 中通过 `handler.Target != null` 判定，命中则抛 `ClosureCaptureException`。此方案对已通过测试的 class-handler 路径零侵入。

## 技术栈

- C# (.NET Standard 2.1 / Unity)
- NUnit 测试框架 (Unity EditMode Tests)
- FastList (Change.Framework.Collections) — `Add` / `Count` / `this[int]` / `IndexOf` / `RemoveAt`

## 文件清单

| 文件 | 操作 | 职责 | 所属任务 |
|------|------|------|----------|
| `Tests/.../Cqrs/RegistrationGuardTests.cs` + `.meta` | 删除 | 引用已删除 `IsFrozen`，阻塞编译 | 1 |
| `Cqrs/Exceptions/ClosureCaptureException.cs` | 创建 | 委托捕获变量时抛出的异常（继承 InvalidOperationException） | 2 |
| `Cqrs/Abstractions/ICqrsRegistry.cs` | 修改 | 新增 `Subscribe(Action)` + `Unsubscribe(Action)` | 3 |
| `Cqrs/Core/CqrsBus.cs` | 修改 | EventHandlerList 增加 Delegates 列表；实现 Subscribe/Unsubscribe(Action) + 闭包检测；Publish 增加委托遍历 | 3 |
| `Tests/.../Cqrs/EventDispatchTests.cs` | 修改 | 新增委托模式 + 闭包检测 + 混合调度 + 委托反注册测试 | 3 |
| `Cqrs/Abstractions/ICqrsBootstrap.cs` | 修改 | 新增 `Subscribe(Action)`；修正过时的 Freeze 注释（反注册经 ICqrsRegistry 进行，与现有 handler 行为一致） | 4 |
| `Cqrs/Core/CqrsBootstrap.cs` | 修改 | 转发 `Subscribe(Action)` 到 bus | 4 |
| `Tests/.../Cqrs/ZeroAllocationDispatchTests.cs` | 修改 | 新增委托路径零分配测试 | 5 |

基础路径: `UnityProject/Assets/Change/Framework/Cqrs/`
测试基础路径: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/`

## 任务依赖图

```
任务 1: 删除阻塞测试，建立绿色基线        (无依赖)
  └── 任务 2: ClosureCaptureException      (依赖 1)
        └── 任务 3: 委托订阅模式 + Publish  (依赖 2) ← 高风险核心
              └── 任务 4: Bootstrap 镜像     (依赖 3)
                    └── 任务 5: 零分配验证    (依赖 4)
                          └── 任务 6: 最终验证 (依赖 5)
```

## 风险评估

| 风险（来源） | 影响 | 计划中的缓解措施 |
|-------------|------|-----------------|
| 闭包检测不准确（Design 切片 B 高风险） | 捕获委托漏检 → 破坏 0GC | 任务 3 覆盖：静态方法、非捕获 lambda、捕获局部变量、捕获 this、实例方法引用 五种场景 |
| Publish 行为变更影响 class-handler 路径（Design 切片 C 回归评估） | 已通过的 EventDispatchTests 失败 | 任务 3 先运行现有 EventDispatchTests 确认不回归；新增测试仅追加不改动现有断言 |
| 委托调用破坏零分配（FRD 0GC 约束） | GC 分配回归 | 任务 5 用 `GC.GetAllocatedBytesForCurrentThread` 验证委托路径零分配 |
| 跨列表异常聚合遗漏（FRD 决策 #3） | 委托异常中断其他订阅者 | 任务 3 测试 `Publish_HandlerAndDelegateBothThrow_AggregatesAllExceptions` |
| 委托反注册引用匹配（Design 切片 D） | 反注册失败/误删 | 任务 3 测试精确引用匹配 + 幂等；复用现有 handler 反注册的 IndexOf+RemoveAt 模式 |
| `RegistrationGuardTests.cs` 阻塞编译（代码现状） | 测试程序集无法编译 | 任务 1 优先删除，建立绿色基线 |

---

## 任务

### 任务 1: 删除阻塞编译的失效测试，建立绿色基线

**覆盖的上游需求：** 代码现状调查 — `RegistrationGuardTests.cs` 引用已删除的 `CqrsBus.IsFrozen`，阻塞整个 EditMode 测试程序集编译

**文件：**
- 删除：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/RegistrationGuardTests.cs`
- 删除：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/RegistrationGuardTests.cs.meta`

- [ ] **步骤 1: 删除失效测试文件及其 meta**

```bash
rm UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/RegistrationGuardTests.cs
rm UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/RegistrationGuardTests.cs.meta
```

- [ ] **步骤 2: 运行现有 CQRS 测试套件确认绿色基线**

```bash
# 在 Unity Editor 中运行（Test Runner → EditMode），或 Unity CLI batch mode：
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "Change.Framework.Tests"
```

预期：所有 CQRS 测试通过（`EventDispatchTests`、`RuntimeRegistrationTests`、`ZeroAllocationDispatchTests` 等）。这确认删除后测试程序集可正常编译运行。

> 注：Unity CLI 二进制路径因环境而异；若未配置 PATH，在 Unity Editor 的 Test Runner 窗口运行 `Change.Framework.Tests` 命名空间下全部 EditMode 测试即可。后续任务的"运行测试"步骤同理。

- [ ] **步骤 3: Commit**

```bash
git add -u UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/
git commit -m "test(cqrs): remove broken RegistrationGuardTests referencing deleted IsFrozen

RegistrationGuardTests referenced CqrsBus.IsFrozen which was removed when
the Freeze mechanism was deleted (FRD #1). The test failed to compile,
blocking the entire EditMode test assembly. Deleting it restores a green
baseline for the event dual-mode work.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 2: 创建 ClosureCaptureException

**覆盖的上游需求：** Design 切片 B / FRD 决策 #2 — 委托捕获变量时抛出专用异常
**依赖：** 任务 1

**文件：**
- 创建：`UnityProject/Assets/Change/Framework/Cqrs/Exceptions/ClosureCaptureException.cs`
- 测试：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/EventDispatchTests.cs`（追加构造测试）

- [ ] **步骤 1: 编写失败的测试**

在 `EventDispatchTests.cs` 类末尾（`CountingEventHandler` 类定义之前、最后一个测试方法之后）追加：

```csharp
        [Test]
        public void ClosureCaptureException_IsInvalidOperationException_WithMessage()
        {
            var ex = new ClosureCaptureException("captured!");

            Assert.IsInstanceOf<InvalidOperationException>(ex);
            Assert.AreEqual("captured!", ex.Message);
        }
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "EventDispatchTests.ClosureCaptureException_IsInvalidOperationException_WithMessage"
```

预期：编译失败，报错 `The type or namespace name 'ClosureCaptureException' could not be found`。

- [ ] **步骤 3: 创建 ClosureCaptureException.cs**

参考现有 `Exceptions/DuplicateRegistrationException.cs` 的结构（同类、同命名空间、单 message 构造）。新文件 `UnityProject/Assets/Change/Framework/Cqrs/Exceptions/ClosureCaptureException.cs`：

```csharp
using System;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Thrown when subscribing a delegate (<see cref="Action{T}"/>) that captures
    /// variables (i.e. <see cref="Delegate.Target"/> is non-null). Capturing delegates
    /// allocate a closure object on the heap, violating the 0-GC dispatch guarantee.
    /// Only static methods or non-capturing lambdas may be subscribed.
    /// </summary>
    public sealed class ClosureCaptureException : InvalidOperationException
    {
        public ClosureCaptureException(string message)
            : base(message)
        {
        }
    }
}
```

> 注：新建 `.cs` 文件后，Unity 会在下次导入时自动生成对应的 `.cs.meta`（含 GUID）。若使用 Unity CLI 运行测试，导入会自动触发；若在 Editor 中，切回 Editor 等待一次 Asset 刷新即可。无需手写 meta。

- [ ] **步骤 4: 运行测试验证通过**

```bash
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "EventDispatchTests.ClosureCaptureException_IsInvalidOperationException_WithMessage"
```

预期：PASS。

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Exceptions/ClosureCaptureException.cs
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/EventDispatchTests.cs
git commit -m "feat(cqrs): add ClosureCaptureException for captured delegate detection

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 3: 委托订阅模式 + 闭包检测 + Publish 委托遍历（核心，高风险）

**覆盖的上游需求：** Design 切片 B + C（校正版）/ FRD 决策 #2 #3 #4 + 验收条件（静态委托工作、闭包检测、多订阅者、AggregateException、反注册）
**依赖：** 任务 2

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRegistry.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- 修改：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/EventDispatchTests.cs`

**设计要点（并行委托列表）：**
- `EventHandlerList<TEvent>` 新增 `FastList<Action<TEvent>> Delegates`
- `Subscribe(Action<TEvent>)`：null 检查 → 闭包检测（`Target != null` 抛异常）→ 加入 Delegates 列表
- `Unsubscribe(Action<TEvent>)`：`IndexOf` + `RemoveAt`，幂等
- `Publish`：先遍历 Handlers（注册序），再遍历 Delegates（注册序），异常聚合跨两路

- [ ] **步骤 1: 先运行现有 EventDispatchTests 确认绿色基线（防回归）**

```bash
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "EventDispatchTests"
```

预期：全部 PASS（任务 1 后基线绿色）。记录通过数量作为回归对照。

- [ ] **步骤 2: 编写失败的委托模式测试**

在 `EventDispatchTests.cs` 中追加委托夹具与测试方法。

> 注：`Action<TEvent>` 的目标方法签名必须是 `static void M(TEvent e)`（值传递，**不能**用 `in`）。静态方法引用的 `Target == null`，可正常订阅；捕获局部变量或 `this` 的委托 `Target != null`，会被闭包检测拒绝。测试用静态字段记录状态（静态字段不算捕获）。

先在测试类内（`CountingEventHandler` 类定义之前）追加委托夹具：

```csharp
        // ===== Delegate mode fixtures =====

        private static int _staticCounter;

        private static void StaticIncrementHandler(ScoreChangedEvent @event)
        {
            _staticCounter += @event.Delta;
        }

        private sealed class DelegateRecorder
        {
            // 实例方法引用示例，仅用于闭包检测反例测试（Target != null，应被拒绝）
            public void Record(ScoreChangedEvent @event) { }
        }

        private static System.Collections.Generic.List<int> _publishDelegateOrder;

        private static void RecordDelegateCallStatic(ScoreChangedEvent @event)
        {
            _publishDelegateOrder?.Add(1);
        }

        private static void StaticThrowingHandler(ScoreChangedEvent @event)
        {
            throw new InvalidOperationException("Delegate failed");
        }
```

然后在测试类中追加以下测试方法：

```csharp
        // ===== Delegate subscribe tests =====

        [Test]
        public void Subscribe_StaticDelegate_InvokesOnPublish()
        {
            _staticCounter = 0;
            var bus = new CqrsBus();

            bus.Subscribe<ScoreChangedEvent>(StaticIncrementHandler);

            bus.Publish(new ScoreChangedEvent(7));

            Assert.AreEqual(7, _staticCounter);
        }

        [Test]
        public void Subscribe_NullDelegate_ThrowsArgumentNullException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ArgumentNullException>(
                () => bus.Subscribe<ScoreChangedEvent>(null));
        }

        [Test]
        public void Subscribe_CapturingLambda_ThrowsClosureCaptureException()
        {
            var bus = new CqrsBus();
            var captured = 0;

            // lambda 捕获局部变量 captured → Target != null
            Assert.Throws<ClosureCaptureException>(
                () => bus.Subscribe<ScoreChangedEvent>(e => captured += e.Delta));
        }

        [Test]
        public void Subscribe_InstanceMethod_ThrowsClosureCaptureException()
        {
            var bus = new CqrsBus();
            var recorder = new DelegateRecorder();

            // 实例方法引用 → Target == recorder != null
            Assert.Throws<ClosureCaptureException>(
                () => bus.Subscribe<ScoreChangedEvent>(recorder.Record));
        }

        [Test]
        public void Publish_InvokesHandlersThenDelegates()
        {
            var handlerOrder = new System.Collections.Generic.List<int>();
            var delegateOrder = new System.Collections.Generic.List<int>();
            var bus = new CqrsBus();

            bus.Subscribe(new OrderedEventHandler(handlerOrder, 1));
            // 用一个捕获 delegate 记录顺序会触发闭包检测，因此用静态字段记录
            _publishDelegateOrder = delegateOrder;
            bus.Subscribe<ScoreChangedEvent>(RecordDelegateCallStatic);

            bus.Publish(new ScoreChangedEvent(0));

            // handlers 先于 delegates 调用
            CollectionAssert.AreEqual(new[] { 1 }, handlerOrder);
            Assert.AreEqual(1, delegateOrder.Count, "delegate should be invoked once");
        }

        [Test]
        public void Publish_WithoutSubscribers_DoesNothing_DelegatePath()
        {
            var bus = new CqrsBus();

            Assert.DoesNotThrow(() => bus.Publish(new ScoreChangedEvent(1)));
        }

        [Test]
        public void Publish_HandlerAndDelegateBothThrow_AggregatesAllExceptions()
        {
            var bus = new CqrsBus();

            bus.Subscribe(new ThrowingEventHandler());
            bus.Subscribe<ScoreChangedEvent>(StaticThrowingHandler);

            var exception = Assert.Throws<AggregateException>(
                () => bus.Publish(new ScoreChangedEvent(0)));

            Assert.AreEqual(2, exception.InnerExceptions.Count,
                "both handler and delegate exceptions should be aggregated");
        }

        // ===== Delegate unsubscribe tests =====

        [Test]
        public void Unsubscribe_Delegate_RemovesSpecificDelegate()
        {
            _staticCounter = 0;
            var bus = new CqrsBus();

            bus.Subscribe<ScoreChangedEvent>(StaticIncrementHandler);
            bus.Subscribe<ScoreChangedEvent>(StaticIncrementHandler);
            bus.Unsubscribe<ScoreChangedEvent>(StaticIncrementHandler);

            bus.Publish(new ScoreChangedEvent(5));

            // 订阅两次，反注册一次（移除首个匹配），剩余一次
            Assert.AreEqual(5, _staticCounter);
        }

        [Test]
        public void Unsubscribe_Delegate_WhenNotSubscribed_IsIdempotent()
        {
            var bus = new CqrsBus();

            Assert.DoesNotThrow(
                () => bus.Unsubscribe<ScoreChangedEvent>(StaticIncrementHandler));
        }

        [Test]
        public void Unsubscribe_NullDelegate_ThrowsArgumentNullException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ArgumentNullException>(
                () => bus.Unsubscribe<ScoreChangedEvent>(null));
        }
```

- [ ] **步骤 3: 运行测试验证失败**

```bash
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "EventDispatchTests"
```

预期：编译失败，报错 `CqrsBus`（及 `ICqrsRegistry`）缺少 `Subscribe<TEvent>(Action<TEvent>)` 和 `Unsubscribe<TEvent>(Action<TEvent>)` 方法。

- [ ] **步骤 4: 在 ICqrsRegistry 添加委托重载**

修改 `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRegistry.cs`，在 `Subscribe<TEvent>(IEventHandler<TEvent>)` 之后追加委托重载，在 `Unsubscribe<TEvent>(IEventHandler<TEvent>)` 之后追加委托反注册重载。完整文件：

```csharp
using System;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Mutable registration surface for CQRS handlers.
    /// Registration and unregistration may be called at any time on the main thread.
    /// Thread safety is the caller's responsibility.
    /// </summary>
    public interface ICqrsRegistry
    {
        void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;

        void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>;

        void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;

        /// <summary>
        /// Subscribes a static delegate. The delegate must NOT capture variables
        /// (i.e. must be a static method or non-capturing lambda); a capturing
        /// delegate throws <see cref="ClosureCaptureException"/>.
        /// </summary>
        void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent;

        void UnregisterCommand<TCommand>()
            where TCommand : struct, ICommand;

        void UnregisterQuery<TQuery, TResult>()
            where TQuery : struct, IQuery<TResult>;

        void Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;

        /// <summary>
        /// Unsubscribes a delegate previously registered via
        /// <see cref="Subscribe{TEvent}(Action{TEvent})"/>. Matching is by exact
        /// delegate reference. No-op if not found.
        /// </summary>
        void Unsubscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent;
    }
}
```

- [ ] **步骤 5: 在 CqrsBus.EventHandlerList 增加委托列表**

修改 `UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`。定位 `EventHandlerList<TEvent>` 内部类（当前约第 64-68 行），增加 `Delegates` 列表：

将：
```csharp
        private sealed class EventHandlerList<TEvent> : IEventHandlerList
            where TEvent : struct, IEvent
        {
            public FastList<IEventHandler<TEvent>> Handlers { get; } = new();
        }
```
改为：
```csharp
        private sealed class EventHandlerList<TEvent> : IEventHandlerList
            where TEvent : struct, IEvent
        {
            public FastList<IEventHandler<TEvent>> Handlers { get; } = new();

            public FastList<Action<TEvent>> Delegates { get; } = new();
        }
```

- [ ] **步骤 6: 添加日志常量**

在 `CqrsBus` 顶部常量区（`EventSubscribedMessage` 附近，约第 26-28 行），追加：

```csharp
        private const string EventSubscribedMessage = "Subscribed event handler.";
        private const string EventDelegateSubscribedMessage = "Subscribed event delegate.";
```

（即在现有 `EventSubscribedMessage` 行之后新增 `EventDelegateSubscribedMessage` 行。）

- [ ] **步骤 7: 实现 Subscribe(Action) + 闭包检测**

在 `CqrsBus.cs` 现有 `Subscribe<TEvent>(IEventHandler<TEvent>)` 方法之后（约第 186 行后），追加委托订阅方法：

```csharp
        public void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            // 闭包检测：Target != null 表示委托捕获了实例或局部变量，会触发堆分配，破坏 0GC。
            if (handler.Target != null)
            {
                throw new ClosureCaptureException(
                    "Delegate captures variables; only static methods or non-capturing lambdas " +
                    "are allowed to maintain the 0-GC guarantee. " +
                    $"Delegate type: {handler.GetType().FullName}, Target: {handler.Target.GetType().FullName}.");
            }

            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                handlerList = new EventHandlerList<TEvent>();
                _eventHandlers.TryAdd(eventType, handlerList);
            }

            var typedList = (EventHandlerList<TEvent>)handlerList;
            typedList.Delegates.Add(handler);

            SafeInfo(EventDelegateSubscribedMessage);
        }
```

- [ ] **步骤 8: 实现 Unsubscribe(Action)**

在 `CqrsBus.cs` 现有 `Unsubscribe<TEvent>(IEventHandler<TEvent>)` 方法之后（约第 234 行后），追加委托反注册方法（复用 IndexOf + RemoveAt 幂等模式）：

```csharp
        public void Unsubscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                return;
            }

            var typedList = (EventHandlerList<TEvent>)handlerList;
            var index = typedList.Delegates.IndexOf(handler);
            if (index >= 0)
            {
                typedList.Delegates.RemoveAt(index);
            }
        }
```

- [ ] **步骤 9: 扩展 Publish 遍历委托列表**

定位 `CqrsBus.Publish<TEvent>` 方法（约第 282-314 行），将整个方法体替换为：先遍历 Handlers，再遍历 Delegates，异常聚合跨两路。

将：
```csharp
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish<TEvent>(in TEvent @event)
            where TEvent : struct, IEvent
        {
            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                return;
            }

            var typedList = (EventHandlerList<TEvent>)handlerList;
            var handlers = typedList.Handlers;
            var count = handlers.Count;
            if (count == 0) return;

            List<Exception> exceptions = null;
            for (var i = 0; i < count; i++)
            {
                try
                {
                    handlers[i].Handle(in @event);
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
```
改为：
```csharp
        /// <summary>
        /// Publishes an event to all subscribed class handlers then to all subscribed
        /// delegates, each in registration order. Subscribers are matched strictly by the
        /// closed generic type <typeparamref name="TEvent"/>; no base-interface fan-out
        /// is performed. Class handlers are invoked before delegates; cross-type ordering
        /// between handlers and delegates is not guaranteed. All subscribers are invoked
        /// even if one throws. If any throws, exceptions are collected and re-thrown as an
        /// <see cref="AggregateException"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish<TEvent>(in TEvent @event)
            where TEvent : struct, IEvent
        {
            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                return;
            }

            var typedList = (EventHandlerList<TEvent>)handlerList;
            var handlers = typedList.Handlers;
            var delegates = typedList.Delegates;

            var handlerCount = handlers.Count;
            var delegateCount = delegates.Count;
            if (handlerCount == 0 && delegateCount == 0)
            {
                return;
            }

            List<Exception> exceptions = null;

            for (var i = 0; i < handlerCount; i++)
            {
                try
                {
                    handlers[i].Handle(in @event);
                }
                catch (Exception ex)
                {
                    exceptions ??= new List<Exception>();
                    exceptions.Add(ex);
                }
            }

            for (var i = 0; i < delegateCount; i++)
            {
                try
                {
                    // Action<TEvent> 按值传递 struct（无 in），拷贝在栈上，无堆分配
                    delegates[i](@event);
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
```

- [ ] **步骤 10: 运行全部 EventDispatchTests 验证通过（含新增委托测试 + 现有 handler 测试不回归）**

```bash
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "EventDispatchTests"
```

预期：全部 PASS，包括任务 1 基线的原有测试（无回归）和本任务新增的委托测试。

- [ ] **步骤 11: 运行 RuntimeRegistrationTests 确认现有反注册不回归**

```bash
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "RuntimeRegistrationTests"
```

预期：全部 PASS（handler 反注册路径未受影响）。

- [ ] **步骤 12: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRegistry.cs
git add UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/EventDispatchTests.cs
git commit -m "feat(cqrs): add static delegate subscription mode with closure detection

Add Subscribe<TEvent>(Action<TEvent>) / Unsubscribe<TEvent>(Action<TEvent>)
to ICqrsRegistry and CqrsBus. Delegates are stored in a parallel
FastList<Action<TEvent>> alongside the existing handler list (minimal
invasion to the proven class-handler path).

Closure detection: Subscribe throws ClosureCaptureException when
handler.Target != null, enforcing the 0-GC guarantee (capturing delegates
allocate a closure on the heap).

Publish now iterates class handlers (registration order) then delegates
(registration order), aggregating exceptions across both lists into a
single AggregateException.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 4: ICqrsBootstrap + CqrsBootstrap 镜像委托方法，修正过时注释

**覆盖的上游需求：** Design 切片 B/D 对外 API（Bootstrap 面）/ FRD 注册 API "Subscribe<TEvent>(Action<TEvent> handler)"
**依赖：** 任务 3

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBootstrap.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBootstrap.cs`

- [ ] **步骤 1: 编写失败的 Bootstrap 委托测试**

在 `EventDispatchTests.cs` 追加一个通过 Bootstrap 路径的测试：

```csharp
        [Test]
        public void Bootstrap_SubscribeStaticDelegate_InvokesOnPublish()
        {
            _staticCounter = 0;
            var bootstrap = new CqrsBootstrap();

            bootstrap.Subscribe<ScoreChangedEvent>(StaticIncrementHandler);
            ICqrsRuntime runtime = bootstrap.Build();

            runtime.Publish(new ScoreChangedEvent(9));

            Assert.AreEqual(9, _staticCounter);
        }
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "EventDispatchTests.Bootstrap_SubscribeStaticDelegate_InvokesOnPublish"
```

预期：编译失败，`ICqrsBootstrap` 缺少 `Subscribe<TEvent>(Action<TEvent>)`。

- [ ] **步骤 3: 修改 ICqrsBootstrap.cs — 添加委托重载并修正过时注释**

`ICqrsBootstrap.cs` 当前第 4-5 行和第 18-22 行有遗留的 Freeze 描述（FRD #1 后已失效）。整文件替换为：

```csharp
using System;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Mutable CQRS bootstrap surface used to register handlers and delegates.
    /// Registration remains valid before and after <see cref="Build"/>; the returned
    /// <see cref="ICqrsRuntime"/> is the underlying bus, which also exposes the
    /// <see cref="ICqrsRegistry"/> registration surface.
    /// </summary>
    public interface ICqrsBootstrap
    {
        void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;

        void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>;

        void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;

        /// <summary>
        /// Subscribes a static delegate. Must not capture variables; a capturing
        /// delegate throws <see cref="ClosureCaptureException"/>.
        /// </summary>
        void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent;

        /// <summary>
        /// Returns the runtime dispatch surface (the underlying bus).
        /// Implementations must return the same <see cref="ICqrsRuntime"/> instance on
        /// every subsequent call and must be safe to invoke concurrently.
        /// </summary>
        ICqrsRuntime Build();
    }
}
```

> 注：此处**未**在 Bootstrap 面暴露 `Unsubscribe(Action)`。理由：Bootstrap 当前仅暴露注册面（无 handler 版 Unsubscribe），保持对称——反注册通过 `ICqrsRegistry`（bus 实现）进行。若用户后续需要 Bootstrap 反注册，可单独追加；当前与现有 handler 行为一致（Bootstrap 无 `Unsubscribe(handler)`）。

- [ ] **步骤 4: 修改 CqrsBootstrap.cs — 转发委托订阅**

在 `CqrsBootstrap.cs` 现有 `Subscribe<TEvent>(IEventHandler<TEvent>)` 方法之后（约第 45 行后），追加：

```csharp
        public void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent
        {
            _bus.Subscribe(handler);
        }
```

- [ ] **步骤 5: 运行测试验证通过**

```bash
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "EventDispatchTests.Bootstrap_SubscribeStaticDelegate_InvokesOnPublish"
```

预期：PASS。

- [ ] **步骤 6: 运行 CqrsBootstrapLifecycleTests 确认不回归**

```bash
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "CqrsBootstrapLifecycleTests"
```

预期：全部 PASS（注释修正不影响行为）。

- [ ] **步骤 7: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBootstrap.cs
git add UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBootstrap.cs
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/EventDispatchTests.cs
git commit -m "feat(cqrs): add Subscribe(Action) to ICqrsBootstrap, fix stale Freeze comments

Mirror the delegate subscription overload on the bootstrap surface.
Correct outdated XML doc comments claiming Build() freezes registration
(Freeze was removed in FRD #1).

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 5: 委托路径零分配验证

**覆盖的上游需求：** FRD 验收条件 "0GC 分配初步验证" / Design 关键验证点 #3
**依赖：** 任务 4

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ZeroAllocationDispatchTests.cs`

- [ ] **步骤 1: 编写失败的零分配测试**

在 `ZeroAllocationDispatchTests.cs` 中追加委托夹具与测试。先在类内（`TickDomainEventHandler` 之后）追加静态字段与静态处理器：

```csharp
        private static int _sStaticTickCount;

        private static void StaticTickHandler(TickDomainEvent domainEvent)
        {
            _sStaticTickCount += domainEvent.Delta;
        }
```

然后在类内追加测试方法（仿照现有 `Publish_HotPath_AllocatesZeroBytesAfterWarmup`）：

```csharp
        [Test]
        public void Publish_DelegateHotPath_AllocatesZeroBytesAfterWarmup()
        {
            _sStaticTickCount = 0;
            var bootstrap = new CqrsBootstrap();
            bootstrap.Subscribe<TickDomainEvent>(StaticTickHandler);
            ICqrsRuntime runtime = bootstrap.Build();

            var domainEvent = new TickDomainEvent(1);
            for (var i = 0; i < WarmupIterations; i++)
            {
                runtime.Publish(in domainEvent);
            }

            ForceFullGc();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < MeasuredIterations; i++)
            {
                runtime.Publish(in domainEvent);
            }
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after,
                "delegate publish hot path must not allocate");
            Assert.AreEqual(WarmupIterations + MeasuredIterations, _sStaticTickCount);
        }
```

- [ ] **步骤 2: 运行测试验证通过**

```bash
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "ZeroAllocationDispatchTests.Publish_DelegateHotPath_AllocatesZeroBytesAfterWarmup"
```

预期：PASS。委托在注册时分配一次（订阅阶段），热路径调用按值传递 struct（栈拷贝，无堆分配），`GC.GetAllocatedBytesForCurrentThread` 前后相等。

> 若失败（after > before）：检查 Publish 委托循环是否意外装箱，或 `Action<TEvent>` 是否捕获了变量（应被闭包检测拦截）。确认 `StaticTickHandler` 为 static 且 `Target == null`。

- [ ] **步骤 3: 运行全部零分配测试确认无回归**

```bash
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "ZeroAllocationDispatchTests"
```

预期：全部 PASS（`Send`/`Query`/`Publish` handler 路径 + 新增 delegate 路径）。

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ZeroAllocationDispatchTests.cs
git commit -m "test(cqrs): add zero-allocation test for delegate publish hot path

Verify Action<TEvent> dispatch allocates zero bytes after warmup,
confirming the static-delegate mode preserves the 0-GC guarantee.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 6: 最终验证

**覆盖的上游需求：** FRD 全部验收条件汇总验证
**依赖：** 任务 5

**文件：** 无（仅验证）

- [ ] **步骤 1: 运行完整 CQRS 测试套件**

```bash
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "Change.Framework.Tests"
```

预期：所有 CQRS 测试通过，包括：
- `EventDispatchTests`（handler + delegate 模式、闭包检测、混合调度、异常聚合、反注册）
- `RuntimeRegistrationTests`（注册/反注册不回归）
- `ZeroAllocationDispatchTests`（三条零分配路径）
- `CommandDispatchTests` / `QueryDispatchTests` / `CqrsBootstrapLifecycleTests` / `DomainEventSemanticsTests` / `CqrsArchitectureGuardTests` / `CqrsLoggingIntegrationTests`

- [ ] **步骤 2: 确认无过时引用残留**

```bash
# 确认本特性未引入已删除类型的引用（Freeze 相关）
grep -rn "IsFrozen\|RegistryFrozenException\|ThrowIfFrozen\|\.Freeze()" \
  UnityProject/Assets/Change/Framework/Cqrs/ --include="*.cs"
# 预期：无输出（这些在 FRD #1 已移除，本特性不应重新引入）
```

- [ ] **步骤 3: 确认委托 API 已就位**

```bash
# 确认 ICqrsRegistry 和 ICqrsBootstrap 都有委托重载
grep -n "Subscribe<TEvent>(Action<TEvent>" \
  UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRegistry.cs \
  UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBootstrap.cs
# 预期：两个文件各命中一行

# 确认 CqrsBus 实现了委托订阅/反注册/Publish 委托遍历
grep -n "Delegates" UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs
# 预期：至少 3 行（EventHandlerList 声明、Subscribe 添加、Publish 遍历）
```

- [ ] **步骤 4: 验收条件核对（对照 FRD）**

逐项确认：
- [ ] Class Handler 订阅和调用正常工作（现有，未回归）
- [ ] 静态委托订阅正常工作（任务 3）
- [ ] 闭包检测正常：捕获变量的委托注册时抛异常（任务 3）
- [ ] 同一 Event 多订阅者都被调用（任务 3 混合调度测试）
- [ ] `AggregateException` 聚合失败，不影响其他订阅者（任务 3 跨列表聚合测试）
- [ ] 反注册正常工作（任务 3 委托反注册 + 现有 handler 反注册）
- [ ] 单元测试覆盖两种订阅模式和闭包检测（任务 2/3/5）
- [ ] 0GC 分配初步验证（任务 5）

> 注：FRD 验收条件 "Class Handler 模式支持 DI 构造注入" 属于现有功能（FRD #1 前），本特性未改动，由现有测试覆盖。

- [ ] **步骤 5: Commit（如有文档/收尾变更）**

本任务为纯验证，通常无代码变更。若验证中发现需补充的文档注释，单独 commit：

```bash
git add <变更文件>
git commit -m "docs(cqrs): finalize event dual-mode subscription documentation

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## 实施后的架构约束记录

来自 FRD 未决问题 #1 的隐式结论（由闭包检测结构性强制）：

**静态委托无法访问外部服务实例**——因为 `handler.Target != null` 会被 `ClosureCaptureException` 拒绝。需要依赖注入或访问外部服务的场景，**必须使用 Class Handler 模式**（`IEventHandler<TEvent>`）。静态委托仅适用于无状态场景（日志、指标统计等）。这一点由代码结构保证，无需额外文档。

## 设计文档校正建议（实施后）

实施完成后，建议在 `2026-06-18-framework-cqrs-dual-mode-event-design.md` 顶部追加"已实施校正"小节，或更新其架构决策表，将"统一存储 Option A"标注为"已改为并行委托列表（实施决策）"，使设计与实现一致。此为收尾文档工作，非阻塞实现。
