# CQRS 双模式 Command/Query 实现计划

> 日期: 2026-06-19 | 状态: 草稿
> 上游设计: [2026-06-18-framework-cqrs-dual-mode-command-query-design.md](../designs/2026-06-18-framework-cqrs-dual-mode-command-query-design.md)
> 上游 FRD: [2026-06-18-framework-cqrs-dual-mode-command-query-frd.md](../discover/2026-06-18-framework-cqrs-dual-mode-command-query-frd.md)

## 上游产出引用

### 来自 Design

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| Slice A: Struct 自处理模式 | 任务 A1-A3 |
| Slice B: 对象池集成（IPoolable → Reset） | 任务 B1-B2 |
| Slice C: 异步支持（仅 Class 模式） | 任务 C1-C3 |
| Slice D: 冲突检测 + 文档 | 任务 D1-D2 |
| 文件地图：每个文件所属切片 | 见下方「文件清单」 |
| 架构决策：接口 + 运行时检测 | 任务 A1 实现 `ISelfHandlingCommand` 缓存检测 |
| 架构决策：Reset 在 finally 块 | 任务 B1 实现 try/finally + Reset |
| 性能目标：Struct 热路径 0GC | 任务 A3 0GC 验证测试 |

### 来自 FRD

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 验收条件：Struct 自处理无需注册即可分发 | 任务 A1 测试 |
| 验收条件：严格 0 字节 GC（热路径） | 任务 A3、B2 0GC 回归测试 |
| 验收条件：异步 Handler 正常工作 | 任务 C2、C3 |
| 验收条件：Class/Struct 模式冲突检测 | 任务 D1 |
| 决策 #3：异步仅 Class 模式 | 任务 C2 SendAsync 拒绝 self-handling |
| 决策 #5：框架负责 Reset，不管理池生命周期 | 任务 B1 finally 仅调 Reset，不归还池 |

## 目标

为 `Change.Framework.Cqrs` 增加 Struct 自处理模式（0GC 高频路径）、Class Handler 的对象池 Reset 集成、Class 模式异步分发，以及 Class/Struct 模式冲突检测，使框架同时满足复杂业务（DI + 池化 + 异步）与高频战斗（0GC 自处理）两类场景。

## 架构

沿用现有 `CqrsBus`（实现 `ICqrsBus`/`ICqrsRegistry`/`ICqrsRuntime`/`ICqrsBootstrap` 链路）。Struct 自处理通过新增 `ISelfHandlingCommand`/`ISelfHandlingQuery<TResult>` 接口 + 每类型缓存的 `Delegate.CreateDelegate` 约束调用实现，**热路径 0 装箱、0GC**。Class 池化通过 `Handle()` 后 finally 块检测 `IPoolable` 调 `Reset()`。异步通过独立注册表 + `SendAsync`/`AskAsync` 分发。

## ⚠️ 关键设计修正（已在自检中确认）

设计文档的行为契约伪代码使用 `HasExecuteMethod<T>() + command.Execute()`（在 `TCommand : ICommand` 约束下）——**此写法会装箱 struct，无法满足 0GC 验收条件**。本计划采用以下修正（仍满足设计决策 #4「编译时接口约束 + 运行时检测」与 0GC 目标）：

1. **不修改** `ICommand`/`IQuery<TResult>` 标记接口，改为**新增** `ISelfHandlingCommand`/`ISelfHandlingQuery<TResult>` 接口。
2. 运行时检测通过每类型静态缓存 `SelfHandlingCommandCache<TCommand>.Invoke`（`Delegate.CreateDelegate` 构建的开放实例委托，AOT/IL2CPP 安全，热路径零装箱）。
3. 冲突检测**权威放在注册时**（注册 self-handling 类型抛 `ModeConflictException`），而非分发时——因为注册拒绝使「同类型同时有 Handler 和 Execute」在结构上不可达，分发时只需 self-handling → `Execute()`，否则查 Handler。这比设计矩阵中「分发时双重检测」更健壮，且测试覆盖等价（注册时覆盖即阻止混合态）。

其余设计契约（Reset 时机、异步范围、API 表面、文件归属）完全遵循。

## 技术栈

- C# 9（Unity 2022.3.60f1 默认）
- `System.Threading.Tasks.Task`（框架层零外部依赖原则——**不使用 UniTask**，UniTask 属 Change.Runtime 引擎层）
- `Change.Framework.Pooling.IPoolable`、`Change.Framework.Logging.ILogger`
- 测试：NUnit（`Change.Framework.EditModeTests` 程序集，namespace `Change.Framework.Tests`）

## 文件清单

| 文件 | 操作 | 职责 | 所属任务 |
|------|------|------|----------|
| `Abstractions/ISelfHandlingCommand.cs` | 创建 | 自处理 Command 接口，`void Execute()` | A1 |
| `Abstractions/ISelfHandlingQuery.cs` | 创建 | 自处理 Query 接口，`TResult Execute()` | A2 |
| `Core/CqrsBus.cs` | 修改 | self-handling 缓存与分发；finally Reset；异步注册表与分发；冲突守卫 | A1,A2,B1,B2,C1,C2,C3,D1 |
| `Abstractions/IAsyncCommandHandler.cs` | 创建 | `Task ExecuteAsync(TCommand)` | C1 |
| `Abstractions/IAsyncQueryHandler.cs` | 创建 | `Task<TResult> ExecuteAsync(TQuery)` | C1 |
| `Abstractions/ICqrsBus.cs` | 修改 | `SendAsync`/`AskAsync` 签名 | C1 |
| `Abstractions/ICqrsRuntime.cs` | 修改 | `SendAsync`/`AskAsync` 签名（使 `Build()` 路径可用） | C1 |
| `Abstractions/ICqrsRegistry.cs` | 修改 | `RegisterAsyncCommand`/`RegisterAsyncQuery` 签名 | C1 |
| `Abstractions/ICqrsBootstrap.cs` | 修改 | `RegisterAsyncCommand`/`RegisterAsyncQuery` 签名（bootstrap 对等） | C1 |
| `Core/CqrsBootstrap.cs` | 修改 | 异步注册透传 | C1 |
| `Exceptions/ModeConflictException.cs` | 创建 | 模式冲突异常 | D1 |
| `Tests/EditMode/Cqrs/SelfHandlingDispatchTests.cs` | 创建 | self-handling 分发 + 0GC | A1,A2,A3 |
| `Tests/EditMode/Cqrs/PoolingResetTests.cs` | 创建 | Reset 调用与异常隔离 + 0GC 回归 | B1,B2 |
| `Tests/EditMode/Cqrs/AsyncDispatchTests.cs` | 创建 | 异步分发 + Reset + self-handling 拒绝 | C2,C3 |
| `Tests/EditMode/Cqrs/ModeConflictTests.cs` | 创建 | 注册时冲突守卫 | D1 |
| `Documentation/CQRS-DualMode-Guide.md` | 创建 | 双模式使用指南 | D2 |

