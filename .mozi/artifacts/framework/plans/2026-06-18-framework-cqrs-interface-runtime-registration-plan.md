# CQRS 接口层简化与运行时注册 实现计划

> 日期: 2026-06-18 | 状态: 草稿
> 上游设计: [架构设计](../designs/2026-06-18-framework-cqrs-interface-runtime-registration-design.md)
> 上游 FRD: [功能需求](../discover/2026-06-18-framework-cqrs-interface-runtime-registration-frd.md)

## 上游产出引用

### 来自 Design

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 切片 A: IEvent 统一 | 任务 1-3 |
| 切片 B: 架构扁平化 | 任务 4-6 |
| 切片 C: 运行时注册 API | 任务 7-9 |
| 切片 D: 测试套件与文档 | 任务 10-11 |
| 设计决策: Freeze 从 ICqrsRegistry 移除 | 任务 8 移除 Freeze 方法和所有调用点 |
| 设计决策: Unregister 不存在时 Command/Query 抛异常 | 任务 8 实现 + 任务 10 验证 |
| 设计决策: Unsubscribe 不存在时静默成功 | 任务 8 实现 + 任务 10 验证 |
| 回归风险: Freeze 移除影响现有测试 | 任务 9 更新所有 Freeze 相关测试 |

### 来自 FRD

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 验收条件: IDomainEvent 已移除 | 任务 1 |
| 验收条件: CqrsBus 同时实现 ICqrsRegistry + ICqrsRuntime | 任务 5 |
| 验收条件: 重复注册检测 | 任务 8 + 任务 10 |
| 验收条件: 单元测试覆盖注册/反注册 | 任务 10 |

## 目标

精简 CQRS 接口层次（删除 IDomainEvent、ICqrsRuntimeProvider、CqrsRuntime），让 CqrsBus 同时作为注册面和调度面，并支持运行时注册/反注册。

## 架构

方案 A：完全简化。`CqrsBus : ICqrsBus, ICqrsRegistry, ICqrsRuntime`，移除 Freeze 机制，线程安全由调用者在主线程保证。接口从 4 层减到 2 层。

## 技术栈

- C# (.NET Standard 2.1 / Unity)
- NUnit 测试框架 (Unity EditMode Tests)
- FastDictionary / FastList (Change.Framework.Collections)

## 文件清单

| 文件 | 操作 | 职责 | 所属任务 |
|------|------|------|----------|
| `Abstractions/IDomainEvent.cs` | 删除 | 冗余事件标记接口 | 1 |
| `Abstractions/IDomainEventHandler.cs` | 删除 | 冗余事件 Handler 接口 | 1 |
| `Abstractions/ICqrsRuntime.cs` | 修改 | Publish 约束 IDomainEvent→IEvent | 2 |
| `Core/CqrsRuntime.cs` | 删除 | 纯转发适配器 | 4 |
| `Abstractions/ICqrsRuntimeProvider.cs` | 删除 | Context 级 Runtime 提供者 | 4 |
| `Core/CqrsContextRuntimeProvider.cs` | 删除 | RuntimeProvider 实现 | 4 |
| `Exceptions/ContextNotRegisteredException.cs` | 删除 | 仅 RuntimeProvider 使用 | 4 |
| `Core/CqrsBus.cs` | 修改 | 添加 ICqrsRuntime + Unregister + 移除 Freeze | 5, 8 |
| `Core/CqrsBootstrap.cs` | 修改 | Build() 简化 | 5 |
| `Abstractions/ICqrsRegistry.cs` | 修改 | 添加 Unregister，移除 Freeze | 7 |
| `Exceptions/RegistryFrozenException.cs` | 删除 | Freeze 移除后无用 | 8 |
| `Tests/.../DomainEventSemanticsTests.cs` | 修改 | IDomainEvent→IEvent | 3 |
| `Tests/.../ZeroAllocationDispatchTests.cs` | 修改 | IDomainEvent→IEvent + 适配 | 3, 6 |
| `Tests/.../CqrsBootstrapLifecycleTests.cs` | 修改 | 适配 Build 变更 + 移除 Freeze 测试 | 3, 6 |
| `Tests/.../CqrsArchitectureGuardTests.cs` | 修改 | IDomainEventHandler→IEventHandler | 3 |
| `Tests/.../CqrsContextRuntimeProviderTests.cs` | 删除 | 随实现一同移除 | 4 |
| `Tests/.../CommandDispatchTests.cs` | 修改 | 移除 Freeze 调用 | 9 |
| `Tests/.../QueryDispatchTests.cs` | 修改 | 移除 Freeze 调用 | 9 |
| `Tests/.../EventDispatchTests.cs` | 修改 | 移除 Freeze 调用 | 9 |
| `Tests/.../RegistrationGuardTests.cs` | 修改 | 移除 Freeze 调用 | 9 |
| `Tests/.../RuntimeRegistrationTests.cs` | 新增 | 运行时注册/反注册全覆盖 | 10 |

