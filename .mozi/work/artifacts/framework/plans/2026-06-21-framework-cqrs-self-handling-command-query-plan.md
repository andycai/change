# CQRS 自处理 Command/Query 实现计划

> 日期: 2026-06-21 | 状态: 草稿
> 上游 FRD: [2026-06-21-framework-cqrs-self-handling-command-query-frd.md](../discover/2026-06-21-framework-cqrs-self-handling-command-query-frd.md)
> 替代: 原 `2026-06-18-framework-cqrs-dual-mode-command-query-plan.md`（已废止）

## 上游产出引用

### 来自 FRD

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| ICommand 增加 `void Execute()` | 任务 1 修改 ICommand.cs |
| IQuery\<TResult\> 增加 `TResult Query()` | 任务 1 修改 IQuery.cs |
| 删除 ISelfHandlingCommand / ISelfHandlingQuery | 任务 1 删除文件 |
| 删除 I*Handler 接口 | 任务 1 删除 ICommandHandler.cs, IQueryHandler.cs, IAsyncCommandHandler.cs, IAsyncQueryHandler.cs |
| IAsyncCommand 返回 UniTask，独立不继承 | 任务 1 创建 IAsyncCommand.cs |
| IAsyncQuery\<TResult\> 返回 UniTask\<TResult\>，独立不继承 | 任务 1 创建 IAsyncQuery.cs |
| 允许同时实现 ICommand + IAsyncCommand | 编译时通过接口约束自然支持 |
| Bus 接口更新（UniTask, 删除 Query, 删除注册） | 任务 2 修改 ICqrsBus.cs, ICqrsRuntime.cs, ICqrsRegistry.cs, ICqrsBootstrap.cs |
| CqrsBus 发送直接调用 Execute/Query | 任务 3 简化 CqrsBus（删除 Handler 字典、DynamicMethod、ResetIfPoolable） |
| CqrsBootstrap 删除注册方法 | 任务 4 简化 CqrsBootstrap |
| 删除 HandlerNotRegisteredException / DuplicateRegistrationException / ModeConflictException | 任务 4 删除异常文件（保留 ClosureCaptureException） |
| 验收条件覆盖 | 任务 5-8 覆盖所有测试 |
| Event 保持不变 | 任务 5-8 中 Event 相关测试不修改 |

## 目标

将 CQRS Command/Query 从双模式（Class Handler + Struct SelfHandling）简化为纯 Struct 自处理模式。删除所有 Handler 接口和注册 API，Command/Query struct 自身实现执行逻辑。异步改用 UniTask。Event 保持不动。

## 架构

- `ICommand` 从 marker 变为带 `void Execute()` 的接口
- `IQuery<TResult>` 从 marker 变为带 `TResult Query()` 的接口
- `IAsyncCommand`/`IAsyncQuery<TResult>` 为独立接口（不继承同步变体），返回 `UniTask`/`UniTask<TResult>`
- `CqrsBus.Send/Ask` 直接调用 struct 的 `Execute()`/`Query()`，不再查找 Handler 字典
- Handler 注册 API 从 `ICqrsRegistry`、`ICqrsBootstrap`、`CqrsBus`、`CqrsBootstrap` 中全部删除
- Event 相关 API（`IEventHandler`、`Subscribe`/`Publish`/`Unsubscribe`）完全不变

## 技术栈

- UniTask (`Cysharp.Threading.Tasks`) — 项目中已使用
- NUnit + Unity Test Runner — 测试框架

## 文件清单

| 文件 | 操作 | 职责 | 所属任务 |
|------|------|------|----------|
| `Cqrs/Abstractions/ICommand.cs` | 修改 | 增加 `void Execute()` 方法 | 1 |
| `Cqrs/Abstractions/IQuery.cs` | 修改 | 增加 `TResult Query()` 方法 | 1 |
| `Cqrs/Abstractions/IAsyncCommand.cs` | 创建 | `IAsyncCommand` 接口 `UniTask ExecuteAsync()` | 1 |
| `Cqrs/Abstractions/IAsyncQuery.cs` | 创建 | `IAsyncQuery<TResult>` 接口 `UniTask<TResult> QueryAsync()` | 1 |
| `Cqrs/Abstractions/ISelfHandlingCommand.cs` | 删除 | 已合并到 ICommand | 1 |
| `Cqrs/Abstractions/ISelfHandlingQuery.cs` | 删除 | 已合并到 IQuery\<TResult\> | 1 |
| `Cqrs/Abstractions/ICommandHandler.cs` | 删除 | 不再需要 Handler 概念 | 1 |
| `Cqrs/Abstractions/IQueryHandler.cs` | 删除 | 不再需要 Handler 概念 | 1 |
| `Cqrs/Abstractions/IAsyncCommandHandler.cs` | 删除 | 替换为 IAsyncCommand | 1 |
| `Cqrs/Abstractions/IAsyncQueryHandler.cs` | 删除 | 替换为 IAsyncQuery\<TResult\> | 1 |
| `Cqrs/Abstractions/ICqrsBus.cs` | 修改 | 异步返回 UniTask，删除 Query 方法，更新泛型约束 | 2 |
| `Cqrs/Abstractions/ICqrsRuntime.cs` | 修改 | 异步返回 UniTask，删除 Query 方法 | 2 |
| `Cqrs/Abstractions/ICqrsRegistry.cs` | 修改 | 删除 CQ 注册方法，仅保留 Event Subscribe/Unsubscribe | 2 |
| `Cqrs/Abstractions/ICqrsBootstrap.cs` | 修改 | 删除 CQ 注册方法，仅保留 Event Subscribe + Build | 2 |
| `Cqrs/Core/CqrsBus.cs` | 修改 | 删除 Handler 字典、SelfHandlingCache、DynamicMethod、ResetIfPoolable；Send/Ask 直接调用 Execute/Query | 3 |
| `Cqrs/Core/CqrsBootstrap.cs` | 修改 | 删除 CQ 注册方法，仅保留 Event Subscribe | 4 |
| `Cqrs/Exceptions/ModeConflictException.cs` | 删除 | 不再需要双模式冲突 | 4 |
| `Cqrs/Exceptions/DuplicateRegistrationException.cs` | 删除 | CQ 不再注册，Event 允许重复 | 4 |
| `Cqrs/Exceptions/HandlerNotRegisteredException.cs` | 删除 | 不再需要 Handler 注册 | 4 |
| `Cqrs/Exceptions/ClosureCaptureException.cs` | 保留 | 仍用于 Event 委托闭包检测 | — |
| `Tests/EditMode/Cqrs/CommandDispatchTests.cs` | 重写 | 使用自处理 Command 测试 Send | 5 |
| `Tests/EditMode/Cqrs/QueryDispatchTests.cs` | 重写 | 使用自处理 Query 测试 Ask | 5 |
| `Tests/EditMode/Cqrs/AsyncDispatchTests.cs` | 重写 | 使用 IAsyncCommand/IAsyncQuery + UniTask | 5 |
| `Tests/EditMode/Cqrs/SelfHandlingDispatchTests.cs` | 删除 | 合并到 CommandDispatchTests/QueryDispatchTests | 5 |
| `Tests/EditMode/Cqrs/ModeConflictTests.cs` | 删除 | 不再需要 | 5 |
| `Tests/EditMode/Cqrs/RuntimeRegistrationTests.cs` | 大幅缩减 | 只保留 Event Subscribe/Unsubscribe 测试 | 6 |
| `Tests/EditMode/Cqrs/PoolingResetTests.cs` | 删除 | 不再需要 IPoolable Reset | 6 |
| `Tests/EditMode/Cqrs/ZeroAllocationDispatchTests.cs` | 重写 | 使用自处理 Command/Query 测试 | 6 |
| `Tests/EditMode/Cqrs/CqrsArchitectureGuardTests.cs` | 修改 | 删除 IQueryHandler 引用检查 | 6 |
| `Tests/EditMode/Cqrs/CqrsBootstrapLifecycleTests.cs` | 修改 | 删除 CQ 注册相关测试 | 6 |
| `Tests/EditMode/Cqrs/CqrsLoggingIntegrationTests.cs` | 修改/保留 | 调整注册相关测试（如功能被删除则删除文件） | 6 |
| `GameScript/Composition/GameHotfixInstaller.cs` | 修改 | 删除 Handler 注册，改为自处理 struct | 7 |
| `GameScript/UI/Quest/QuestCommandHandlers.cs` | 重写 | 将 Handler 逻辑迁移为 Command struct 的 Execute() | 7 |
| `GameScript/UI/Quest/QuestQueryHandlers.cs` | 重写 | 将 Handler 逻辑迁移为 Query struct 的 Query() | 7 |
| `GameScript/UI/Quest/QuestMessages.cs` | 修改 | 为 Command/Query struct 增加 Execute()/Query() 方法 | 7 |
| `Runtime/Tests/PlayMode/Quest/QuestFairyGuiPlayModeTests.cs` | 修改 | CreateBus 改为自处理模式 | 8 |
| `GameScript/Tests/EditMode/Quest/QuestSessionStateTests.cs` | 修改 | CreateBus 改为自处理模式 | 8 |
| `Documentation/` 目录 | 检查更新 | 更新接口说明中的引用 | 9 |