**说明：** 相对设计文件地图的扩充——异步 API 同时加入 `ICqrsRuntime`/`ICqrsRegistry`/`ICqrsBootstrap` 与 `CqrsBootstrap`，否则 `bootstrap.Build()` 返回的 `ICqrsRuntime` 无法访问异步分发，且无注册入口。

## 任务依赖图

```
任务 A1 (self-handling command) ──┐
任务 A2 (self-handling query)  ──┤
任务 B1 (sync Reset) ─────────┬──┤
任务 B2 (query Reset + 异常隔离) ┘  │
                                   ├── 任务 D1 (冲突守卫) ── 任务 D2 (文档)
任务 C1 (异步接口面) ── 任务 C2 (SendAsync) ── 任务 C3 (AskAsync) ┘
```

- A1、A2、B1、B2 互相独立（可并行）；计划按线性顺序编排：A1→A2→A3→B1→B2→C1→C2→C3→D1→D2
- C1-C3 依赖 B（异步 finally 复用 Reset 模式）
- D1 依赖 A、C（守卫需 self-handling 缓存与异步注册已就绪）

## 风险评估

| 风险（来源） | 影响 | 计划中的缓解措施 |
|-------------|------|-----------------|
| self-handling 分发装箱（Design Slice A 伪代码会装箱） | 0GC 验收失败 | 任务 A1 改用 `Delegate.CreateDelegate` 约束调用；任务 A3 用 `GC.GetAllocatedBytesForCurrentThread` 验证 |
| `Delegate.CreateDelegate` 在 IL2CPP/AOT 限制 | 设备上构建委托失败 | `CreateDelegate` 仅绑定既有元数据、不发射 IL，AOT 安全；任务 A3 在 EditMode 验证；HybridCLR 热更程序集（战斗逻辑所在）原生支持 |
| finally Reset 检测破坏现有 0GC 热路径（Design 回归评估） | `ZeroAllocationDispatchTests` 回归 | 任务 B2 重新运行 `ZeroAllocationDispatchTests`，确认非 IPoolable handler 的 `is IPoolable` 为 isinst 零分配 |
| 架构守卫扫描误报（`CqrsArchitectureGuardTests`） | 新增异步 handler 触发守卫 | 守卫仅扫 `IEventHandler<>`/`IQueryHandler<,>`，新增 `IAsync*` 不在扫描范围；任务 D1 后重跑守卫确认 |
| 同步/异步注册表共存 | 同类型双 handler 语义混淆 | 计划明确允许共存（不同 API）；文档（任务 D2）说明选型 |

---

## 全局约定

**测试运行命令**（所有任务复用，替换 `<TestClass>` 与 `<results-name>`）：

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.<TestClass>" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/<results-name>.xml"
```

**提交规范：** 禁止 `Co-Authored-By`/`Signed-off-by` 等自动署名行。提交信息格式 `feat(cqrs): ...` / `test(cqrs): ...`。

**0GC 测试断言模式**（来自 `ZeroAllocationDispatchTests`）：

```csharp
ForceFullGc();
var before = GC.GetAllocatedBytesForCurrentThread();
for (var i = 0; i < MeasuredIterations; i++) { /* dispatch */ }
var after = GC.GetAllocatedBytesForCurrentThread();
Assert.AreEqual(before, after);
```

**所有新 struct 实现 `readonly struct`**（避免 `in` 防御拷贝，保证 0GC）。

---

## 任务 A1: Self-Handling Command 分发

**覆盖的上游需求：** FRD「Struct 自处理执行路径正常工作」「Send API 自动识别 Class/Struct」；Design Slice A。

**文件：**
- 创建：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ISelfHandlingCommand.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- 测试：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/SelfHandlingDispatchTests.cs`

- [ ] **步骤 1: 创建接口**

文件 `Abstractions/ISelfHandlingCommand.cs`：

```csharp
namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Marks a struct command as self-handling: it executes its own logic via
    /// <see cref="Execute"/> with no external handler registration.
    /// </summary>
    /// <remarks>
    /// Implement as <c>readonly struct</c>. Dispatch is zero-allocation: the bus
    /// invokes <see cref="Execute"/> through a per-type cached constrained call,
    /// avoiding boxing. A command type cannot be both self-handling and registered
    /// with <see cref="ICommandHandler{TCommand}"/>.
    /// </remarks>
    public interface ISelfHandlingCommand : ICommand
    {
        void Execute();
    }
}
```

- [ ] **步骤 2: 编写失败的测试**

文件 `Tests/EditMode/Cqrs/SelfHandlingDispatchTests.cs`：

```csharp
using System;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class SelfHandlingDispatchTests
    {
        private sealed class Counter
        {
            public int Value;
        }

        private readonly struct BumpSelfHandlingCommand : ISelfHandlingCommand
        {
            private readonly Counter _counter;
            private readonly int _delta;

            public BumpSelfHandlingCommand(Counter counter, int delta)
            {
                _counter = counter;
                _delta = delta;
            }

            public void Execute()
            {
                _counter.Value += _delta;
            }
        }

        [Test]
        public void Send_SelfHandlingCommand_DispatchesExecuteWithoutRegistration()
        {
            var counter = new Counter();
            var bus = new CqrsBus();

            bus.Send(new BumpSelfHandlingCommand(counter, 5));

            Assert.AreEqual(5, counter.Value);
        }

        private readonly struct PlainCommand : ICommand
        {
            public int Value { get; }
            public PlainCommand(int value) { Value = value; }
        }

        private sealed class PlainCommandHandler : ICommandHandler<PlainCommand>
        {
            public int Received;
            public void Handle(in PlainCommand command) { Received = command.Value; }
        }

        [Test]
        public void Send_NonSelfHandlingCommand_StillUsesHandlerLookup()
        {
            var bus = new CqrsBus();
            var handler = new PlainCommandHandler();
            bus.RegisterCommand(handler);

            bus.Send(new PlainCommand(42));

            Assert.AreEqual(42, handler.Received);
        }
    }
}
```