基础路径: `UnityProject/Assets/Change/Framework/Cqrs/`
测试基础路径: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/`

## 任务依赖图

```
任务 1: 删除 IDomainEvent 接口        (无依赖)
  └── 任务 2: 更新 Publish 签名         (依赖 1)
        └── 任务 3: 更新 IEvent 测试     (依赖 2)
              └── 任务 4: 删除 RuntimeProvider (依赖 3)
                    └── 任务 5: CqrsBus 双接口 (依赖 4)
                          └── 任务 6: 扁平化测试 (依赖 5)
                                └── 任务 7: ICqrsRegistry Unregister (依赖 6)
                                      └── 任务 8: CqrsBus Unregister (依赖 7)
                                            └── 任务 9: 更新 Freeze 测试 (依赖 8)
                                                  └── 任务 10: RuntimeRegistrationTests (依赖 9)
                                                        └── 任务 11: 最终验证 (依赖 10)
```

## 风险评估

| 风险（来源） | 影响 | 计划中的缓解措施 |
|-------------|------|-----------------|
| Freeze 移除导致测试大量失败（Design Slice C 回归评估） | 多个测试文件预期 Freeze 行为 | 任务 9 系统性更新所有 Freeze 引用 |
| Gas 模块事件编译失败（Design Slice A 回归评估） | 编译错误 | Gas 事件已使用 IEvent，无需变更；任务 3 后编译验证 |
| 调度热路径性能回归（Design 整体回归评估） | 零分配保证被破坏 | 任务 11 运行 ZeroAllocationDispatchTests 确认通过 |
| 多 Context 场景失去 RuntimeProvider（Design Slice B 回归评估） | 无影响 | 当前无外部消费者使用 RuntimeProvider |

---

## 任务

### 任务 1: 删除 IDomainEvent 接口

**覆盖的上游需求：** Design 切片 A / FRD 验收条件 "IDomainEvent 已移除"

**文件：**
- 删除：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEvent.cs`
- 删除：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEvent.cs.meta`
- 删除：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEventHandler.cs`
- 删除：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEventHandler.cs.meta`

- [ ] **步骤 1: 删除文件**

```bash
rm UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEvent.cs
rm UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEvent.cs.meta
rm UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEventHandler.cs
rm UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEventHandler.cs.meta
```

- [ ] **步骤 2: 验证编译失败（预期）**

在 Unity 中打开项目或运行编译检查。预期：`ICqrsRuntime.cs` 和 `CqrsRuntime.cs` 引用了不存在的 `IDomainEvent` 类型，编译失败。

- [ ] **步骤 3: Commit**

```bash
git add -u UnityProject/Assets/Change/Framework/Cqrs/Abstractions/
git commit -m "refactor(cqrs): remove IDomainEvent and IDomainEventHandler interfaces

Delete redundant event marker/handler interfaces per FRD decision #1.
All events now use IEvent as the unified marker interface.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 2: 更新 ICqrsRuntime 和 CqrsRuntime Publish 签名

**覆盖的上游需求：** Design 切片 A / FRD 验收条件 "ICqrsRuntime.Publish 约束改为 IEvent"
**依赖：** 任务 1

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntime.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsRuntime.cs`

- [ ] **步骤 1: 修改 ICqrsRuntime.cs 第 19-21 行**

将:
```csharp
        void Publish<TDomainEvent>(in TDomainEvent domainEvent)
            where TDomainEvent : struct, IDomainEvent;
```
改为:
```csharp
        void Publish<TEvent>(in TEvent @event)
            where TEvent : struct, IEvent;
```

- [ ] **步骤 2: 修改 CqrsRuntime.cs 第 29-33 行**

将:
```csharp
        public void Publish<TDomainEvent>(in TDomainEvent domainEvent)
            where TDomainEvent : struct, IDomainEvent
        {
            _bus.Publish(in domainEvent);
        }
```
改为:
```csharp
        public void Publish<TEvent>(in TEvent @event)
            where TEvent : struct, IEvent
        {
            _bus.Publish(in @event);
        }