## 任务依赖图

```
任务 1: 定义新接口 + 删除旧接口
  ├── 任务 2: 更新 Bus 接口
  │     └── 任务 3: 简化 CqrsBus 实现
  │           └── 任务 4: 简化 CqrsBootstrap + 清理异常
  │                 ├── 任务 5: 重写框架测试（dispatch）
  │                 │     └── 任务 6: 重写框架测试（runtime + 性能）
  │                 │           └── 任务 9: 验证全部通过
  │                 └── 任务 7: 更新 GameScript 使用代码
  │                       └── 任务 8: 更新 GameScript 测试
  │                             └── 任务 9: 验证全部通过
  任务 0（并行）: 归档旧 FRD（已完）
```

## 风险评估

| 风险（来源） | 影响 | 计划中的缓解措施 |
|-------------|------|-----------------|
| 大量文件变更导致遗漏 | 编译失败或运行时报错 | 任务 9 进行全局编译检查 + 完整测试套件运行 |
| ICommand/IQuery 从 marker 改为带方法接口，现有实现未添加 Execute/Query | 编译失败 | 按文件逐个修改，先改接口后立即编译验证 |
| GameScript 业务代码迁移时 Handler 逻辑翻译错误 | 业务功能回归 | 任务 7 迁移后由任务 8 的业务测试覆盖验证 |
| Event 被意外影响 | Event 订阅/发布失败 | 所有 Event 测试保持不变（任务 5/6 排除 Event 修改） |
| UniTask 依赖不存在 | 编译失败 | 项目中已有 Cysharp.Threading.Tasks，搜索确认已使用 |

---

## 任务

### 任务 0: 归档旧 FRD

**已完成于 discover 阶段。** 旧文件已重命名为 `.superseded`。

### 任务 1: 定义新接口 + 删除旧接口

**覆盖的上游需求：** FRD 验收条件 1-4（ICommand + Execute, IQuery + Query, IAsyncCommand, IAsyncQuery, 删除旧接口）

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICommand.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IQuery.cs`
- 创建：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IAsyncCommand.cs`
- 创建：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IAsyncQuery.cs`
- 删除：`.../ISelfHandlingCommand.cs`
- 删除：`.../ISelfHandlingQuery.cs`
- 删除：`.../ICommandHandler.cs`
- 删除：`.../IQueryHandler.cs`
- 删除：`.../IAsyncCommandHandler.cs`
- 删除：`.../IAsyncQueryHandler.cs`

- [ ] **步骤 1: 修改 ICommand.cs — 增加 Execute 方法**

```csharp
namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Command message that executes itself. Implement as <c>readonly struct</c>
    /// for zero-allocation dispatch. Provides <see cref="Execute"/> as the
    /// single execution entry point, invoked by <c>CqrsBus.Send&lt;T&gt;</c>.
    /// </summary>
    public interface ICommand
    {
        void Execute();
    }
}
```

- [ ] **步骤 2: 修改 IQuery.cs — 增加 Query 方法**

```csharp
namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Query message that computes its own result. Implement as <c>readonly struct</c>
    /// for zero-allocation dispatch. Provides <see cref="Query"/> as the single
    /// execution entry point, invoked by <c>CqrsBus.Ask&lt;T&gt;</c>.
    /// </summary>
    public interface IQuery<TResult>
    {
        TResult Query();
    }
}
```

- [ ] **步骤 3: 创建 IAsyncCommand.cs**

```csharp
using Cysharp.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Async command that executes itself and returns a <see cref="UniTask"/>.
    /// Implement as <c>readonly struct</c>. Dispatched via <c>CqrsBus.SendAsync&lt;T&gt;</c>.
    /// Does <b>not</b> inherit <see cref="ICommand"/>; a struct may implement both.
    /// </summary>
    public interface IAsyncCommand
    {
        UniTask ExecuteAsync();
    }
}
```

- [ ] **步骤 4: 创建 IAsyncQuery.cs**

```csharp
using Cysharp.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Async query that computes its own result and returns a <see cref="UniTask{TResult}"/>.
    /// Implement as <c>readonly struct</c>. Dispatched via <c>CqrsBus.AskAsync&lt;T&gt;</c>.
    /// Does <b>not</b> inherit <see cref="IQuery{TResult}"/>; a struct may implement both.
    /// </summary>
    public interface IAsyncQuery<TResult>
    {
        UniTask<TResult> QueryAsync();
    }
}
```

- [ ] **步骤 5: 创建 .meta 文件（Unity 资源导入）**

```bash
# Unity 会自动生成 .meta，将新 cs 文件放入 Assets 目录后如有所需可手动创建。
# 编辑器中导入后 Assets 会自动生成。操作方式：确保目录已存在 .meta 引用。
```

- [ ] **步骤 6: 删除旧接口文件（移出仓库而非重命名）**

使用 git rm 删除以下文件：
```bash
git rm UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ISelfHandlingCommand.cs
git rm UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ISelfHandlingQuery.cs
git rm UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICommandHandler.cs
git rm UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IQueryHandler.cs
git rm UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IAsyncCommandHandler.cs
git rm UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IAsyncQueryHandler.cs
```
同时删除对应的 `.cs.meta` 文件（存在于同一目录）。

- [ ] **步骤 7: 编译检查**

```bash
cd UnityProject && dotnet build 2>&1 | tail -20
```
预期：编译错误，因为 ICommand/IQuery 接口新增方法导致现有实现缺少 Execute()/Query()。这是预期的 TDD 红灯。

- [ ] **步骤 8: Commit**

```bash
git add -A
git commit -m "feat(cqrs): define ICommand.Execute, IQuery.Query, IAsyncCommand/IAsyncQuery; delete handler interfaces
- ICommand: add void Execute() (replaces ISelfHandlingCommand)
- IQuery<TResult>: add TResult Query() (replaces ISelfHandlingQuery<TResult>)
- IAsyncCommand: add UniTask ExecuteAsync() (independent)
- IAsyncQuery<TResult>: add UniTask<TResult> QueryAsync() (independent)
- Delete: ICommandHandler, IQueryHandler, IAsyncCommandHandler, IAsyncQueryHandler, ISelfHandlingCommand, ISelfHandlingQuery"
```

### 任务 2: 更新 Bus 接口

**覆盖的上游需求：** FRD 验收条件 5-8（UniTask 返回值、删除 Query、删除注册 API）

**依赖：** 任务 1

**文件：**
- 修改：`ICqrsBus.cs`
- 修改：`ICqrsRuntime.cs`
- 修改：`ICqrsRegistry.cs`
- 修改：`ICqrsBootstrap.cs`

- [ ] **步骤 1: 修改 ICqrsBus.cs**

```csharp
using Cysharp.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    public interface ICqrsBus
    {
        void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand;

        TResult Ask<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>;

        void Publish<TEvent>(in TEvent @event)
            where TEvent : struct, IEvent;

        UniTask SendAsync<TCommand>(TCommand command)
            where TCommand : struct, IAsyncCommand;

        UniTask<TResult> AskAsync<TQuery, TResult>(TQuery query)
            where TQuery : struct, IAsyncQuery<TResult>;
    }
}
```

变更说明：
- `SendAsync` 泛型约束从 `ICommand` 改为 `IAsyncCommand`
- `AskAsync` 泛型约束从 `IQuery<TResult>` 改为 `IAsyncQuery<TResult>`
- 返回值从 `Task` / `Task<TResult>` 改为 `UniTask` / `UniTask<TResult>`
- 删除 `Query` 方法（已由 `Ask` 覆盖）

- [ ] **步骤 2: 修改 ICqrsRuntime.cs**

```csharp
using Cysharp.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    public interface ICqrsRuntime
    {
        void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand;

        TResult Ask<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>;

        void Publish<TEvent>(in TEvent @event)
            where TEvent : struct, IEvent;

        UniTask SendAsync<TCommand>(TCommand command)
            where TCommand : struct, IAsyncCommand;

        UniTask<TResult> AskAsync<TQuery, TResult>(TQuery query)
            where TQuery : struct, IAsyncQuery<TResult>;
    }
}
```

变更说明：与 `ICqrsBus` 相同 + 删除 `Query` 方法。

- [ ] **步骤 3: 修改 ICqrsRegistry.cs**

```csharp
using System;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Mutable registration surface for CQRS event handlers only.
    /// Command/Query handler registration has been removed — all commands and queries
    /// are now self-handling structs with Execute()/Query() methods.
    /// </summary>
    public interface ICqrsRegistry
    {
        void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;

        void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent;

        void Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;

        void Unsubscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent;
    }
}
```

变更说明：删除 `RegisterCommand`、`RegisterQuery`、`RegisterAsyncCommand`、`RegisterAsyncQuery`、`UnregisterCommand`、`UnregisterQuery`。

- [ ] **步骤 4: 修改 ICqrsBootstrap.cs**

```csharp
using System;