- [ ] **步骤 3: 运行测试验证失败**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.SelfHandlingDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/self-handling-a1-1.xml"
```

预期：FAIL —— `Send_SelfHandlingCommand_DispatchesExecuteWithoutRegistration` 抛 `HandlerNotRegisteredException`（当前 `Send` 不识别 self-handling）。

- [ ] **步骤 4: 实现 self-handling 分发**

在 `Core/CqrsBus.cs` 顶部 `using` 增加（若无）：

```csharp
using System.Reflection;
```

在 `CqrsBus` 类内（字段区之后、`Send` 之前）加入每类型缓存与约束调用桥：

```csharp
        private static class SelfHandlingCommandCache<TCommand>
            where TCommand : struct, ICommand
        {
            public static readonly Action<TCommand> Invoke = BuildInvoke();

            private static Action<TCommand> BuildInvoke()
            {
                if (!typeof(ISelfHandlingCommand).IsAssignableFrom(typeof(TCommand)))
                {
                    return null;
                }

                var method = typeof(TCommand).GetMethod(
                    "Execute", BindingFlags.Public | BindingFlags.Instance);
                // 开放实例委托：首参数为 struct 值（按值传递，约束调用，零装箱）。
                return (Action<TCommand>)Delegate.CreateDelegate(
                    typeof(Action<TCommand>), null, method);
            }
        }
```

修改 `Send<TCommand>`，在 handler 查找之前插入 self-handling 分支：

```csharp
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            var invoke = SelfHandlingCommandCache<TCommand>.Invoke;
            if (invoke != null)
            {
                invoke(command);
                return;
            }

            var commandType = typeof(TCommand);
            if (!_commandHandlers.TryGetValue(commandType, out var registration))
            {
                throw new HandlerNotRegisteredException($"Command handler not registered: {commandType.FullName}");
            }

            var typedRegistration = (CommandHandlerRegistration<TCommand>)registration;
            typedRegistration.Handler.Handle(in command);
        }
```

> 说明：`Action<TCommand>` 按 struct 值传递是栈拷贝（非 GC 堆分配），通过 0GC 断言；`Delegate.CreateDelegate` 仅在类型首次使用时构造一次并缓存。

- [ ] **步骤 5: 运行测试验证通过**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.SelfHandlingDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/self-handling-a1-2.xml"
```

预期：PASS。

- [ ] **步骤 6: 回归 — 现有分发不受影响**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.CommandDispatchTests,Change.Framework.Tests.QueryDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/self-handling-a1-regress.xml"
```

预期：PASS（现有 Class handler 路径不变）。

- [ ] **步骤 7: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ISelfHandlingCommand.cs UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/SelfHandlingDispatchTests.cs
git commit -m "feat(cqrs): add struct self-handling command dispatch (zero-alloc)"
```

---

## 任务 A2: Self-Handling Query 分发

**覆盖的上游需求：** FRD「Ask API 自动识别 Class/Struct」；Design Slice A。

**文件：**
- 创建：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ISelfHandlingQuery.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- 测试：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/SelfHandlingDispatchTests.cs`（追加）

- [ ] **步骤 1: 创建接口**

文件 `Abstractions/ISelfHandlingQuery.cs`：

```csharp
namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Marks a struct query as self-handling: it computes its own result via
    /// <see cref="Execute"/> with no external handler registration.
    /// </summary>
    /// <remarks>
    /// Implement as <c>readonly struct</c>. Dispatch is zero-allocation via a per-type
    /// cached constrained call. A query type cannot be both self-handling and registered
    /// with <see cref="IQueryHandler{TQuery, TResult}"/>.
    /// </remarks>
    public interface ISelfHandlingQuery<TResult> : IQuery<TResult>
    {
        TResult Execute();
    }
}
```

- [ ] **步骤 2: 编写失败的测试**（追加到 `SelfHandlingDispatchTests.cs`）

```csharp
        private sealed class Source
        {
            public int Value;
        }

        private readonly struct ReadSelfHandlingQuery : ISelfHandlingQuery<int>
        {
            private readonly Source _source;

            public ReadSelfHandlingQuery(Source source) { _source = source; }

            public int Execute() => _source.Value;
        }

        [Test]
        public void Ask_SelfHandlingQuery_ReturnsExecuteResultWithoutRegistration()
        {
            var source = new Source { Value = 7 };
            var bus = new CqrsBus();

            var result = bus.Ask<ReadSelfHandlingQuery, int>(new ReadSelfHandlingQuery(source));

            Assert.AreEqual(7, result);
        }

        [Test]
        public void Query_SelfHandlingQuery_ReturnsExecuteResultWithoutRegistration()
        {
            var source = new Source { Value = 9 };
            var bus = new CqrsBus();

            var result = bus.Query<ReadSelfHandlingQuery, int>(new ReadSelfHandlingQuery(source));

            Assert.AreEqual(9, result);
        }
```

- [ ] **步骤 3: 运行测试验证失败**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.SelfHandlingDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/self-handling-a2-1.xml"
```

预期：FAIL —— 两个 query 测试抛 `HandlerNotRegisteredException`。

- [ ] **步骤 4: 实现 self-handling query 分发**

在 `CqrsBus` 类内加入 query 缓存（开放泛型接口需遍历检测）：

```csharp
        private static class SelfHandlingQueryCache<TQuery>
            where TQuery : struct
        {
            public static readonly bool IsSelfHandling = Compute();

            private static bool Compute()
            {
                foreach (var i in typeof(TQuery).GetInterfaces())
                {
                    if (i.IsGenericType
                        && i.GetGenericTypeDefinition() == typeof(ISelfHandlingQuery<>))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
```

修改 `Query<TQuery, TResult>`，开头插入 self-handling 分支。注意：因 `Query` 的约束仅为 `IQuery<TResult>`，无法直接调用 `ISelfHandlingQuery<TResult>` 约束方法，故通过 `Delegate.CreateDelegate` 缓存 `Func<TQuery, TResult>`：