```

- [ ] **步骤 3: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntime.cs
git add UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsRuntime.cs
git commit -m "refactor(cqrs): update Publish signature from IDomainEvent to IEvent

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 3: 更新测试代码适配 IEvent 统一

**覆盖的上游需求：** Design 切片 A 验收标准 "所有现有测试通过（语义等价替换）"
**依赖：** 任务 2

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DomainEventSemanticsTests.cs`
- 修改：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ZeroAllocationDispatchTests.cs`
- 修改：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsBootstrapLifecycleTests.cs`
- 修改：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsArchitectureGuardTests.cs`

- [ ] **步骤 1: 修改 DomainEventSemanticsTests.cs**

变更内容：
1. `EnemyDefeatedDomainEvent : IDomainEvent` → `EnemyDefeatedDomainEvent : IEvent`
2. `ItemPickedUpDomainEvent : IDomainEvent` → `ItemPickedUpDomainEvent : IEvent`
3. `EnemyDefeatedDomainEventHandler : IDomainEventHandler<EnemyDefeatedDomainEvent>` → `EnemyDefeatedDomainEventHandler : IEventHandler<EnemyDefeatedDomainEvent>`

使用精确替换：

```bash
# 第 8 行
sed -i '' 's/: IDomainEvent$/ : IEvent/' UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DomainEventSemanticsTests.cs
# 第 18 行  
sed -i '' 's/: IDomainEvent$/ : IEvent/' UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DomainEventSemanticsTests.cs
# 第 27 行
sed -i '' 's/: IDomainEventHandler<EnemyDefeatedDomainEvent>/: IEventHandler<EnemyDefeatedDomainEvent>/' UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DomainEventSemanticsTests.cs
```

- [ ] **步骤 2: 修改 ZeroAllocationDispatchTests.cs**

```bash
# TickDomainEvent : IDomainEvent → TickDomainEvent : IEvent
sed -i '' 's/: IDomainEvent$/: IEvent/' UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ZeroAllocationDispatchTests.cs
# TickDomainEventHandler : IDomainEventHandler<TickDomainEvent> → IEventHandler<TickDomainEvent>
sed -i '' 's/: IDomainEventHandler<TickDomainEvent>/: IEventHandler<TickDomainEvent>/' UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ZeroAllocationDispatchTests.cs
```

- [ ] **步骤 3: 修改 CqrsBootstrapLifecycleTests.cs**

```bash
# TestDomainEvent : IDomainEvent → IEvent
sed -i '' 's/: IDomainEvent$/: IEvent/' UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsBootstrapLifecycleTests.cs
# TestDomainEventHandler : IDomainEventHandler<TestDomainEvent> → IEventHandler<TestDomainEvent>
sed -i '' 's/: IDomainEventHandler<TestDomainEvent>/: IEventHandler<TestDomainEvent>/' UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsBootstrapLifecycleTests.cs
```

- [ ] **步骤 4: 修改 CqrsArchitectureGuardTests.cs 第 219 行**

将 `ImplementsDomainEventHandler` 方法中的 `typeof(IDomainEventHandler<>)` 改为 `typeof(IEventHandler<>)`：

```csharp
        private static bool ImplementsDomainEventHandler(Type type)
        {
            foreach (var @interface in type.GetInterfaces())
            {
                if (!@interface.IsGenericType)
                {
                    continue;
                }

                if (@interface.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                {
                    return true;
                }
            }

            return false;
        }
```

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/
git commit -m "test(cqrs): update tests to use IEvent instead of IDomainEvent

Replace all IDomainEvent references with IEvent and IDomainEventHandler<>
with IEventHandler<> across all CQRS test files.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 4: 删除 RuntimeProvider 层

**覆盖的上游需求：** Design 切片 B / FRD 验收条件 "ICqrsRuntimeProvider、CqrsContextRuntimeProvider 已删除"
**依赖：** 任务 3

**文件：**
- 删除：`Cqrs/Abstractions/ICqrsRuntimeProvider.cs` + `.meta`
- 删除：`Cqrs/Core/CqrsContextRuntimeProvider.cs` + `.meta`
- 删除：`Cqrs/Core/CqrsRuntime.cs` + `.meta`
- 删除：`Cqrs/Exceptions/ContextNotRegisteredException.cs` + `.meta`
- 删除：`Tests/.../CqrsContextRuntimeProviderTests.cs` + `.meta`

- [ ] **步骤 1: 删除文件**

```bash
BASE="UnityProject/Assets/Change/Framework/Cqrs"
rm "$BASE/Abstractions/ICqrsRuntimeProvider.cs"
rm "$BASE/Abstractions/ICqrsRuntimeProvider.cs.meta"
rm "$BASE/Core/CqrsContextRuntimeProvider.cs"
rm "$BASE/Core/CqrsContextRuntimeProvider.cs.meta"
rm "$BASE/Core/CqrsRuntime.cs"
rm "$BASE/Core/CqrsRuntime.cs.meta"
rm "$BASE/Exceptions/ContextNotRegisteredException.cs"
rm "$BASE/Exceptions/ContextNotRegisteredException.cs.meta"
rm "UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsContextRuntimeProviderTests.cs"
rm "UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsContextRuntimeProviderTests.cs.meta"
```

- [ ] **步骤 2: 验证编译失败（预期）**

CqrsBootstrap.cs 引用了已删除的 `CqrsRuntime`，编译预期失败。

- [ ] **步骤 3: Commit**

```bash
git add -u UnityProject/Assets/Change/Framework/Cqrs/
git add -u UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/
git commit -m "refactor(cqrs): remove ICqrsRuntimeProvider, CqrsContextRuntimeProvider, and CqrsRuntime

Delete the context-scoped runtime provider layer and the CqrsRuntime adapter.
CqrsBus will directly implement ICqrsRuntime instead.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 5: CqrsBus 双接口 + CqrsBootstrap 简化

**覆盖的上游需求：** Design 切片 B / FRD 验收条件 "CqrsBus 同时实现 ICqrsRegistry 和 ICqrsRuntime"
**依赖：** 任务 4

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBootstrap.cs`

- [ ] **步骤 1: 修改 CqrsBus.cs 第 14 行 — 添加 ICqrsRuntime 接口**

将:
```csharp
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry
```
改为:
```csharp
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry, ICqrsRuntime
```

无需添加方法体 — `Send<TCommand>`, `Ask<TQuery, TResult>`, `Publish<TEvent>` 方法已在 CqrsBus 中存在且签名与 `ICqrsRuntime` 完全匹配（Publish 在任务 2 已统一约束）。

- [ ] **步骤 2: 修改 CqrsBootstrap.cs 第 47-66 行 — 简化 Build()**

将 `Build()` 方法体替换为直接返回 `_bus`：

```csharp
        public ICqrsRuntime Build()
        {
            var runtime = _runtime;
            if (runtime != null)
            {
                return runtime;
            }

            lock (_buildGate)
            {
                if (_runtime != null)
                {
                    return _runtime;
                }

                _runtime = _bus;
                return _runtime;
            }
        }
```

变更要点：
- 删除 `_bus.Freeze()` 调用
- `_runtime = _bus` 代替 `_runtime = new CqrsRuntime(_bus)`（CqrsBus 已实现 ICqrsRuntime）
- 同时更新类注释第 6-11 行（Freeze 相关描述改为运行时注册）

- [ ] **步骤 3: 更新 CqrsBootstrap.cs 类注释**

将第 5 行注释更新为：

```csharp
    /// Default CQRS bootstrap that owns registration and exposes the bus as a runtime.
    /// <para>
    /// After the first <see cref="Build"/>, subsequent calls return the same
    /// <see cref="ICqrsRuntime"/> instance (the underlying CqrsBus directly).
    /// Registration and dispatch are both available through the returned bus.
    /// </para>
```

- [ ] **步骤 4: 更新 CqrsBus.cs 类注释**

将第 9-12 行注释更新为：

```csharp
    /// In-process CQRS bus implementing registration, dispatch, and runtime surfaces.
    /// Supports runtime registration and unregistration on the main thread.
    /// Thread safety for registration is the caller's responsibility.
```

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs
git add UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBootstrap.cs
git commit -m "feat(cqrs): CqrsBus implements ICqrsRuntime, simplify CqrsBootstrap.Build()

CqrsBus now implements ICqrsBus, ICqrsRegistry, and ICqrsRuntime.
Build() returns CqrsBus directly instead of wrapping in CqrsRuntime adapter.
Removes Freeze() call from Build() to support runtime registration.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 6: 更新测试适配架构扁平化

**覆盖的上游需求：** Design 切片 B 验收标准 "所有现有调度测试通过"
**依赖：** 任务 5

**文件：**
- 修改：`Tests/.../CqrsBootstrapLifecycleTests.cs`
- 修改：`Tests/.../ZeroAllocationDispatchTests.cs`
- 修改：`Tests/.../CqrsArchitectureGuardTests.cs`

- [ ] **步骤 1: 删除 CqrsBootstrapLifecycleTests.cs 中 CqrsRuntime 相关测试**

删除 `Runtime_Constructor_WithNullBus_ThrowsArgumentNullException` 测试方法（CqrsRuntime 已删除）。

同时更新以下测试（Freeze 已不再被 Build 调用）：

`RegisterCommand_AfterBuild_ThrowsRegistryFrozenException` — 改为验证注册成功（不再抛异常）:

```csharp
        [Test]
        public void RegisterCommand_AfterBuild_Succeeds()
        {
            var bootstrap = new CqrsBootstrap();
            bootstrap.Build();

            Assert.DoesNotThrow(() => bootstrap.RegisterCommand(new TestCommandHandler(new Counter())));
        }
```

`RegisterQueryAndSubscribe_AfterBuild_ThrowRegistryFrozenException` — 改为验证注册和订阅成功：

```csharp
        [Test]
        public void RegisterQueryAndSubscribe_AfterBuild_Succeed()
        {
            var bootstrap = new CqrsBootstrap();
            bootstrap.Build();

            Assert.DoesNotThrow(() => bootstrap.RegisterQuery(new TestQueryHandler()));
            Assert.DoesNotThrow(() => bootstrap.Subscribe(new TestDomainEventHandler(new Counter())));
        }
```

同时删除对这些测试方法中 `using Change.Framework.Cqrs;` 的 `RegistryFrozenException` 引用检查。

- [ ] **步骤 2: 更新 CqrsArchitectureGuardTests.cs**

移除 `ForbiddenDispatchDependencies` 数组中的 `ICqrsRuntime` 行？不，保留它。设计文档约定事件 Handler 不应注入调度面（ICqrsBus 和 ICqrsRuntime）。CqrsBus 同时实现两者，禁止注入任一。

同时删除 `using System;` 中未使用的引用（如果 CqrsRuntime 被引用）。

- [ ] **步骤 3: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/
git commit -m "test(cqrs): update tests for flattened architecture

Update tests to reflect: CqrsRuntime deleted, Build() no longer freezes,
ICqrsRuntime still forbidden for event/query handler injection.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 7: 在 ICqrsRegistry 添加 Unregister API

**覆盖的上游需求：** Design 切片 C / FRD 验收条件 "运行时注册 API 可用"
**依赖：** 任务 6

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRegistry.cs`

- [ ] **步骤 1: 修改 ICqrsRegistry.cs — 添加 Unregister 方法，移除 Freeze**

将文件内容改为：

```csharp
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

        void UnregisterCommand<TCommand>()
            where TCommand : struct, ICommand;

        void UnregisterQuery<TQuery, TResult>()
            where TQuery : struct, IQuery<TResult>;

        void Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;
    }
}
```

变更：新增 3 个 Unregister 方法，删除 `Freeze()` 方法。

- [ ] **步骤 2: 验证编译失败（预期）**

CqrsBus.cs 声明实现了 ICqrsRegistry 但缺少 Unregister 方法，编译预期失败。

- [ ] **步骤 3: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRegistry.cs
git commit -m "feat(cqrs): add Unregister APIs to ICqrsRegistry, remove Freeze

Add UnregisterCommand, UnregisterQuery, Unsubscribe methods.
Remove Freeze() — runtime registration no longer requires freeze lifecycle.
Thread safety is caller's responsibility (main thread constraint).

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 8: 在 CqrsBus 实现 Unregister + 移除 Freeze

**覆盖的上游需求：** Design 切片 C 全部验收标准 / FRD 验收条件 "重复注册检测"、"反注册 API"
**依赖：** 任务 7

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- 删除：`UnityProject/Assets/Change/Framework/Cqrs/Exceptions/RegistryFrozenException.cs` + `.meta`

- [ ] **步骤 1: 从 Register* 方法中移除 Freeze 检查和 lock**

修改 `RegisterCommand`（第 113-134 行）：删除 `lock (_registrationGate)` 包裹、`ThrowIfFrozen()` 调用。保留 handler null 检查和值类型检查。

修改后的 `RegisterCommand`:

```csharp
        public void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            ThrowIfValueTypeHandler(handler, nameof(handler));

            var commandType = typeof(TCommand);
            if (!_commandHandlers.TryAdd(commandType, new CommandHandlerRegistration<TCommand>(handler)))
            {
                throw new DuplicateRegistrationException($"Command handler already registered: {commandType.FullName}");
            }

            SafeInfo(CommandRegisteredMessage);
        }
```

同样处理 `RegisterQuery`（移除 lock + ThrowIfFrozen）：

```csharp
        public void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            ThrowIfValueTypeHandler(handler, nameof(handler));

            var queryType = typeof(TQuery);
            var resultType = typeof(TResult);

            if (_queryResultByQueryType.TryGetValue(queryType, out var existingResultType))
            {
                if (existingResultType == resultType)
                {
                    throw new DuplicateRegistrationException(
                        $"Query handler already registered: {queryType.FullName} -> {resultType.FullName}");
                }

                throw new DuplicateRegistrationException(
                    $"Query type already registered with a different result: {queryType.FullName} -> {existingResultType.FullName}; " +
                    $"each query supports exactly one handler.");
            }

            var queryKey = new QueryKey(queryType, resultType);
            _queryHandlers.TryAdd(queryKey, new QueryHandlerRegistration<TQuery, TResult>(handler));
            _queryResultByQueryType.TryAdd(queryType, resultType);

            SafeInfo(QueryRegisteredMessage);
        }
```

同样处理 `Subscribe`（移除 lock + ThrowIfFrozen）：

```csharp
        public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            ThrowIfValueTypeHandler(handler, nameof(handler));

            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                handlerList = new EventHandlerList<TEvent>();
                _eventHandlers.TryAdd(eventType, handlerList);
            }

            var typedList = (EventHandlerList<TEvent>)handlerList;
            typedList.Handlers.Add(handler);

            SafeInfo(EventSubscribedMessage);
        }
```

- [ ] **步骤 2: 从 Send/Query/Publish 中移除 ThrowIfNotFrozen**

删除每个 dispatch 方法中的 `ThrowIfNotFrozen();` 调用（第 212、228、261 行）。

- [ ] **步骤 3: 删除 Freeze 方法和相关字段/方法**

删除：
- `Freeze()` 方法（第 200-206 行）
- `ThrowIfFrozen()` 方法（第 300-303 行）
- `ThrowIfNotFrozen()` 方法（第 294-298 行）
- `_isFrozen` 字段（第 96 行）
- `IsFrozen` 属性（第 101 行）
- `_registrationGate` 字段（第 94 行）
- `using System.Collections.Generic;` — 保留（Publish 中的 `List<Exception>` 仍然需要）

- [ ] **步骤 4: 添加 Unregister* 方法实现**

在 `Subscribe` 方法之后添加三个 Unregister 方法：

```csharp
        public void UnregisterCommand<TCommand>()
            where TCommand : struct, ICommand
        {
            var commandType = typeof(TCommand);
            if (!_commandHandlers.Remove(commandType))
            {
                throw new HandlerNotRegisteredException($"Command handler not registered: {commandType.FullName}");
            }
        }

        public void UnregisterQuery<TQuery, TResult>()
            where TQuery : struct, IQuery<TResult>
        {
            var queryType = typeof(TQuery);
            var resultType = typeof(TResult);
            var queryKey = new QueryKey(queryType, resultType);

            if (!_queryHandlers.Remove(queryKey))
            {
                throw new HandlerNotRegisteredException(
                    $"Query handler not registered: {queryType.FullName} -> {resultType.FullName}");
            }

            _queryResultByQueryType.Remove(queryType);
        }

        public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
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
            var index = typedList.Handlers.IndexOf(handler);
            if (index >= 0)
            {
                typedList.Handlers.RemoveAt(index);
            }
        }
```

- [ ] **步骤 5: 删除 RegistryFrozenException**

```bash
rm UnityProject/Assets/Change/Framework/Cqrs/Exceptions/RegistryFrozenException.cs
rm UnityProject/Assets/Change/Framework/Cqrs/Exceptions/RegistryFrozenException.cs.meta
```

- [ ] **步骤 6: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs
git add -u UnityProject/Assets/Change/Framework/Cqrs/Exceptions/
git commit -m "feat(cqrs): implement Unregister APIs and remove Freeze mechanism

- Add UnregisterCommand/UnregisterQuery/Unsubscribe to CqrsBus
- Remove lock, Freeze, _isFrozen, _registrationGate
- Remove ThrowIfFrozen/ThrowIfNotFrozen from dispatch
- Thread safety becomes caller's responsibility (main thread)
- Delete RegistryFrozenException (no longer used)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 9: 更新 dispatch 测试移除 Freeze 引用

**覆盖的上游需求：** Design 切片 C 回归风险评估 "Freeze 移除影响现有测试"
**依赖：** 任务 8

**文件：**
- 修改：`Tests/.../CommandDispatchTests.cs`
- 修改：`Tests/.../QueryDispatchTests.cs`
- 修改：`Tests/.../EventDispatchTests.cs`
- 修改：`Tests/.../RegistrationGuardTests.cs`

- [ ] **步骤 1: 更新 CommandDispatchTests.cs**

移除所有 `bus.Freeze();` 调用。删除 `RegisterCommand_AfterFreeze_ThrowsRegistryFrozenException` 和 `RegisterCommand_AfterFreeze_WithNullHandler_ThrowsRegistryFrozenException` 测试（RegistryFrozenException 已删除，且 Freeze 已移除）。

对于需要先注册再 dispatch 的测试，直接移除 Freeze 行即可（dispatch 不再需要 freeze 前置条件）。

- [ ] **步骤 2: 更新 QueryDispatchTests.cs**

同样：移除所有 `bus.Freeze();` 调用。删除 AfterFreeze 相关测试。

- [ ] **步骤 3: 更新 EventDispatchTests.cs**

同样：移除所有 `bus.Freeze();` 调用。删除 Subscribe_AfterFreeze 相关测试。

- [ ] **步骤 4: 更新 RegistrationGuardTests.cs**

移除 `bus.Freeze();` 调用（如果存在）。

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/
git commit -m "test(cqrs): remove Freeze calls from all CQRS dispatch tests

Remove bus.Freeze() from CommandDispatchTests, QueryDispatchTests,
EventDispatchTests, RegistrationGuardTests. Delete freeze-related
test methods (RegistryFrozenException no longer exists).

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 10: 编写 RuntimeRegistrationTests

**覆盖的上游需求：** Design 切片 D 全部验收标准 / FRD 验收条件 "单元测试覆盖注册/反注册场景"
**依赖：** 任务 9

**文件：**
- 新增：`Tests/EditMode/Cqrs/RuntimeRegistrationTests.cs`

- [ ] **步骤 1: 创建测试文件**

```csharp
using System;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class RuntimeRegistrationTests
    {
        private readonly struct RegTestCommand : ICommand
        {
            public RegTestCommand(int value) { Value = value; }
            public int Value { get; }
        }

        private readonly struct RegTestQuery : IQuery<int>
        {
            public RegTestQuery(int multiplier) { Multiplier = multiplier; }
            public int Multiplier { get; }
        }

        private readonly struct RegTestEvent : IEvent
        {
            public RegTestEvent(int delta) { Delta = delta; }
            public int Delta { get; }
        }

        private sealed class Counter { public int Value; }

        private sealed class RegTestCommandHandler : ICommandHandler<RegTestCommand>
        {
            private readonly Counter _c;
            public RegTestCommandHandler(Counter c) { _c = c; }
            public void Handle(in RegTestCommand cmd) { _c.Value += cmd.Value; }
        }

        private sealed class RegTestQueryHandler : IQueryHandler<RegTestQuery, int>
        {
            private readonly Counter _c;
            public RegTestQueryHandler(Counter c) { _c = c; }
            public int Handle(in RegTestQuery q) { return _c.Value * q.Multiplier; }
        }

        private sealed class RegTestEventHandler : IEventHandler<RegTestEvent>
        {
            private readonly Counter _c;
            public RegTestEventHandler(Counter c) { _c = c; }
            public void Handle(in RegTestEvent e) { _c.Value += e.Delta; }
        }

        // === Duplicate Registration Tests ===

        [Test]
        public void RegisterCommand_DuplicateRegistration_Throws()
        {
            var bus = new CqrsBus();
            bus.RegisterCommand(new RegTestCommandHandler(new Counter()));
            Assert.Throws<DuplicateRegistrationException>(
                () => bus.RegisterCommand(new RegTestCommandHandler(new Counter())));
        }

        [Test]
        public void RegisterQuery_DuplicateRegistration_Throws()
        {
            var bus = new CqrsBus();
            bus.RegisterQuery(new RegTestQueryHandler(new Counter()));
            Assert.Throws<DuplicateRegistrationException>(
                () => bus.RegisterQuery(new RegTestQueryHandler(new Counter())));
        }

        [Test]
        public void Subscribe_DuplicateSubscription_AppendsSuccessfully()
        {
            var bus = new CqrsBus();
            var handler = new RegTestEventHandler(new Counter());
            bus.Subscribe(handler);
            Assert.DoesNotThrow(() => bus.Subscribe(handler));
        }

        // === Unregister Command Tests ===

        [Test]
        public void UnregisterCommand_WhenRegistered_RemovesHandler()
        {
            var bus = new CqrsBus();
            var counter = new Counter();
            bus.RegisterCommand(new RegTestCommandHandler(counter));
            bus.UnregisterCommand<RegTestCommand>();
            Assert.Throws<HandlerNotRegisteredException>(
                () => bus.Send(new RegTestCommand(1)));
        }

        [Test]
        public void UnregisterCommand_WhenNotRegistered_Throws()
        {
            var bus = new CqrsBus();
            Assert.Throws<HandlerNotRegisteredException>(
                () => bus.UnregisterCommand<RegTestCommand>());
        }

        [Test]
        public void UnregisterCommand_ThenReregister_Works()
        {
            var bus = new CqrsBus();
            var counter = new Counter();
            bus.RegisterCommand(new RegTestCommandHandler(counter));
            bus.UnregisterCommand<RegTestCommand>();
            bus.RegisterCommand(new RegTestCommandHandler(counter));
            bus.Send(new RegTestCommand(5));
            Assert.AreEqual(5, counter.Value);
        }

        // === Unregister Query Tests ===

        [Test]
        public void UnregisterQuery_WhenRegistered_RemovesHandler()
        {
            var bus = new CqrsBus();
            var counter = new Counter { Value = 10 };
            bus.RegisterQuery(new RegTestQueryHandler(counter));
            bus.UnregisterQuery<RegTestQuery, int>();
            Assert.Throws<HandlerNotRegisteredException>(
                () => bus.Ask<RegTestQuery, int>(new RegTestQuery(2)));
        }

        [Test]
        public void UnregisterQuery_WhenNotRegistered_Throws()
        {
            var bus = new CqrsBus();
            Assert.Throws<HandlerNotRegisteredException>(
                () => bus.UnregisterQuery<RegTestQuery, int>());
        }

        // === Unsubscribe Tests ===

        [Test]
        public void Unsubscribe_RemovesSpecificHandler()
        {
            var bus = new CqrsBus();
            var counter1 = new Counter();
            var counter2 = new Counter();
            var h1 = new RegTestEventHandler(counter1);
            var h2 = new RegTestEventHandler(counter2);
            bus.Subscribe(h1);
            bus.Subscribe(h2);
            bus.Unsubscribe(h1);
            bus.Publish(new RegTestEvent(3));
            Assert.AreEqual(0, counter1.Value, "Unsubscribed handler should not receive event");
            Assert.AreEqual(3, counter2.Value);
        }

        [Test]
        public void Unsubscribe_WhenNotSubscribed_IsIdempotent()
        {
            var bus = new CqrsBus();
            var handler = new RegTestEventHandler(new Counter());
            Assert.DoesNotThrow(() => bus.Unsubscribe(handler));
        }

        [Test]
        public void Unsubscribe_WithNullHandler_ThrowsArgumentNullException()
        {
            var bus = new CqrsBus();
            Assert.Throws<ArgumentNullException>(() => bus.Unsubscribe<RegTestEvent>(null));
        }

        // === Full Lifecycle Tests ===

        [Test]
        public void RegisterSendUnregisterSend_ThrowsHandlerNotRegistered()
        {
            var bus = new CqrsBus();
            var counter = new Counter();
            bus.RegisterCommand(new RegTestCommandHandler(counter));
            bus.Send(new RegTestCommand(10));
            Assert.AreEqual(10, counter.Value);
            bus.UnregisterCommand<RegTestCommand>();
            Assert.Throws<HandlerNotRegisteredException>(
                () => bus.Send(new RegTestCommand(1)));
        }
    }
}
```

- [ ] **步骤 2: 创建 .asmdef 引用**

确认测试 assembly definition 文件已包含对 `Change.Framework` 的引用（现有测试已工作，无需修改）。

- [ ] **步骤 3: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/RuntimeRegistrationTests.cs
git commit -m "test(cqrs): add RuntimeRegistrationTests covering register/unregister scenarios

Covers: duplicate registration detection, UnregisterCommand/UnregisterQuery/
Unsubscribe, idempotent unsubscribe, full register-unregister-reregister lifecycle.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 任务 11: 最终验证与线程安全文档

**覆盖的上游需求：** FRD 验收条件 "线程安全使用约束文档完成" / Design 切片 D
**依赖：** 任务 10

**文件：**
- 修改：`Cqrs/Core/CqrsBus.cs`（添加线程安全注释）

- [ ] **步骤 1: 在 CqrsBus 类注释中添加线程安全约束**

更新类级 XML 文档注释：

```csharp
    /// <summary>
    /// In-process CQRS bus implementing registration, dispatch, and runtime surfaces.
    /// Supports runtime registration and unregistration.
    /// </summary>
    /// <remarks>
    /// <para><b>Thread safety:</b> Registration and unregistration methods
    /// (<see cref="RegisterCommand{TCommand}"/>, <see cref="UnregisterCommand{TCommand}"/>,
    /// etc.) must be called from the main thread only. The caller is responsible for
    /// ensuring thread safety — no internal locking is performed.</para>
    /// <para>Dispatch methods (<see cref="Send{TCommand}"/>, <see cref="Ask{TQuery, TResult}"/>,
    /// <see cref="Publish{TEvent}"/>) may be called from any thread after registration
    /// is complete, as the internal dictionaries are read-only during dispatch.</para>
    /// <para>Registration during an active dispatch on the same thread is safe
    /// (synchronous execution guarantees no interleaving).</para>
    /// </remarks>
```

- [ ] **步骤 2: 运行完整 CQRS 测试套件确认全部通过**

```bash
# Unity EditMode test run (using Unity CLI)
# Expected: all CQRS tests pass
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "Change.Framework.Tests"
```

预期输出：所有 CQRS 测试通过，包括 RuntimeRegistrationTests。

- [ ] **步骤 3: 运行零分配测试确认无性能回归**

预期：`Send_HotPath_AllocatesZeroBytesAfterWarmup`、`Query_HotPath_AllocatesZeroBytesAfterWarmup`、`Publish_HotPath_AllocatesZeroBytesAfterWarmup` 全部通过。

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs
git commit -m "docs(cqrs): add thread safety documentation to CqrsBus

Document main-thread constraint for registration/unregistration.
Dispatch remains thread-safe for reads after registration completes.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### 验证总步骤

全部任务完成后执行：

```bash
# 1. 编译检查
# 在 Unity 中确认项目编译通过，无 CS 错误

# 2. 运行全套 CQRS 测试
Unity -runTests -projectPath UnityProject -testPlatform EditMode -testFilter "Change.Framework.Tests"

# 3. 确认删除的文件不再出现在代码库
grep -r "IDomainEvent\|IDomainEventHandler\|ICqrsRuntimeProvider\|CqrsContextRuntimeProvider\|CqrsRuntime\|RegistryFrozenException\|ContextNotRegisteredException" \
  UnityProject/Assets/ --include="*.cs" | grep -v ".meta" | grep -v "/Tests/EditMode/Cqrs/DomainEventSemanticsTests.cs" | grep -v "/Tests/EditMode/Cqrs/ZeroAllocationDispatchTests.cs"
# 预期：仅在测试文件中出现作为历史命名引用（DomainEventSemanticsTests 中的类型名）

# 4. 确认 CqrsBus 实现三个接口
grep "public sealed class CqrsBus" UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs
# 预期输出：public sealed class CqrsBus : ICqrsBus, ICqrsRegistry, ICqrsRuntime
```