namespace Change.Framework.Cqrs
{
    public interface ICqrsBootstrap
    {
        void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;

        void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent;

        ICqrsRuntime Build();
    }
}
```

变更说明：删除 `RegisterCommand`、`RegisterQuery`、`RegisterAsyncCommand`、`RegisterAsyncQuery`。

- [ ] **步骤 5: Commit**

```bash
git add -A
git commit -m "feat(cqrs): update bus interfaces - UniTask returns, remove CQ registration APIs
- ICqrsBus/ICqrsRuntime: SendAsync/AskAsync return UniTask, constraints -> IAsyncCommand/IAsyncQuery
- ICqrsBus/ICqrsRuntime: remove Query method
- ICqrsRegistry/ICqrsBootstrap: remove all Command/Query registration methods"
```

### 任务 3: 简化 CqrsBus 实现

**覆盖的上游需求：** FRD 验收条件 7-9（Send/Ask 直接调用 Execute/Query, 删除 Handler 字典、DynamicMethod、ResetIfPoolable）

**依赖：** 任务 2

**文件：**
- 修改：`CqrsBus.cs`

- [ ] **步骤 1: 重写 CqrsBus.cs**

将 `CqrsBus.cs` 重写为简化的实现。变更要点：

1. 删除 `_commandHandlers`、`_queryHandlers`、`_asyncCommandHandlers`、`_asyncQueryHandlers`、`_queryResultByQueryType` 字典
2. 删除 `ICommandHandlerRegistration`、`IQueryHandlerRegistration`、`IAsyncCommandHandlerRegistration`、`IAsyncQueryHandlerRegistration` 及其实现类
3. 删除 `SelfHandlingCommandCache`、`SelfHandlingQueryCache`、`SelfHandlingQueryInvokeCache`
4. 删除 `CommandHandlerRegistration`、`QueryHandlerRegistration`、`AsyncCommandHandlerRegistration`、`AsyncQueryHandlerRegistration`
5. 删除 `RegisterCommand`、`RegisterQuery`、`RegisterAsyncCommand`、`RegisterAsyncQuery`、`UnregisterCommand`、`UnregisterQuery`
6. 简化 `Send<TCommand>`：直接调用 `default(TCommand).Execute()` 或通过 `Unsafe.As`... 实际上，由于泛型约束 `where TCommand : struct, ICommand`，可以直接：
   ```csharp
   command.Execute();  // 编译器会通过约束调用接口方法，对 struct 不会装箱
   ```
   注意：由于泛型约束 `ICommand` 现在有 `Execute()`，`command.Execute()` 直接可用。
   但我们需要使用 `in` 参数（readonly ref），对值类型调用接口方法会触发拷贝。
   
   解决方案：由于 `ICommand.Execute()` 现在是值类型实例上的接口方法，直接调用 `command.Execute()` 会装箱（值类型在 `in` 参数上调用接口方法）。需要确保零装箱。
   
   之前的设计使用了 `constrained callvirt` IL。现在我们更简单 - 因为约束保证了它是 struct，可以使用 `Unsafe.As` 或者：
   
   实际上，对于 `in TCommand command where TCommand : struct, ICommand`，调用 `command.Execute()` 会：
   1. 通过 `in` 参数传入的是 readonly ref
   2. 调用接口方法时，CLR 需要 box 结构体（因为接口方法在值类型上调用时，如果通过 `in` 参数传入，ref 无法作为 `this` 传入）
   3. 所以 `command.Execute()` 会装箱！
   
   我们需要避免装箱。原来的代码是怎么处理的？
   
   原来的 `SelfHandlingCommandCache` 通过 DynamicMethod 生成 `Action<TCommand>` 来避免装箱。
   
   但现在更简单 - 因为我们不再需要区分 self-handling 和 handler 模式，我们可以直接复制传入的 struct 值到本地变量，然后调用方法：
   ```csharp
   public void Send<TCommand>(in TCommand command) where TCommand : struct, ICommand
   {
       var copy = command;  // 栈拷贝
       copy.Execute();      // 对值类型变量调用接口方法，constrained callvirt，无装箱
   }
   ```
   
   这样栈拷贝一次（对 struct 来说是 O(1) 的），然后对本地变量调用 `Execute()` 时编译器会发出 `constrained callvirt`，不会装箱。
   
   或者更好的方式 - 使用 Unsafe 类型：
   ```csharp
   Unsafe.AsRef(in command).Execute();
   ```
   但这会把 readonly ref 变成 mutable ref，如果 `Execute()` 修改了 struct 字段，编译器不会报错。
   
   所以最简单的安全做法是栈拷贝 + 调用。

   Wait, 实际上我们需要想一下：对于 readonly struct，`copy.Execute()` 中的 `copy` 是可变的。但 `Execute()` 是 `void Execute()`，没有 `readonly` 修饰。实际上接口方法没有 `readonly` 的概念。

   最好的方式 - 我们不使用 `in` 参数。把 `Send` 签名的 `in` 去掉？但 FRD 说保持签名一致。

   好吧，简单的方式：

   ```csharp
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public void Send<TCommand>(in TCommand command) where TCommand : struct, ICommand
   {
       var copy = command;
       copy.Execute();
   }
   ```

   这会有一次栈拷贝，但相比原来的 `SelfHandlingCommandCache` 和 handler 字典查找，这更简单且可预测。对 readonly struct 的拷贝是 O(size) 但通常很小。

   实际上，更好的方案是避免 `in` 参数带来的间接性。但用户要求 `in` 参数不变，所以我们这样做。

   Wait, 我再想一下。`ISelfHandlingCommand.Execute()` 在原来的代码中是通过 DynamicMethod 调用的。为什么不直接使用：
   ```csharp
   ((ICommand)command).Execute();
   ```
   这会装箱！不好。

   所以栈拷贝方案最安全：
   ```csharp
   var copy = command;  // 栈拷贝
   copy.Execute();      // constrained callvirt, 0 boxing
   ```

   For `Query()` 同理。对于 `IQuery<TResult>.Query()`：
   ```csharp
   public TResult Ask<TQuery, TResult>(in TQuery query) where TQuery : struct, IQuery<TResult>
   {
       var copy = query;
       return copy.Query();
   }
   ```

   对于 async：
   ```csharp
   public async UniTask SendAsync<TCommand>(TCommand command) where TCommand : struct, IAsyncCommand
   {
       await command.ExecuteAsync();
   }
   ```
   这里 `command` 不是 `in` 参数（从 FRD 看 `SendAsync` 是传值），所以不会装箱。

   ```csharp
   public async UniTask<TResult> AskAsync<TQuery, TResult>(TQuery query) where TQuery : struct, IAsyncQuery<TResult>
   {
       return await query.QueryAsync();
   }
   ```

   Publish 保持不变。

   好的，让我写出完整的 CqrsBus.cs。

   需要保留的：
   - Event 相关：`_eventHandlers`，`IEventHandlerList`，`EventHandlerList<TEvent>`，`Subscribe`（两个重载），`Unsubscribe`（两个重载），`Publish`
   - Logger：`_logger`，构造函数，`SafeInfo`，`ThrowIfValueTypeHandler`
   - 已删除 CQ 相关：全部删除

   好的，让我写出完整的文件。

```csharp
using System;
using System.Collections.Generic;
using Change.Framework.Collections;
using Change.Framework.Logging;
using Cysharp.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Change.Framework.Cqrs
{
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry, ICqrsRuntime
    {
        private interface IEventHandlerList { }

        private sealed class EventHandlerList<TEvent> : IEventHandlerList
            where TEvent : struct, IEvent
        {
            public FastList<IEventHandler<TEvent>> Handlers { get; } = new();
            public FastList<Action<TEvent>> Delegates { get; } = new();
        }

        private readonly FastDictionary<Type, IEventHandlerList> _eventHandlers = new();
        private readonly ILogger _logger;

        public CqrsBus() : this(NullLogger.Instance) { }

        public CqrsBus(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            var copy = command;
            copy.Execute();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult Ask<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            var copy = query;
            return copy.Query();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish<TEvent>(in TEvent @event)
            where TEvent : struct, IEvent
        {
            // ... 保持原有 Publish 实现不变
        }

        public async UniTask SendAsync<TCommand>(TCommand command)
            where TCommand : struct, IAsyncCommand
        {
            await command.ExecuteAsync();
        }

        public async UniTask<TResult> AskAsync<TQuery, TResult>(TQuery query)
            where TQuery : struct, IAsyncQuery<TResult>
        {
            return await query.QueryAsync();
        }

        // Subscribe/Unsubscribe 保持不变
        public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : struct, IEvent { ... }
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IEvent { ... }
        public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : struct, IEvent { ... }
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IEvent { ... }

        private void ThrowIfValueTypeHandler(object handler, string paramName) { ... }
        private void SafeInfo(string message) { ... }
    }
}
```

这个实现减少了约 300 行代码（从 ~700 行减少到约 ~250 行）。

注意：`in` 参数 + 栈拷贝是安全的 0 分配操作，因为 struct 的拷贝在栈上完成。`readonly struct` 的拷贝成本是固定的（等于 struct 大小），且对小型 struct 可忽略。

7. 对于 async 方法，`UniTask` 返回类型 `async UniTask` 和 `async UniTask<TResult>` 是标准模式。

现在，`Publish`、`Subscribe`、`Unsubscribe` 完全保持原有实现。`ThrowIfValueTypeHandler` 仍用于 Event 订阅。`SafeInfo` 保留。

好的，我需要在计划中写出完整的 CqrsBus.cs 代码。这比较长，但技能要求"每个代码步骤展示实际代码"。

实际上，我可以用 Edit 的方式展示变更，即说明要保留哪些部分、删除哪些部分。但为了精确起见，我会给出完整的代码。

不过我需要在计划中保持合理长度。让我用一种"已删除部分列出删除项，保留部分完整给出"的方式来写。

让我在计划中提供 CqrsBus.cs 的完整重写代码。<｜end▁of▁thinking｜>

- [ ] **步骤 1: 重写 CqrsBus.cs — 删除 Handler 逻辑，简化分发**

完整重写 `CqrsBus.cs`：

```csharp
using System;
using System.Collections.Generic;
using Change.Framework.Collections;
using Change.Framework.Logging;
using Cysharp.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Change.Framework.Cqrs
{
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry, ICqrsRuntime
    {
        private interface IEventHandlerList { }

        private sealed class EventHandlerList<TEvent> : IEventHandlerList
            where TEvent : struct, IEvent
        {
            public FastList<IEventHandler<TEvent>> Handlers { get; } = new();
            public FastList<Action<TEvent>> Delegates { get; } = new();
        }

        private readonly FastDictionary<Type, IEventHandlerList> _eventHandlers = new();
        private readonly ILogger _logger;

        public CqrsBus() : this(NullLogger.Instance) { }

        public CqrsBus(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            // 栈拷贝避免接口方法装箱（constrained callvirt via local variable）
            var cmd = command;
            cmd.Execute();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult Ask<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            var q = query;
            return q.Query();
        }

        // ===== Publish (unchanged from original) =====

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish<TEvent>(in TEvent @event)
            where TEvent : struct, IEvent
        {
            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
                return;

            var typedList = (EventHandlerList<TEvent>)handlerList;
            var handlers = typedList.Handlers;
            var delegates = typedList.Delegates;
            var handlerCount = handlers.Count;
            var delegateCount = delegates.Count;
            if (handlerCount == 0 && delegateCount == 0)
                return;

            List<Exception> exceptions = null;

            for (var i = 0; i < handlerCount; i++)
            {
                try { handlers[i].Handle(in @event); }
                catch (Exception ex) { exceptions ??= new List<Exception>(); exceptions.Add(ex); }
            }

            for (var i = 0; i < delegateCount; i++)
            {
                try { delegates[i](@event); }
                catch (Exception ex) { exceptions ??= new List<Exception>(); exceptions.Add(ex); }
            }

            if (exceptions != null) throw new AggregateException(exceptions);
        }

        // ===== Async =====

        public async UniTask SendAsync<TCommand>(TCommand command)
            where TCommand : struct, IAsyncCommand
        {
            await command.ExecuteAsync();
        }

        public async UniTask<TResult> AskAsync<TQuery, TResult>(TQuery query)
            where TQuery : struct, IAsyncQuery<TResult>
        {
            return await query.QueryAsync();
        }

        // ===== Event Subscribe / Unsubscribe (unchanged) =====

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            ThrowIfValueTypeHandler(handler, nameof(handler));
            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                handlerList = new EventHandlerList<TEvent>();
                _eventHandlers.TryAdd(eventType, handlerList);
            }
            ((EventHandlerList<TEvent>)handlerList).Handlers.Add(handler);
            SafeInfo("Subscribed event handler.");
        }

        public void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (handler.Target != null)
                throw new ClosureCaptureException(
                    "Delegate captures variables; only static methods or non-capturing lambdas "
                    + "are allowed to maintain the 0-GC guarantee. "
                    + $"Delegate type: {handler.GetType().FullName}, Target: {handler.Target.GetType().FullName}.");
            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                handlerList = new EventHandlerList<TEvent>();
                _eventHandlers.TryAdd(eventType, handlerList);
            }
            ((EventHandlerList<TEvent>)handlerList).Delegates.Add(handler);
            SafeInfo("Subscribed event delegate.");
        }

        public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!_eventHandlers.TryGetValue(typeof(TEvent), out var handlerList)) return;
            var typedList = (EventHandlerList<TEvent>)handlerList;
            var idx = typedList.Handlers.IndexOf(handler);
            if (idx >= 0) typedList.Handlers.RemoveAt(idx);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!_eventHandlers.TryGetValue(typeof(TEvent), out var handlerList)) return;
            var typedList = (EventHandlerList<TEvent>)handlerList;
            var idx = typedList.Delegates.IndexOf(handler);
            if (idx >= 0) typedList.Delegates.RemoveAt(idx);
        }

        // ===== Helpers =====

        private static void ThrowIfValueTypeHandler(object handler, string paramName)
        {
            if (handler.GetType().IsValueType)
                throw new InvalidOperationException(
                    $"Value-type handlers are not supported: {paramName} must be implemented by a class.");
        }

        private void SafeInfo(string message)
        {
            try { _logger.Info(message); }
            catch (Exception ex)
            {
#if UNITY_EDITOR || DEBUG
                System.Diagnostics.Debug.WriteLine($"[CqrsBus] Logger error: {ex.Message}");
#endif
            }
        }
    }
}
```

删除内容清单（原文件中删除的部分）：
- `_commandHandlers`, `_queryHandlers`, `_queryResultByQueryType`, `_asyncCommandHandlers`, `_asyncQueryHandlers` 字段
- `ICommandHandlerRegistration`, `IQueryHandlerRegistration`, `IAsyncCommandHandlerRegistration`, `IAsyncQueryHandlerRegistration` 接口
- `CommandHandlerRegistration`, `QueryHandlerRegistration`, `AsyncCommandHandlerRegistration`, `AsyncQueryHandlerRegistration` 类
- `SelfHandlingCommandCache`, `SelfHandlingQueryCache`, `SelfHandlingQueryInvokeCache` 类
- `RegisterCommand`, `RegisterQuery`, `RegisterAsyncCommand`, `RegisterAsyncQuery`, `UnregisterCommand`, `UnregisterQuery` 方法
- `Query<TQuery,TResult>` 方法
- `ResetIfPoolable` 方法

- [ ] **步骤 2: 编译检查**

```bash
cd UnityProject && dotnet build 2>&1 | grep -E "error CS|Build FAILED" | head -20
```
预期：编译错误来自测试文件和 GameScript 代码，它们仍引用旧接口。这是预期的。

- [ ] **步骤 3: Commit**

```bash
git add -A
git commit -m "refactor(cqrs): simplify CqrsBus - remove handler dispatch, direct Execute/Query calls
- Send/Ask: direct struct.Execute()/Query() calls with stack copy (0-boxing)
- SendAsync/AskAsync: return UniTask, call IAsyncCommand/IAsyncQuery
- Remove: handler dictionaries, SelfHandlingCache*, DynamicMethod, ResetIfPoolable
- Remove: RegisterCommand, RegisterQuery, RegisterAsyncCommand, RegisterAsyncQuery
- Remove: Query() method (use Ask())
- Keep: Event Subscribe/Publish/Unsubscribe unchanged
- Reduced ~700 lines to ~250 lines"
```

### 任务 4: 简化 CqrsBootstrap + 清理异常

**覆盖的上游需求：** FRD 决策 #5（删除注册 API）

**依赖：** 任务 3

**文件：**
- 修改：`CqrsBootstrap.cs`
- 删除：`ModeConflictException.cs`
- 删除：`DuplicateRegistrationException.cs`
- 删除：`HandlerNotRegisteredException.cs`
- 保留：`ClosureCaptureException.cs`

- [ ] **步骤 1: 简化 CqrsBootstrap.cs**

```csharp
using System;