```csharp
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult Query<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            if (SelfHandlingQueryCache<TQuery>.IsSelfHandling)
            {
                var invoke = SelfHandlingQueryInvokeCache<TQuery, TResult>.Invoke;
                return invoke(query);
            }

            var queryType = typeof(TQuery);
            var resultType = typeof(TResult);
            var queryKey = new QueryKey(queryType, resultType);
            if (!_queryHandlers.TryGetValue(queryKey, out var registration))
            {
                throw new HandlerNotRegisteredException(
                    $"Query handler not registered: {queryType.FullName} -> {resultType.FullName}");
            }

            var typedRegistration = (QueryHandlerRegistration<TQuery, TResult>)registration;
            return typedRegistration.Handler.Handle(in query);
        }

        private static class SelfHandlingQueryInvokeCache<TQuery, TResult>
            where TQuery : struct, IQuery<TResult>
        {
            public static readonly Func<TQuery, TResult> Invoke = BuildInvoke();

            private static Func<TQuery, TResult> BuildInvoke()
            {
                if (!SelfHandlingQueryCache<TQuery>.IsSelfHandling)
                {
                    return null;
                }

                var method = typeof(TQuery).GetMethod(
                    "Execute", BindingFlags.Public | BindingFlags.Instance);
                return (Func<TQuery, TResult>)Delegate.CreateDelegate(
                    typeof(Func<TQuery, TResult>), null, method);
            }
        }
```

`Ask<TQuery, TResult>` 委托给 `Query`，无需改动（已 `return Query<TQuery, TResult>(in query);`）。

- [ ] **步骤 5: 运行测试验证通过**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.SelfHandlingDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/self-handling-a2-2.xml"
```

预期：PASS。

- [ ] **步骤 6: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ISelfHandlingQuery.cs UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/SelfHandlingDispatchTests.cs
git commit -m "feat(cqrs): add struct self-handling query dispatch (zero-alloc)"
```

---

## 任务 A3: Self-Handling 热路径 0GC 验证

**覆盖的上游需求：** FRD「严格 0 字节 GC 分配（热路径执行）」「0GC 分配初步验证」。

**文件：**
- 测试：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/SelfHandlingDispatchTests.cs`（追加）

- [ ] **步骤 1: 编写 0GC 测试**（追加到 `SelfHandlingDispatchTests.cs`，复用现有常量/`ForceFullGc` 风格）

```csharp
        private const int WarmupIterations = 1000;
        private const int MeasuredIterations = 100000;

        private static void ForceFullGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        [Test]
        public void Send_SelfHandlingHotPath_AllocatesZeroBytesAfterWarmup()
        {
            var counter = new Counter();
            var bus = new CqrsBus();
            var command = new BumpSelfHandlingCommand(counter, 1);

            for (var i = 0; i < WarmupIterations; i++)
            {
                bus.Send(command);
            }

            ForceFullGc();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < MeasuredIterations; i++)
            {
                bus.Send(command);
            }
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
            Assert.AreEqual(WarmupIterations + MeasuredIterations, counter.Value);
        }

        [Test]
        public void Ask_SelfHandlingHotPath_AllocatesZeroBytesAfterWarmup()
        {
            var source = new Source { Value = 3 };
            var bus = new CqrsBus();
            var query = new ReadSelfHandlingQuery(source);

            for (var i = 0; i < WarmupIterations; i++)
            {
                bus.Ask<ReadSelfHandlingQuery, int>(query);
            }

            ForceFullGc();

            var sum = 0;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < MeasuredIterations; i++)
            {
                sum += bus.Ask<ReadSelfHandlingQuery, int>(query);
            }
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
            Assert.AreEqual(3 * MeasuredIterations, sum);
        }
```

- [ ] **步骤 2: 运行测试验证**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.SelfHandlingDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/self-handling-a3.xml"
```

预期：PASS。**若 FAIL（before != after）**：说明 `Delegate.CreateDelegate` 路径有分配——检查是否误用了 `Expression.Compile` 或装箱；`Action<TCommand>`/`Func<TQuery,TResult>` 按 struct 值传应为零堆分配。

- [ ] **步骤 3: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/SelfHandlingDispatchTests.cs
git commit -m "test(cqrs): verify zero-allocation on self-handling hot path"
```

---

## 任务 B1: 同步 Command 池化 Reset

**覆盖的上游需求：** FRD「Reset() 自动调用，无泄漏」；Design Slice B。

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- 测试：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/PoolingResetTests.cs`

- [ ] **步骤 1: 编写失败的测试**

文件 `Tests/EditMode/Cqrs/PoolingResetTests.cs`：

```csharp
using Change.Framework.Cqrs;
using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class PoolingResetTests
    {
        private readonly struct PoolableCommand : ICommand
        {
            public int Value { get; }
            public PoolableCommand(int value) { Value = value; }
        }

        private sealed class PoolableCommandHandler
            : ICommandHandler<PoolableCommand>, IPoolable
        {
            public int HandleCount;
            public int ResetCount;

            public void Handle(in PoolableCommand command) { HandleCount++; }

            public void Reset() { ResetCount++; }
        }

        private readonly struct PlainCommand : ICommand
        {
            public int Value { get; }
            public PlainCommand(int value) { Value = value; }
        }

        private sealed class NonPoolableHandler : ICommandHandler<PlainCommand>
        {
            public int HandleCount;
            public void Handle(in PlainCommand command) { HandleCount++; }
        }

        [Test]
        public void Send_PoolableHandler_CallsResetAfterEachHandle()
        {
            var bus = new CqrsBus();
            var handler = new PoolableCommandHandler();
            bus.RegisterCommand(handler);

            bus.Send(new PoolableCommand(1));
            bus.Send(new PoolableCommand(2));

            Assert.AreEqual(2, handler.HandleCount);
            Assert.AreEqual(2, handler.ResetCount);
        }

        [Test]
        public void Send_NonPoolableHandler_DoesNotCallReset()
        {
            var bus = new CqrsBus();
            var handler = new NonPoolableHandler();
            bus.RegisterCommand(handler);

            bus.Send(new PlainCommand(1));

            Assert.AreEqual(1, handler.HandleCount);
        }
    }
}
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.PoolingResetTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/pooling-b1-1.xml"
```

预期：FAIL —— `Send_PoolableHandler_CallsResetAfterEachHandle`（ResetCount == 0）。

- [ ] **步骤 3: 实现 finally Reset**

在 `CqrsBus` 类内加入 Reset 辅助方法（实例方法，需访问 `_logger`）：

```csharp
        private void ResetIfPoolable(object handler, Type messageType)
        {
            if (handler is IPoolable poolable)
            {
                try
                {
                    poolable.Reset();
                }
                catch (Exception ex)
                {
                    try
                    {
                        _logger.Error(
                            $"Reset() failed for {messageType.FullName}: {ex.Message}");
                    }
                    catch (Exception)
                    {
                        // 日志本身失败时静默，不影响命令执行。
                    }
                }
            }
        }
```

修改 `Send<TCommand>` 的 handler 分支（self-handling 分支保持不变，仅 handler 路径加 try/finally）：

```csharp
            var handler = ((CommandHandlerRegistration<TCommand>)registration).Handler;
            try
            {
                handler.Handle(in command);
            }
            finally
            {
                ResetIfPoolable(handler, typeof(TCommand));
            }
```

- [ ] **步骤 4: 运行测试验证通过**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.PoolingResetTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/pooling-b1-2.xml"
```

预期：PASS。

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/PoolingResetTests.cs
git commit -m "feat(cqrs): auto-call IPoolable.Reset after command dispatch"
```

---

## 任务 B2: Query Reset + Reset 异常隔离 + 0GC 回归

**覆盖的上游需求：** FRD「Reset() 自动调用，无泄漏」「Reset() 抛异常时不影响命令执行」；Design Slice B；回归评估（现有 0GC 热路径不受影响）。

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- 测试：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/PoolingResetTests.cs`（追加）

- [ ] **步骤 1: 编写失败的测试**（追加到 `PoolingResetTests.cs`）

```csharp
        private readonly struct PoolableQuery : IQuery<int>
        {
            public int Seed { get; }
            public PoolableQuery(int seed) { Seed = seed; }
        }

        private sealed class PoolableQueryHandler
            : IQueryHandler<PoolableQuery, int>, IPoolable
        {
            public int ResetCount;
            public int Handle(in PoolableQuery query) => query.Seed * 2;
            public void Reset() { ResetCount++; }
        }

        private sealed class ThrowingResetHandler
            : ICommandHandler<PoolableCommand>, IPoolable
        {
            public bool Handled;
            public void Handle(in PoolableCommand command) { Handled = true; }
            public void Reset() { throw new System.InvalidOperationException("reset boom"); }
        }

        [Test]
        public void Query_PoolableHandler_CallsResetAfterHandle()
        {
            var bus = new CqrsBus();
            var handler = new PoolableQueryHandler();
            bus.RegisterQuery(handler);

            var result1 = bus.Ask<PoolableQuery, int>(new PoolableQuery(3));
            var result2 = bus.Ask<PoolableQuery, int>(new PoolableQuery(4));

            Assert.AreEqual(6, result1);
            Assert.AreEqual(8, result2);
            Assert.AreEqual(2, handler.ResetCount);
        }

        [Test]
        public void Send_ResetThrows_CommandStillSucceeds()
        {
            var bus = new CqrsBus();
            var handler = new ThrowingResetHandler();
            bus.RegisterCommand(handler);

            Assert.DoesNotThrow(() => bus.Send(new PoolableCommand(1)));
            Assert.IsTrue(handler.Handled);
        }
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.PoolingResetTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/pooling-b2-1.xml"
```

预期：FAIL —— `Query_PoolableHandler_CallsResetAfterHandle`（ResetCount == 0）。

- [ ] **步骤 3: 为 Query 路径加 try/finally**

修改 `Query<TQuery, TResult>` 的 handler 分支（self-handling 分支不变）：

```csharp
            var handler = ((QueryHandlerRegistration<TQuery, TResult>)registration).Handler;
            try
            {
                return handler.Handle(in query);
            }
            finally
            {
                ResetIfPoolable(handler, typeof(TQuery));
            }
```

> Reset 异常隔离已在 `ResetIfPoolable` 内 try/catch 处理（任务 B1 步骤 3），`Send_ResetThrows_CommandStillSucceeds` 应在步骤 4 通过。

- [ ] **步骤 4: 运行测试验证通过**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.PoolingResetTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/pooling-b2-2.xml"
```

预期：PASS（含 Reset 异常隔离测试）。

- [ ] **步骤 5: 回归 — 现有 0GC 热路径不受影响**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.ZeroAllocationDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/pooling-b2-regress.xml"
```

预期：PASS。**若 FAIL**：说明 finally/`is IPoolable` 破坏了 0GC——确认 handler 为 sealed class、`handler is IPoolable` 对非 IPoolable 类型为 isinst 零分配、finally 块本身无分配。

- [ ] **步骤 6: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/PoolingResetTests.cs
git commit -m "feat(cqrs): auto-call Reset after query dispatch and isolate Reset failures"
```

---

## 任务 C1: 异步 Handler 接口与 API 面

**覆盖的上游需求：** FRD「异步 Command 执行正常工作」「SendAsync 仅支持 Class 异步 Handler」；Design Slice C。

**依赖：** 任务 B1（异步 finally 复用 `ResetIfPoolable`）。

**文件：**
- 创建：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IAsyncCommandHandler.cs`
- 创建：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IAsyncQueryHandler.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBus.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntime.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRegistry.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBootstrap.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBootstrap.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`（注册表字段 + 注册方法骨架）

- [ ] **步骤 1: 创建异步 Handler 接口**

文件 `Abstractions/IAsyncCommandHandler.cs`：

```csharp
using System.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Handles a command asynchronously. Implement as a class. Only Class mode
    /// supports async dispatch; struct self-handling commands must use sync <c>Send</c>.
    /// </summary>
    public interface IAsyncCommandHandler<TCommand>
        where TCommand : struct, ICommand
    {
        Task ExecuteAsync(TCommand command);
    }
}
```

文件 `Abstractions/IAsyncQueryHandler.cs`：

```csharp
using System.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Handles a query asynchronously. Implement as a class. Only Class mode
    /// supports async dispatch.
    /// </summary>
    public interface IAsyncQueryHandler<TQuery, TResult>
        where TQuery : struct, IQuery<TResult>
    {
        Task<TResult> ExecuteAsync(TQuery query);
    }
}
```

- [ ] **步骤 2: 扩展分发与注册接口签名**

`Abstractions/ICqrsBus.cs` 追加：

```csharp
using System.Threading.Tasks;
// ...
        Task SendAsync<TCommand>(TCommand command)
            where TCommand : struct, ICommand;

        Task<TResult> AskAsync<TQuery, TResult>(TQuery query)
            where TQuery : struct, IQuery<TResult>;
```

`Abstractions/ICqrsRuntime.cs` 追加（使 `Build()` 返回面可用）：