namespace Change.Framework.Cqrs
{
    public sealed class CqrsBootstrap : ICqrsBootstrap
    {
        private readonly CqrsBus _bus;
        private readonly object _buildGate = new();
        private volatile ICqrsRuntime _runtime;

        public CqrsBootstrap() : this(new CqrsBus()) { }

        public CqrsBootstrap(CqrsBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            _bus.Subscribe(handler);
        }

        public void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent
        {
            _bus.Subscribe(handler);
        }

        public ICqrsRuntime Build()
        {
            var runtime = _runtime;
            if (runtime != null) return runtime;

            lock (_buildGate)
            {
                if (_runtime != null) return _runtime;
                _runtime = _bus;
                return _runtime;
            }
        }
    }
}
```

变更说明：删除了 `RegisterCommand`、`RegisterQuery`、`RegisterAsyncCommand`、`RegisterAsyncQuery`。

- [ ] **步骤 2: 删除异常文件**

```bash
git rm UnityProject/Assets/Change/Framework/Cqrs/Exceptions/ModeConflictException.cs
git rm UnityProject/Assets/Change/Framework/Cqrs/Exceptions/DuplicateRegistrationException.cs
git rm UnityProject/Assets/Change/Framework/Cqrs/Exceptions/HandlerNotRegisteredException.cs
```
删除对应的 `.meta` 文件。确认 `ClosureCaptureException.cs` 保留。

- [ ] **步骤 3: 编译检查**

```bash
cd UnityProject && dotnet build 2>&1 | grep -E "error CS|Build FAILED" | head -10
```
预期：仍有编译错误（测试文件引用旧类型）。

- [ ] **步骤 4: Commit**

```bash
git add -A
git commit -m "refactor(cqrs): simplify CqrsBootstrap, delete unused exceptions
- CqrsBootstrap: remove RegisterCommand/RegisterQuery/RegisterAsyncCommand/RegisterAsyncQuery
- Delete: ModeConflictException, DuplicateRegistrationException, HandlerNotRegisteredException
- Keep: ClosureCaptureException (still used by event delegate detection)"
```

### 任务 5: 重写框架测试 — Dispatch 测试

**覆盖的上游需求：** FRD 验收条件 1-6（所有框架测试通过）

**依赖：** 任务 4

**文件：**
- 重写：`CommandDispatchTests.cs`
- 重写：`QueryDispatchTests.cs`
- 重写：`AsyncDispatchTests.cs`
- 删除：`SelfHandlingDispatchTests.cs`
- 删除：`ModeConflictTests.cs`

- [ ] **步骤 1: 重写 CommandDispatchTests.cs**

```csharp
using System;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class CommandDispatchTests
    {
        private readonly struct IncrementCounterCommand : ICommand
        {
            private readonly CounterState _state;
            private readonly int _amount;

            public IncrementCounterCommand(CounterState state, int amount)
            {
                _state = state;
                _amount = amount;
            }

            public void Execute()
            {
                _state.Value += _amount;
            }
        }

        private sealed class CounterState
        {
            public int Value;
        }

        [Test]
        public void Send_ExecutesSelfHandlingCommand()
        {
            var state = new CounterState();
            var bus = new CqrsBus();

            bus.Send(new IncrementCounterCommand(state, 3));

            Assert.AreEqual(3, state.Value);
        }

        [Test]
        public void SelfHandling_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var counter = new CounterState();
            var bus = new CqrsBus();
            var command = new IncrementCounterCommand(counter, 1);

            for (var i = 0; i < 1000; i++) bus.Send(command);

            ForceFullGc();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100000; i++) bus.Send(command);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
            Assert.AreEqual(101000, counter.Value);
        }

        private static void ForceFullGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
```

变更说明：
- 不再有单独注册的 `ICommandHandler`，command struct 自己携带状态
- 保持了 0GC 热路径测试（从 SelfHandlingDispatchTests 并入）
- 删除了 `RegisterCommand_NullHandler_ThrowsArgumentNullException`、`RegisterCommand_StructHandler_ThrowsInvalidOperationException` 等不再适用的测试
- 删除了 `RegisterCommand_DuplicateRegistration_ThrowsDuplicateRegistrationException`
- 删除了 `Send_WithoutRegistration_ThrowsHandlerNotRegisteredException`（不再需要注册）

- [ ] **步骤 2: 重写 QueryDispatchTests.cs**

```csharp
using System;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class QueryDispatchTests
    {
        private readonly struct GetScoreQuery : IQuery<int>
        {
            private readonly ScoreState _state;

            public GetScoreQuery(ScoreState state) { _state = state; }

            public int Query() => _state.Value;
        }

        private sealed class ScoreState
        {
            public int Value;
        }

        [Test]
        public void Ask_ReturnsFromSelfHandlingQuery()
        {
            var state = new ScoreState { Value = 27 };
            var bus = new CqrsBus();

            var result = bus.Ask<GetScoreQuery, int>(new GetScoreQuery(state));

            Assert.AreEqual(27, result);
        }

        [Test]
        public void SelfHandling_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var state = new ScoreState { Value = 3 };
            var bus = new CqrsBus();
            var query = new GetScoreQuery(state);

            for (var i = 0; i < 1000; i++) bus.Ask<GetScoreQuery, int>(query);

            ForceFullGc();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var sum = 0;
            for (var i = 0; i < 100000; i++) sum += bus.Ask<GetScoreQuery, int>(query);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
            Assert.AreEqual(3 * 100000, sum);
        }

        private static void ForceFullGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