```csharp
using System.Threading.Tasks;
// ...
        Task SendAsync<TCommand>(TCommand command)
            where TCommand : struct, ICommand;

        Task<TResult> AskAsync<TQuery, TResult>(TQuery query)
            where TQuery : struct, IQuery<TResult>;
```

`Abstractions/ICqrsRegistry.cs` 追加：

```csharp
        void RegisterAsyncCommand<TCommand>(IAsyncCommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;

        void RegisterAsyncQuery<TQuery, TResult>(IAsyncQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>;
```

`Abstractions/ICqrsBootstrap.cs` 追加：

```csharp
        void RegisterAsyncCommand<TCommand>(IAsyncCommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;

        void RegisterAsyncQuery<TQuery, TResult>(IAsyncQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>;
```

- [ ] **步骤 3: 在 CqrsBus 加入异步注册表与注册方法骨架**

`Core/CqrsBus.cs` 顶部 `using` 增加（若无）：

```csharp
using System.Threading.Tasks;
```

加入内部注册类型与字段：

```csharp
        private interface IAsyncCommandHandlerRegistration { }

        private interface IAsyncQueryHandlerRegistration { }

        private sealed class AsyncCommandHandlerRegistration<TCommand>
            : IAsyncCommandHandlerRegistration
            where TCommand : struct, ICommand
        {
            public AsyncCommandHandlerRegistration(IAsyncCommandHandler<TCommand> handler)
            {
                Handler = handler;
            }

            public IAsyncCommandHandler<TCommand> Handler { get; }
        }

        private sealed class AsyncQueryHandlerRegistration<TQuery, TResult>
            : IAsyncQueryHandlerRegistration
            where TQuery : struct, IQuery<TResult>
        {
            public AsyncQueryHandlerRegistration(IAsyncQueryHandler<TQuery, TResult> handler)
            {
                Handler = handler;
            }

            public IAsyncQueryHandler<TQuery, TResult> Handler { get; }
        }
```

字段区追加：

```csharp
        private readonly FastDictionary<Type, IAsyncCommandHandlerRegistration> _asyncCommandHandlers = new();
        private readonly FastDictionary<QueryKey, IAsyncQueryHandlerRegistration> _asyncQueryHandlers = new();
```

注册方法（冲突守卫在任务 D1 完善，此处先放基础校验）：

```csharp
        public void RegisterAsyncCommand<TCommand>(IAsyncCommandHandler<TCommand> handler)
            where TCommand : struct, ICommand
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            ThrowIfValueTypeHandler(handler, nameof(handler));

            var commandType = typeof(TCommand);
            if (!_asyncCommandHandlers.TryAdd(
                    commandType, new AsyncCommandHandlerRegistration<TCommand>(handler)))
            {
                throw new DuplicateRegistrationException(
                    $"Async command handler already registered: {commandType.FullName}");
            }

            SafeInfo(CommandRegisteredMessage);
        }

        public void RegisterAsyncQuery<TQuery, TResult>(IAsyncQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            ThrowIfValueTypeHandler(handler, nameof(handler));

            var queryKey = new QueryKey(typeof(TQuery), typeof(TResult));
            if (!_asyncQueryHandlers.TryAdd(
                    queryKey, new AsyncQueryHandlerRegistration<TQuery, TResult>(handler)))
            {
                throw new DuplicateRegistrationException(
                    $"Async query handler already registered: {queryKey.QueryType.FullName} -> {queryKey.ResultType.FullName}");
            }

            SafeInfo(QueryRegisteredMessage);
        }
```

`SendAsync`/`AskAsync` 在任务 C2/C3 实现。本步骤为满足接口契约先加入 throw骨架（避免接口未实现导致编译失败）：

```csharp
        public Task SendAsync<TCommand>(TCommand command)
            where TCommand : struct, ICommand
        {
            throw new System.NotImplementedException();
        }

        public Task<TResult> AskAsync<TQuery, TResult>(TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            throw new System.NotImplementedException();
        }
```

- [ ] **步骤 4: CqrsBootstrap 透传异步注册**

`Core/CqrsBootstrap.cs` 追加方法：

```csharp
        public void RegisterAsyncCommand<TCommand>(IAsyncCommandHandler<TCommand> handler)
            where TCommand : struct, ICommand
        {
            _bus.RegisterAsyncCommand(handler);
        }

        public void RegisterAsyncQuery<TQuery, TResult>(IAsyncQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            _bus.RegisterAsyncQuery(handler);
        }
```

- [ ] **步骤 5: 编译验证（全量现有测试不应回归）**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.CommandDispatchTests,Change.Framework.Tests.QueryDispatchTests,Change.Framework.Tests.CqrsBootstrapLifecycleTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/async-c1-compile.xml"
```

预期：PASS（接口扩展与注册骨架不改变现有行为；`SendAsync`/`AskAsync` 暂为骨架，未被现有测试调用）。

- [ ] **步骤 6: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IAsyncCommandHandler.cs UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IAsyncQueryHandler.cs UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBus.cs UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntime.cs UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRegistry.cs UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBootstrap.cs UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBootstrap.cs UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs
git commit -m "feat(cqrs): add async handler interfaces and registration surface"
```

---

## 任务 C2: SendAsync 分发 + Reset + self-handling 拒绝

**覆盖的上游需求：** FRD「异步 Command Handler 正常执行并完成」「SendAsync 拒绝 Struct 自处理」；Design Slice C。