```

- [ ] **步骤 3: 重写 AsyncDispatchTests.cs**

```csharp
using Cysharp.Threading.Tasks;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class AsyncDispatchTests
    {
        private readonly struct AsyncCommand : IAsyncCommand
        {
            private readonly int _value;
            public int Captured { get; private set; }

            public AsyncCommand(int value) : this()
            {
                _value = value;
            }

            public UniTask ExecuteAsync()
            {
                Captured = _value;
                return UniTask.CompletedTask;
            }
        }

        [Test]
        public void SendAsync_ExecutesSelfHandlingCommand()
        {
            var bus = new CqrsBus();
            var cmd = new AsyncCommand(11);

            bus.SendAsync(cmd).GetAwaiter().GetResult();

            Assert.AreEqual(11, cmd.Captured);
        }

        private readonly struct AsyncQuery : IAsyncQuery<int>
        {
            private readonly int _seed;

            public AsyncQuery(int seed) { _seed = seed; }

            public UniTask<int> QueryAsync() => UniTask.FromResult(_seed * 3);
        }

        [Test]
        public void AskAsync_ReturnsResult()
        {
            var bus = new CqrsBus();

            var result = bus.AskAsync<AsyncQuery, int>(new AsyncQuery(4)).GetAwaiter().GetResult();

            Assert.AreEqual(12, result);
        }

        [Test]
        public void AsyncHotPath_AllocatesZeroBytes()
        {
            var bus = new CqrsBus();
            var cmd = new AsyncCommand(1);

            for (var i = 0; i < 1000; i++) bus.SendAsync(cmd).GetAwaiter().GetResult();

            ForceFullGc();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100000; i++) bus.SendAsync(cmd).GetAwaiter().GetResult();
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
        }

        private static void ForceFullGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
```

- [ ] **步骤 4: 删除旧测试文件**

```bash
git rm UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/SelfHandlingDispatchTests.cs
git rm UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ModeConflictTests.cs
```
删除对应 `.meta`。

- [ ] **步骤 5: 编译 + 运行测试**

```bash
cd UnityProject && dotnet build 2>&1 | grep -E "error CS|Build FAILED" | head -10
# 如编译通过，运行新测试
dotnet test --filter "FullyQualifiedName~CommandDispatchTests|FullyQualifiedName~QueryDispatchTests|FullyQualifiedName~AsyncDispatchTests" 2>&1 | tail -30
```

- [ ] **步骤 6: Commit**

```bash
git add -A
git commit -m "test(cqrs): rewrite dispatch tests for self-handling commands/queries
- CommandDispatchTests: self-handling command with Execute(), 0GC hot path test
- QueryDispatchTests: self-handling query with Query(), 0GC hot path test
- AsyncDispatchTests: IAsyncCommand/IAsyncQuery with UniTask, 0GC test
- Delete: SelfHandlingDispatchTests.cs, ModeConflictTests.cs"
```

### 任务 6: 重写框架测试 — Runtime + 性能 + 架构测试

**覆盖的上游需求：** FRD 验收条件（所有框架测试通过）

**依赖：** 任务 5

**文件：**
- 大幅缩减：`RuntimeRegistrationTests.cs`（只保留 Event 部分）
- 重写：`ZeroAllocationDispatchTests.cs`
- 修改：`CqrsArchitectureGuardTests.cs`
- 修改：`CqrsBootstrapLifecycleTests.cs`
- 删除/保留：`CqrsLoggingIntegrationTests.cs`
- 删除：`PoolingResetTests.cs`

- [ ] **步骤 1: 缩减 RuntimeRegistrationTests.cs**

只保留 Event 订阅/取消订阅测试（原文件的 `Subscribe_DuplicateSubscription_AppendsSuccessfully`, `Unsubscribe_RemovesSpecificHandler`, `Unsubscribe_WhenNotSubscribed_IsIdempotent`, `Unsubscribe_WithNullHandler_ThrowsArgumentNullException` 等），删除所有 CQ 注册/取消注册测试。

- [ ] **步骤 2: 重写 ZeroAllocationDispatchTests.cs**

使用自处理 Command/Query 代替 Handler 注册模式。保留 Publish/Delegate 测试不变。

```csharp
using System;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class ZeroAllocationDispatchTests
    {
        private const int WarmupIterations = 1000;
        private const int MeasuredIterations = 100000;

        private readonly struct TickCommand : ICommand
        {
            private readonly TickState _state;
            private readonly int _delta;
            public TickCommand(TickState state, int delta) { _state = state; _delta = delta; }
            public void Execute() { _state.Value += _delta; }
        }

        private readonly struct GetTickQuery : IQuery<int>
        {
            private readonly TickState _state;
            public GetTickQuery(TickState state) { _state = state; }
            public int Query() => _state.Value;
        }

        private readonly struct TickDomainEvent : IEvent
        {
            public int Delta { get; }
            public TickDomainEvent(int delta) { Delta = delta; }
        }

        private sealed class TickState { public int Value; }

        private sealed class TickDomainEventHandler : IEventHandler<TickDomainEvent>
        {
            public int Count;
            public void Handle(in TickDomainEvent e) { Count += e.Delta; }
        }

        private static int _sStaticTickCount;
        private static void StaticTickHandler(TickDomainEvent e) { _sStaticTickCount += e.Delta; }

        [Test]
        public void Send_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var state = new TickState();
            var bus = new CqrsBus();
            var command = new TickCommand(state, 1);

            for (var i = 0; i < WarmupIterations; i++) bus.Send(command);
            ForceFullGc();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < MeasuredIterations; i++) bus.Send(command);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
            Assert.AreEqual(WarmupIterations + MeasuredIterations, state.Value);
        }

        [Test]
        public void Query_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var state = new TickState { Value = 7 };
            var bus = new CqrsBus();
            var query = new GetTickQuery(state);

            for (var i = 0; i < WarmupIterations; i++) bus.Ask<GetTickQuery, int>(query);
            ForceFullGc();

            var before = GC.GetAllocatedBytesForCurrentThread();
            var sum = 0;
            for (var i = 0; i < MeasuredIterations; i++) sum += bus.Ask<GetTickQuery, int>(query);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
            Assert.AreEqual(7 * MeasuredIterations, sum);
        }

        // Publish_Delegate tests remain unchanged from original
        [Test]
        public void Publish_HotPath_AllocatesZeroBytesAfterWarmup() { /* same as original */ }

        [Test]
        public void Publish_DelegateHotPath_AllocatesZeroBytesAfterWarmup() { /* same as original */ }
    }
}
```

- [ ] **步骤 3: 修改 CqrsArchitectureGuardTests.cs**

修改 `ImplementsQueryHandler` 方法，将 `IQueryHandler<,>` 替换为... 实际上，由于 `IQueryHandler<,>` 已被删除，这个方法应该检查 `IQuery<>` 接口吗？不对 —— 架构守卫测试是要检查 Handler 类的依赖注入。

由于 Handler 类已删除，这个测试不再有意义。删除 `QueryHandlers_MustNotInjectWriteSideOrDispatchSurface` 测试方法，以及相关的辅助方法（`GetQueryHandlerAssemblies`, `ContainsConcreteQueryHandler`, `GetConcreteQueryHandlerTypes`, `ImplementsQueryHandler`, `ForbiddenQueryHandlerDependencies`, `IsForbiddenQueryHandlerDependency`）。

保留 `DomainEventHandlers_MustNotInjectRuntimeOrCommandDispatchSurface` 测试（它检查 `IEventHandler<>`）。

- [ ] **步骤 4: 修改 CqrsBootstrapLifecycleTests.cs**

删除所有注册（RegisterCommand/RegisterQuery）相关的测试，保留：
- `Build_CalledTwice_ReturnsSameRuntimeInstance`
- `Build_FromMultipleThreadsConcurrently_ReturnsSameRuntimeInstance`
- `Bootstrap_DoesNotExposePublicRuntimeProperty`
- `Bootstrap_DoesNotExposePublicFreezeMethod`
- 一个简化的 `Runtime_SupportsQueryAndPublish_HappyPath` 改用自处理 Query

- [ ] **步骤 5: 处理 CqrsLoggingIntegrationTests.cs**

原测试只测试注册日志。由于注册 API 已删除，此文件测试用例全部失效。根据情况：
- 如果认为日志功能仍需测试（Event Subscribe 等），改写为测试 Subscribe 日志
- 否则删除此文件

建议：删除此文件，因为 Event Subscribe 的日志由 EventDispatchTests 间接覆盖。

- [ ] **步骤 6: 删除 PoolingResetTests.cs**

```bash
git rm UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/PoolingResetTests.cs
```
删除对应 `.meta`。

- [ ] **步骤 7: 编译 + 运行测试**

```bash
cd UnityProject && dotnet build 2>&1 | grep -E "error CS|Build FAILED" | head -10
dotnet test --filter "FullyQualifiedName~Cqrs" 2>&1 | tail -20
```

- [ ] **步骤 8: Commit**

```bash
git add -A
git commit -m "test(cqrs): rewrite runtime/performance/architecture tests for self-handling
- RuntimeRegistrationTests: retain only event subscribe/unsubscribe tests
- ZeroAllocationDispatchTests: self-handling command/query, retain event publish tests
- CqrsArchitectureGuardTests: remove query handler dependency checks
- CqrsBootstrapLifecycleTests: remove CQ registration tests, retain build/event tests
- Delete: PoolingResetTests.cs, CqrsLoggingIntegrationTests.cs"
```

### 任务 7: 迁移 GameScript 使用代码

**覆盖的上游需求：** FRD 所有验收条件（业务代码迁移）

**依赖：** 任务 4

**文件：**
- 重写：`QuestCommandHandlers.cs`
- 重写：`QuestQueryHandlers.cs`
- 修改：`QuestMessages.cs`
- 修改：`GameHotfixInstaller.cs`

> **迁移策略：** 将 Handler 类的 `Handle()` 逻辑移到对应 Command/Query struct 的 `Execute()`/`Query()` 方法中。Handler 类通过构造函数注入的服务依赖改为 Command/Query struct 的构造函数字段注入。

- [ ] **步骤 1: 修改 QuestMessages.cs — 添加 Execute/Query 方法**

为每个 Command struct 添加 `Execute()` 方法，为 Query struct 添加 `Query()` 方法。需要注入的依赖通过构造函数字段传入。

```csharp
// 在 BumpMainQuestProgressCommand 中添加：
public void Execute() { }  // 暂时留空（Handler 逻辑在步骤 2 迁移）

// 以此类推为所有 Command/Query struct 添加方法签名
```

注意：因为 Handler 逻辑中引用了 `QuestSessionState` 和 `QuestRewardWallet`，这些依赖需要作为 struct 字段注入。即每个 Command struct 需要增加对应的字段。

```csharp
public readonly struct BumpMainQuestProgressCommand : ICommand
{
    private readonly QuestSessionState _state;
    public int Delta { get; }

    public BumpMainQuestProgressCommand(QuestSessionState state, int delta)
    {
        _state = state;
        Delta = delta;
    }

    public void Execute() => _state.BumpMainProgress(Delta);
}