**依赖：** 任务 C1。

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- 测试：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/AsyncDispatchTests.cs`

- [ ] **步骤 1: 编写失败的测试**

文件 `Tests/EditMode/Cqrs/AsyncDispatchTests.cs`：

```csharp
using System.Threading.Tasks;
using Change.Framework.Cqrs;
using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class AsyncDispatchTests
    {
        private readonly struct AsyncCommand : ICommand
        {
            public int Value { get; }
            public AsyncCommand(int value) { Value = value; }
        }

        private sealed class AsyncCommandHandler
            : IAsyncCommandHandler<AsyncCommand>, IPoolable
        {
            public int ResetCount;
            public int Captured;

            public Task ExecuteAsync(AsyncCommand command)
            {
                Captured = command.Value;
                return Task.CompletedTask;
            }

            public void Reset() { ResetCount++; }
        }

        [Test]
        public async Task SendAsync_DispatchesHandler_AndResetsAfter()
        {
            var bus = new CqrsBus();
            var handler = new AsyncCommandHandler();
            bus.RegisterAsyncCommand(handler);

            await bus.SendAsync(new AsyncCommand(11));

            Assert.AreEqual(11, handler.Captured);
            Assert.AreEqual(1, handler.ResetCount);
        }

        private readonly struct SelfHandlingAsyncCommand : ISelfHandlingCommand
        {
            public void Execute() { }
        }

        [Test]
        public void SendAsync_SelfHandlingCommand_ThrowsNotSupportedException()
        {
            var bus = new CqrsBus();

            Assert.ThrowsAsync<System.NotSupportedException>(
                () => bus.SendAsync(new SelfHandlingAsyncCommand()));
        }
    }
}
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.AsyncDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/async-c2-1.xml"
```

预期：FAIL —— `SendAsync` 仍为 `NotImplementedException` 骨架。

- [ ] **步骤 3: 实现 SendAsync**

替换 `Core/CqrsBus.cs` 中 `SendAsync` 骨架：

```csharp
        public async Task SendAsync<TCommand>(TCommand command)
            where TCommand : struct, ICommand
        {
            if (SelfHandlingCommandCache<TCommand>.Invoke != null)
            {
                throw new NotSupportedException(
                    $"Self-handling command {typeof(TCommand).FullName} does not support async dispatch. Use Send().");
            }

            var commandType = typeof(TCommand);
            if (!_asyncCommandHandlers.TryGetValue(commandType, out var registration))
            {
                throw new HandlerNotRegisteredException(
                    $"Async command handler not registered: {commandType.FullName}");
            }

            var handler = ((AsyncCommandHandlerRegistration<TCommand>)registration).Handler;
            try
            {
                await handler.ExecuteAsync(command);
            }
            finally
            {
                ResetIfPoolable(handler, commandType);
            }
        }
```

- [ ] **步骤 4: 运行测试验证通过**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.AsyncDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/async-c2-2.xml"
```

预期：PASS。

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/AsyncDispatchTests.cs
git commit -m "feat(cqrs): implement SendAsync dispatch with Reset and self-handling rejection"
```

---

## 任务 C3: AskAsync 分发 + Reset

**覆盖的上游需求：** FRD「异步 Query Handler 返回正确结果」；Design Slice C。

**依赖：** 任务 C2。

**文件：**
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- 测试：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/AsyncDispatchTests.cs`（追加）

- [ ] **步骤 1: 编写失败的测试**（追加到 `AsyncDispatchTests.cs`）

```csharp
        private readonly struct AsyncQuery : IQuery<int>
        {
            public int Seed { get; }
            public AsyncQuery(int seed) { Seed = seed; }
        }

        private sealed class AsyncQueryHandler
            : IAsyncQueryHandler<AsyncQuery, int>, IPoolable
        {
            public int ResetCount;
            public Task<int> ExecuteAsync(AsyncQuery query) => Task.FromResult(query.Seed * 3);
            public void Reset() { ResetCount++; }
        }

        [Test]
        public async Task AskAsync_DispatchesHandler_ReturnsResult_AndResetsAfter()
        {
            var bus = new CqrsBus();
            var handler = new AsyncQueryHandler();
            bus.RegisterAsyncQuery(handler);

            var result = await bus.AskAsync<AsyncQuery, int>(new AsyncQuery(4));

            Assert.AreEqual(12, result);
            Assert.AreEqual(1, handler.ResetCount);
        }

        [Test]
        public void AskAsync_WhenNotRegistered_ThrowsHandlerNotRegisteredException()
        {
            var bus = new CqrsBus();

            Assert.ThrowsAsync<HandlerNotRegisteredException>(
                () => bus.AskAsync<AsyncQuery, int>(new AsyncQuery(4)));
        }
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.AsyncDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/async-c3-1.xml"
```

预期：FAIL —— `AskAsync` 仍为 `NotImplementedException` 骨架。

- [ ] **步骤 3: 实现 AskAsync**

替换 `Core/CqrsBus.cs` 中 `AskAsync` 骨架：

```csharp
        public async Task<TResult> AskAsync<TQuery, TResult>(TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            if (SelfHandlingQueryCache<TQuery>.IsSelfHandling)
            {
                throw new NotSupportedException(
                    $"Self-handling query {typeof(TQuery).FullName} does not support async dispatch. Use Ask().");
            }

            var queryKey = new QueryKey(typeof(TQuery), typeof(TResult));
            if (!_asyncQueryHandlers.TryGetValue(queryKey, out var registration))
            {
                throw new HandlerNotRegisteredException(
                    $"Async query handler not registered: {typeof(TQuery).FullName} -> {typeof(TResult).FullName}");
            }

            var handler = ((AsyncQueryHandlerRegistration<TQuery, TResult>)registration).Handler;
            try
            {
                return await handler.ExecuteAsync(query);
            }
            finally
            {
                ResetIfPoolable(handler, typeof(TQuery));
            }
        }
```

- [ ] **步骤 4: 运行测试验证通过**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.AsyncDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/async-c3-2.xml"
```

预期：PASS。

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/AsyncDispatchTests.cs
git commit -m "feat(cqrs): implement AskAsync dispatch with Reset and self-handling rejection"
```

---

## 任务 D1: 模式冲突守卫（注册时）

**覆盖的上游需求：** FRD「Command/Query 注册冲突检测正常（Class 已注册时 Struct 调用报错，反之亦然）」；Design Slice D。

**依赖：** 任务 A1、A2、C1（self-handling 缓存与异步注册表就绪）。

**文件：**
- 创建：`UnityProject/Assets/Change/Framework/Cqrs/Exceptions/ModeConflictException.cs`
- 修改：`UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- 测试：`UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ModeConflictTests.cs`

- [ ] **步骤 1: 创建异常类**

文件 `Exceptions/ModeConflictException.cs`（遵循现有 `HandlerNotRegisteredException` 模式）：

```csharp
using System;
using System.Runtime.Serialization;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Thrown when a command/query type is configured for both Class handler registration
    /// and Struct self-handling (ISelfHandlingCommand / ISelfHandlingQuery&lt;TResult&gt;).
    /// A type must use exactly one mode.
    /// </summary>
    [Serializable]
    public sealed class ModeConflictException : InvalidOperationException
    {
        public ModeConflictException(string message)
            : base(message)
        {
        }

        private ModeConflictException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
```

- [ ] **步骤 2: 编写失败的测试**

文件 `Tests/EditMode/Cqrs/ModeConflictTests.cs`：

```csharp
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class ModeConflictTests
    {
        private readonly struct SelfHandlingCommand : ISelfHandlingCommand
        {
            public void Execute() { }
        }

        private readonly struct PlainCommand : ICommand
        {
            public int Value { get; }
            public PlainCommand(int value) { Value = value; }
        }

        private sealed class PlainHandler : ICommandHandler<PlainCommand>
        {
            public void Handle(in PlainCommand command) { }
        }

        private sealed class SelfHandlingHandler : ICommandHandler<SelfHandlingCommand>
        {
            public void Handle(in SelfHandlingCommand command) { }
        }

        private sealed class AsyncSelfHandlingHandler : IAsyncCommandHandler<SelfHandlingCommand>
        {
            public System.Threading.Tasks.Task ExecuteAsync(SelfHandlingCommand command)
                => System.Threading.Tasks.Task.CompletedTask;
        }

        [Test]
        public void RegisterCommand_OnSelfHandlingType_ThrowsModeConflictException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ModeConflictException>(
                () => bus.RegisterCommand(new SelfHandlingHandler()));
        }

        [Test]
        public void RegisterAsyncCommand_OnSelfHandlingType_ThrowsModeConflictException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ModeConflictException>(
                () => bus.RegisterAsyncCommand(new AsyncSelfHandlingHandler()));
        }

        [Test]
        public void RegisterCommand_OnPlainType_Succeeds()
        {
            var bus = new CqrsBus();

            Assert.DoesNotThrow(() => bus.RegisterCommand(new PlainHandler()));
        }

        // —— Query 对称用例 ——
        private readonly struct SelfHandlingQuery : ISelfHandlingQuery<int>
        {
            public int Execute() => 0;
        }

        private sealed class SelfHandlingQueryHandler : IQueryHandler<SelfHandlingQuery, int>
        {
            public int Handle(in SelfHandlingQuery query) => 0;
        }

        [Test]
        public void RegisterQuery_OnSelfHandlingType_ThrowsModeConflictException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ModeConflictException>(
                () => bus.RegisterQuery(new SelfHandlingQueryHandler()));
        }
    }
}
```

- [ ] **步骤 3: 运行测试验证失败**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.ModeConflictTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/conflict-d1-1.xml"
```

预期：FAIL —— 注册 self-handling 类型当前不抛 `ModeConflictException`。

- [ ] **步骤 4: 在注册方法加入冲突守卫**

`Core/CqrsBus.cs` `RegisterCommand` 开头（`null`/`ThrowIfValueTypeHandler` 校验之后）插入：

```csharp
            if (SelfHandlingCommandCache<TCommand>.Invoke != null)
            {
                throw new ModeConflictException(
                    $"Cannot register a Class handler for self-handling command {typeof(TCommand).FullName}: "
                    + "it implements ISelfHandlingCommand. Use exactly one mode.");
            }
```

`RegisterAsyncCommand` 开头（同样位置）插入相同守卫。

`RegisterQuery` 开头插入（query 用 IsSelfHandling 标志）：

```csharp
            if (SelfHandlingQueryCache<TQuery>.IsSelfHandling)
            {
                throw new ModeConflictException(
                    $"Cannot register a Class handler for self-handling query {typeof(TQuery).FullName}: "
                    + "it implements ISelfHandlingQuery<TResult>. Use exactly one mode.");
            }
```

`RegisterAsyncQuery` 开头插入相同 query 守卫。

- [ ] **步骤 5: 运行测试验证通过**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.ModeConflictTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/conflict-d1-2.xml"
```

预期：PASS。

- [ ] **步骤 6: 全量回归**

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.CommandDispatchTests,Change.Framework.Tests.QueryDispatchTests,Change.Framework.Tests.EventDispatchTests,Change.Framework.Tests.CqrsBootstrapLifecycleTests,Change.Framework.Tests.CqrsArchitectureGuardTests,Change.Framework.Tests.ZeroAllocationDispatchTests,Change.Framework.Tests.SelfHandlingDispatchTests,Change.Framework.Tests.PoolingResetTests,Change.Framework.Tests.AsyncDispatchTests,Change.Framework.Tests.ModeConflictTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/conflict-d1-full.xml"
```

预期：全 PASS。

- [ ] **步骤 7: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Exceptions/ModeConflictException.cs UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ModeConflictTests.cs
git commit -m "feat(cqrs): reject Class handler registration for self-handling types"
```

---

## 任务 D2: 双模式使用指南

**覆盖的上游需求：** FRD 文档需求；Design Slice D（含未决问题 #1 Struct 访问外部服务的推荐方案）。

**文件：**
- 创建：`UnityProject/Assets/Change/Framework/Cqrs/Documentation/CQRS-DualMode-Guide.md`

- [ ] **步骤 1: 编写指南**

文件 `Documentation/CQRS-DualMode-Guide.md`：

````markdown
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

## 常见问题

**Q：Struct 自处理如何访问外部服务？**
A：通过 readonly struct 字段在构造时传入服务引用（如上例 `CombatBoard`），或使用服务定位器。避免在 Execute 内反射查找。

**Q：异步执行中反注册 Handler 会怎样？**
A：不支持。请确保 `await SendAsync/AskAsync` 完成后再反注册；运行中反注册会导致 `Reset` 指向已释放的实例。

**Q：同步和异步 Handler 能否为同一 Command 共存？**
A：可以。它们位于不同注册表，分别经 `Send` / `SendAsync` 分发。按场景择一即可。
````

- [ ] **步骤 2: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Cqrs/Documentation/CQRS-DualMode-Guide.md
git commit -m "docs(cqrs): add dual-mode command/query usage guide"
```

---

## 最终全量回归

完成所有任务后运行完整 CQRS 测试套件：

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.CommandDispatchTests,Change.Framework.Tests.QueryDispatchTests,Change.Framework.Tests.EventDispatchTests,Change.Framework.Tests.CqrsBootstrapLifecycleTests,Change.Framework.Tests.CqrsArchitectureGuardTests,Change.Framework.Tests.ZeroAllocationDispatchTests,Change.Framework.Tests.SelfHandlingDispatchTests,Change.Framework.Tests.PoolingResetTests,Change.Framework.Tests.AsyncDispatchTests,Change.Framework.Tests.ModeConflictTests,Change.Framework.Tests.DomainEventSemanticsTests,Change.Framework.Tests.RuntimeRegistrationTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/cqrs-dual-mode-final.xml"
```

预期：全 PASS（含现有所有 CQRS 测试无回归）。