public readonly struct AdvanceMainQuestStepCommand : ICommand
{
    private readonly QuestSessionState _state;
    private readonly QuestRewardWallet _wallet;

    public AdvanceMainQuestStepCommand(QuestSessionState state, QuestRewardWallet wallet)
    {
        _state = state;
        _wallet = wallet;
    }

    public void Execute()
    {
        _state.CompleteMainIfReady();
        _wallet.AddGold(20);
    }
}
// ... 对其他 Command 和 Query 做同样处理
```

完整迁移：

| 原 Handler | struct 新增字段 | Execute/Query 逻辑 |
|---|---|---|
| `BumpMainQuestProgressHandler` | `QuestSessionState _state` | `_state.BumpMainProgress(Delta)` |
| `AdvanceMainQuestStepHandler` | `QuestSessionState _state, QuestRewardWallet _wallet` | `_state.CompleteMainIfReady(); _wallet.AddGold(20)` |
| `BumpSideQuestProgressHandler` | `QuestSessionState _state` | 从 Handler 原文复制 |
| `ClaimSideQuestRewardHandler` | `QuestSessionState _state, QuestRewardWallet _wallet` | 从 Handler 原文复制 |
| `BumpDailyQuestProgressHandler` | `QuestSessionState _state` | 从 Handler 原文复制 |
| `ClaimDailyQuestRewardHandler` | `QuestSessionState _state, QuestRewardWallet _wallet` | 从 Handler 原文复制 |
| `GetQuestPanelQueryHandler` | `QuestSessionState _state, QuestRewardWallet _wallet` | 从 Handler 原文复制 |

- [ ] **步骤 2: 重写 QuestCommandHandlers.cs — 删除 Handler 类**

Handler 的逻辑已全部迁移到 struct 的 `Execute()` 方法中。此文件可以删除，或改为空的命名空间声明。建议直接删除。

```bash
git rm UnityProject/Assets/GameScript/UI/Quest/QuestCommandHandlers.cs
```

- [ ] **步骤 3: 重写 QuestQueryHandlers.cs — 删除 Handler 类**

Handler 逻辑已迁移到 Query struct 的 `Query()` 方法中。此文件可以删除。

```bash
git rm UnityProject/Assets/GameScript/UI/Quest/QuestQueryHandlers.cs
```

- [ ] **步骤 4: 修改 GameHotfixInstaller.cs — 删除 Handler 注册**

```csharp
using Change.Framework.Cqrs;
using Change.Runtime.Composition;
using VContainer;

namespace GameScript.Composition
{
    public sealed class GameHotfixInstaller : IHotfixGameInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<QuestSessionState>(Lifetime.Singleton);
            builder.Register<QuestRewardWallet>(Lifetime.Singleton);
            builder.Register<CqrsBus>(Lifetime.Singleton);
            builder.Register<ICqrsBus>(c => c.Resolve<CqrsBus>(), Lifetime.Singleton);
            builder.Register<IOpenQuestPanelUseCase, OpenQuestPanelUseCase>(Lifetime.Transient);

            // Handler 注册已删除 — Command/Query 是自处理 struct
            // 不需要通过 Bootstrap 注册
            // CqrsBus 无需 Bootstrap，直接注入即可
        }
    }
}
```

- [ ] **步骤 5: 编译检查**

```bash
cd UnityProject && dotnet build 2>&1 | grep -E "error CS|Build FAILED" | head -10
```

- [ ] **步骤 6: Commit**

```bash
git add -A
git commit -m "refactor(quest): migrate quest handlers to self-handling structs
- Move handler logic into Command/Query struct Execute()/Query() methods
- Struct dependencies injected via constructor fields
- Delete: QuestCommandHandlers.cs, QuestQueryHandlers.cs
- GameHotfixInstaller: remove handler registration, bootstrap no longer needed for CQ"
```

### 任务 8: 更新 GameScript 测试

**覆盖的上游需求：** FRD 验收条件（业务测试通过）

**依赖：** 任务 7

**文件：**
- 修改：`QuestFairyGuiPlayModeTests.cs`
- 修改：`QuestSessionStateTests.cs`

- [ ] **步骤 1: 修改 QuestFairyGuiPlayModeTests.cs**

`CreateBus` 方法中删除 Bootstrap 注册，因为 Command 变成了自处理 struct，不再需要注册。

```csharp
private static ICqrsBus CreateBus(out QuestSessionState state, out QuestRewardWallet wallet)
{
    state = new QuestSessionState();
    wallet = new QuestRewardWallet();
    return new CqrsBus();
}
```

`bus.Send()` 调用需要传入带依赖的 struct 实例。由于 `BumpSideQuestProgressCommand` 和 `ClaimSideQuestRewardCommand` 现在需要 `QuestSessionState`，调用方需要提供。但原测试直接调用 `bus.Send(command)` 使用的是无参构造函数，现在需要改为带依赖的构造。

```csharp
// 原代码：
bus.Send(new BumpSideQuestProgressCommand(1, 3));
// 新代码（struct 需要 session state 参数）：
bus.Send(new BumpSideQuestProgressCommand(state, 1, 3));
```

- [ ] **步骤 2: 修改 QuestSessionStateTests.cs**

与步骤 1 相同的 `CreateBus` 修改。所有 `bus.Send(new SomeCommand(...))` 调用需要添加 `state`/`wallet` 参数。

- [ ] **步骤 3: 编译 + 运行测试**

```bash
cd UnityProject && dotnet build 2>&1 | grep -E "error CS|Build FAILED" | head -10
dotnet test --filter "FullyQualifiedName~QuestSessionStateTests" 2>&1 | tail -20
dotnet test --filter "FullyQualifiedName~QuestFairyGuiPlayModeTests" 2>&1 | tail -20
```

- [ ] **步骤 4: Commit**

```bash
git add -A
git commit -m "test(quest): update tests for self-handling quest command/query structs
- CreateBus: remove bootstrap registration (no longer needed)
- Command structs now require state/wallet as constructor parameters"
```

### 任务 9: 全局验证 + 清理

**覆盖的上游需求：** FRD 全部验收条件

**依赖：** 任务 5, 6, 7, 8

- [ ] **步骤 1: 全局编译检查**

```bash
cd UnityProject && dotnet build 2>&1 | tail -20
```
预期：Build succeeded。

- [ ] **步骤 2: 运行全部 CQRS 相关测试**

```bash
dotnet test --filter "FullyQualifiedName~Cqrs|FullyQualifiedName~Quest" 2>&1 | tail -30
```

- [ ] **步骤 3: 检查编译产物中无引用已删除类型**

```bash
grep -r "ModeConflictException\|HandlerNotRegisteredException\|DuplicateRegistrationException" /Users/andy/Workspace/github/andycai/fun/UnityProject/Assets --include="*.cs" 2>/dev/null | grep -v "superseded" | head -5
```
预期：无输出（所有引用已清理）。

- [ ] **步骤 4: 检查文档目录引用**

检查 `Cqrs/Documentation/` 目录中是否有引用已删除接口的文档，需要更新。

- [ ] **步骤 5: 最终 Commit**

```bash
git add -A
git commit -m "chore(cqrs): final cleanup - remove stale references, verify build and tests"
```

- [ ] **步骤 6: 更新 plans/README.md**

```markdown
# CQRS 模块计划总览

## FRD → Plan 映射

| FRD | Plan 文档 | 任务数 | 依赖 | 状态 |
|-----|----------|--------|------|------|
| [接口层简化与运行时注册](../discover/2026-06-18-framework-cqrs-interface-runtime-registration-frd.md) | — | — | 无 | 待计划 |
| [自处理 Command/Query](../discover/2026-06-21-framework-cqrs-self-handling-command-query-frd.md) | [实现计划](./2026-06-21-framework-cqrs-self-handling-command-query-plan.md) | 9 | #1 | 待开始 |
| [Event 双模式订阅](../discover/2026-06-18-framework-cqrs-dual-mode-event-frd.md) | — | — | #1 | 待计划 |
| [性能验证与迁移](../discover/2026-06-18-framework-cqrs-performance-migration-frd.md) | — | — | #1, #2, #3 | 待计划 |
```
