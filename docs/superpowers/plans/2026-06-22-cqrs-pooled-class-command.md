# CQRS 池化 class Command 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 CQRS 同步与异步 command 增加 class 池化自处理模式（复用 `Change.Framework.Pooling` 对象池），并补齐异步 command 的 EditMode 测试。

**Architecture:** 在既有 struct 自处理 command（`ICommand`/`IAsyncCommand`）之外，新增 class 池化自处理接口（`IPooledCommand`/`IPooledAsyncCommand`）。bus 通过泛型约束（`struct` vs `class`）区分 `Send` 重载：class 路径为 `Get → configure → Execute → Release`（`PoolEngine.Release` 内部调 `Reset`），异步在 `await` 完成后才 `Release`。两轨自处理、不拆 command/handler。

**Tech Stack:** C# / .NET Standard 2.1 / Unity 2022.3.60f1 / UniTask 2.5.10 / NUnit（Unity Test Framework 1.1.33）

## Global Constraints

- 命名空间：框架 `Change.Framework.*`，测试 `Change.Framework.Tests`。
- Framework 零外部依赖；`Pooling` 与 `Cqrs` 同属 `Change.Framework` 程序集，可直接互引。
- Commit message 用前缀（`feat:` / `test:` / `docs:`），**禁止** `Co-Authored-By` / `Signed-off-by` 等署名行。
- 测试结果统一输出到 `UnityProject/TestResults/`，命名 `<suite>-<yyyyMMdd-HHmmss>.xml`。
- Unity 测试命令**不带** `-quit`（否则 XML 假产出）；`-testFilter` 单类，不支持逗号分隔。
- 编译回归命令带 `-quit`；测试命令不带。
- readonly struct 的异步执行委托给 `static async` helper，不在 struct 实例上承载 async 状态机。
- 新建 `.cs` 文件由 Unity 首次导入自动生成 `.meta`，commit 时一并 `git add`。
- 关联 spec：`docs/superpowers/specs/2026-06-21-cqrs-pooled-class-command-design.md`

---

### Task 1: 池化 class command 接口 + 同步分发 + 同步测试

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IPooledCommand.cs`
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBus.cs`
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntime.cs`
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- Test: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/PooledCommandDispatchTests.cs`

**Interfaces:**
- Consumes: `Change.Framework.Pooling.IPoolable`（`void Reset()`）、`Change.Framework.Pooling.Pool<T>`（`Get`/`Release`/`InactiveCount`/`Clear`，约束 `where T : class, IPoolable, new()`）
- Produces: `Change.Framework.Cqrs.IPooledCommand`、`Change.Framework.Cqrs.IPooledAsyncCommand`、`CqrsBus.Send<TCommand>(Action<TCommand>) where TCommand : class, IPooledCommand, new()`

- [ ] **Step 1: 写失败测试（同步池化分发）**

创建 `Tests/EditMode/Cqrs/PooledCommandDispatchTests.cs`：

```csharp
using System;
using Change.Framework.Cqrs;
using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class PooledCommandDispatchTests
    {
        private sealed class IncrementCommand : IPooledCommand
        {
            public CounterState State;
            public int Amount;
            public int ResetCount;

            public void Execute()
            {
                State.Value += Amount;
            }

            public void Reset()
            {
                Amount = 0;
                ResetCount++;
            }
        }

        private sealed class CounterState
        {
            public int Value;
        }

        private sealed class ThrowCommand : IPooledCommand
        {
            public void Execute() => throw new InvalidOperationException("boom");
            public void Reset() { }
        }

        [SetUp]
        public void SetUp()
        {
            Pool<IncrementCommand>.Clear();
            Pool<ThrowCommand>.Clear();
        }

        [Test]
        public void Send_ExecutesPooledCommand()
        {
            var state = new CounterState();
            var bus = new CqrsBus();

            bus.Send<IncrementCommand>(c => { c.State = state; c.Amount = 3; });

            Assert.AreEqual(3, state.Value);
        }

        [Test]
        public void Send_ConfigureSetsFields()
        {
            var state = new CounterState();
            var bus = new CqrsBus();

            bus.Send<IncrementCommand>(c => { c.State = state; c.Amount = 7; });

            Assert.AreEqual(7, state.Value);
        }

        [Test]
        public void Send_ReleasesAndResetsAfterExecute()
        {
            var state = new CounterState();
            var bus = new CqrsBus();
            int resetBefore = 0;

            bus.Send<IncrementCommand>(c =>
            {
                c.State = state;
                c.Amount = 2;
                resetBefore = c.ResetCount;
            });

            // 实例已归还：池中有 1 个空闲
            Assert.AreEqual(1, Pool<IncrementCommand>.InactiveCount);

            // 归还的实例 Reset 已被调用（Release 内部触发）
            var returned = Pool<IncrementCommand>.Get();
            try
            {
                Assert.Greater(returned.ResetCount, resetBefore);
            }
            finally
            {
                Pool<IncrementCommand>.Release(returned);
            }
        }

        [Test]
        public void Send_ReleaseOnException()
        {
            var bus = new CqrsBus();
            Assert.AreEqual(0, Pool<ThrowCommand>.InactiveCount);

            Assert.Throws<InvalidOperationException>(() => bus.Send<ThrowCommand>(_ => { }));

            Assert.AreEqual(1, Pool<ThrowCommand>.InactiveCount);
        }
    }
}
```

- [ ] **Step 2: 运行测试验证失败（编译错误：类型/方法不存在）**

```bash
"/Applications/Unity/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.PooledCommandDispatchTests" \
  -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-pooled-command.xml" \
  -logFile "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-pooled-command.log"
```
Expected: 编译失败，`IPooledCommand` 未定义 / `CqrsBus` 无 `Send<T>(Action<T>)` 重载。

- [ ] **Step 3: 创建接口文件**

创建 `Cqrs/Abstractions/IPooledCommand.cs`：

```csharp
using Change.Framework.Pooling;
using Cysharp.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// 池化自处理同步 command。实现为 class，配合 <c>CqrsBus.Send&lt;T&gt;(Action&lt;T&gt;)</c> 分发。
    /// 实例由 <see cref="Pool{T}"/> 借出，Execute 后 bus 调 <see cref="Pool{T}.Release"/> 归还
    /// （<see cref="Pool{T}.Release"/> 内部会调用 <see cref="IPoolable.Reset"/>）。
    /// per-dispatch 数据通过 configure 回调设置。
    /// </summary>
    public interface IPooledCommand : IPoolable
    {
        void Execute();
    }

    /// <summary>
    /// 池化自处理异步 command。实现为 class，配合 <c>CqrsBus.SendAsync&lt;T&gt;(Action&lt;T&gt;)</c> 分发。
    /// bus 在 await ExecuteAsync 完成后才 Release 归还，避免异步竞态。
    /// </summary>
    public interface IPooledAsyncCommand : IPoolable
    {
        UniTask ExecuteAsync();
    }
}
```

- [ ] **Step 4: 在 ICqrsBus 加同步签名**

修改 `Cqrs/Abstractions/ICqrsBus.cs`，在 `Send<TCommand>(in TCommand)` 之后新增：

```csharp
        void Send<TCommand>(Action<TCommand> configure)
            where TCommand : class, IPooledCommand, new();
```

- [ ] **Step 5: 在 ICqrsRuntime 加同步签名**

修改 `Cqrs/Abstractions/ICqrsRuntime.cs`，在 `Send<TCommand>(in TCommand)` 之后新增同样签名：

```csharp
        void Send<TCommand>(Action<TCommand> configure)
            where TCommand : class, IPooledCommand, new();
```

- [ ] **Step 6: 在 CqrsBus 加 using 与同步实现**

修改 `Cqrs/Core/CqrsBus.cs`：
- 顶部 using 区追加 `using Change.Framework.Pooling;`
- 在 `Send<TCommand>(in TCommand command)` 之后新增：

```csharp
        // ===== Pooled Class Command Dispatch (self-handling, pooled) =====

        public void Send<TCommand>(Action<TCommand> configure)
            where TCommand : class, IPooledCommand, new()
        {
            var command = Pool<TCommand>.Get();
            try
            {
                configure(command);
                command.Execute();
            }
            finally
            {
                // PoolEngine.Release 内部会调用 command.Reset()
                Pool<TCommand>.Release(command);
            }
        }
```

- [ ] **Step 7: 运行测试验证通过**

```bash
"/Applications/Unity/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.PooledCommandDispatchTests" \
  -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-pooled-command.xml" \
  -logFile "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-pooled-command.log"
```
Expected: 4 个测试全部 PASS。检查 XML 中 `testcasecount="4"` 且 `failures="0"`。

- [ ] **Step 8: 提交**

```bash
cd /Users/andy/Workspace/github/andycai/fun
git add UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IPooledCommand.cs \
        UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IPooledCommand.cs.meta \
        UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBus.cs \
        UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntime.cs \
        UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/PooledCommandDispatchTests.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/PooledCommandDispatchTests.cs.meta
git commit -m "feat(cqrs): add pooled class command interface and sync dispatch"
```
（若 `.meta` 尚未由 Unity 生成，先重跑 Step 7 触发导入再 add。）

---

### Task 2: 测试程序集引用 UniTask + 池化 class 异步分发 + 异步测试

**Files:**
- Modify: `UnityProject/Assets/Change/Framework/Tests/EditMode/Change.Framework.EditModeTests.asmdef`
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBus.cs`
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntime.cs`
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- Test: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/PooledAsyncCommandDispatchTests.cs`

**Interfaces:**
- Consumes: Task 1 的 `IPooledAsyncCommand`；`UniTask` / `UniTaskCompletionSource`（asmdef 在本任务加引用）
- Produces: `CqrsBus.SendAsync<TCommand>(Action<TCommand>) where TCommand : class, IPooledAsyncCommand, new()`；测试 asmdef 显式引用 `UniTask`

- [ ] **Step 1: 测试 asmdef 加 UniTask 引用**

修改 `Tests/EditMode/Change.Framework.EditModeTests.asmdef`，`references` 由 `["Change.Framework"]` 改为：

```json
{
    "name": "Change.Framework.EditModeTests",
    "rootNamespace": "Change.Framework",
    "references": [
        "Change.Framework",
        "UniTask"
    ],
    "optionalUnityReferences": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": []
}
```

- [ ] **Step 2: 写失败测试（异步池化分发 + 生命周期）**

创建 `Tests/EditMode/Cqrs/PooledAsyncCommandDispatchTests.cs`：

```csharp
using System;
using Change.Framework.Cqrs;
using Change.Framework.Pooling;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class PooledAsyncCommandDispatchTests
    {
        private sealed class IncrementAsyncCommand : IPooledAsyncCommand
        {
            public CounterState State;
            public int Amount;
            public int ResetCount;
            public bool CompleteSynchronously;

            public async UniTask ExecuteAsync()
            {
                if (!CompleteSynchronously)
                {
                    await UniTask.Yield();
                }
                State.Value += Amount;
            }

            public void Reset()
            {
                Amount = 0;
                ResetCount++;
            }
        }

        private sealed class CounterState
        {
            public int Value;
        }

        private sealed class ThrowAsyncCommand : IPooledAsyncCommand
        {
            public async UniTask ExecuteAsync()
            {
                await UniTask.Yield();
                throw new InvalidOperationException("boom");
            }
            public void Reset() { }
        }

        // ExecuteAsync 由外部 TCS 控制完成时机，用于断言“await 完成前实例未归还”
        private sealed class ControlledAsyncCommand : IPooledAsyncCommand
        {
            public readonly UniTaskCompletionSource Tcs = new UniTaskCompletionSource();
            public async UniTask ExecuteAsync() => await Tcs.Task;
            public void Reset() { }
        }

        [SetUp]
        public void SetUp()
        {
            Pool<IncrementAsyncCommand>.Clear();
            Pool<ThrowAsyncCommand>.Clear();
            Pool<ControlledAsyncCommand>.Clear();
        }

        [Test]
        public async UniTask SendAsync_ExecutesPooledCommand()
        {
            var state = new CounterState();
            var bus = new CqrsBus();
            await bus.SendAsync<IncrementAsyncCommand>(c => { c.State = state; c.Amount = 5; });
            Assert.AreEqual(5, state.Value);
        }

        [Test]
        public async UniTask SendAsync_ConfigureSetsFields()
        {
            var state = new CounterState();
            var bus = new CqrsBus();
            await bus.SendAsync<IncrementAsyncCommand>(c => { c.State = state; c.Amount = 9; });
            Assert.AreEqual(9, state.Value);
        }

        [Test]
        public async UniTask SendAsync_ReleasesOnlyAfterAwait()
        {
            var bus = new CqrsBus();
            ControlledAsyncCommand held = null;

            var sendTask = bus.SendAsync<ControlledAsyncCommand>(c => held = c);

            // ExecuteAsync 未完成：实例仍在 bus 手中，未归还
            Assert.AreEqual(0, Pool<ControlledAsyncCommand>.InactiveCount);
            Assert.NotNull(held);

            held.Tcs.TrySetResult();
            await sendTask;

            // await 完成后：实例已归还
            Assert.AreEqual(1, Pool<ControlledAsyncCommand>.InactiveCount);
        }

        [Test]
        public async UniTask SendAsync_ResetsAfterAwait()
        {
            var state = new CounterState();
            var bus = new CqrsBus();
            int resetBefore = 0;

            await bus.SendAsync<IncrementAsyncCommand>(c =>
            {
                c.State = state;
                c.Amount = 2;
                c.CompleteSynchronously = true;
                resetBefore = c.ResetCount;
            });

            var returned = Pool<IncrementAsyncCommand>.Get();
            try
            {
                Assert.Greater(returned.ResetCount, resetBefore);
            }
            finally
            {
                Pool<IncrementAsyncCommand>.Release(returned);
            }
        }

        [Test]
        public async UniTask SendAsync_ReleaseOnException()
        {
            var bus = new CqrsBus();
            Assert.AreEqual(0, Pool<ThrowAsyncCommand>.InactiveCount);

            try
            {
                await bus.SendAsync<ThrowAsyncCommand>(_ => { });
                Assert.Fail("Expected InvalidOperationException");
            }
            catch (InvalidOperationException) { }

            Assert.AreEqual(1, Pool<ThrowAsyncCommand>.InactiveCount);
        }
    }
}
```

- [ ] **Step 3: 运行测试验证失败（CqrsBus 无 SendAsync<T>(Action<T>) 重载）**

```bash
"/Applications/Unity/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.PooledAsyncCommandDispatchTests" \
  -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-pooled-async.xml" \
  -logFile "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-pooled-async.log"
```
Expected: 编译失败，`CqrsBus` 无 `SendAsync<T>(Action<T>)` 重载。

- [ ] **Step 4: 在 ICqrsBus 加异步签名**

修改 `Cqrs/Abstractions/ICqrsBus.cs`，在 `SendAsync<TCommand>(TCommand)` 之后新增：

```csharp
        UniTask SendAsync<TCommand>(Action<TCommand> configure)
            where TCommand : class, IPooledAsyncCommand, new();
```

- [ ] **Step 5: 在 ICqrsRuntime 加异步签名**

修改 `Cqrs/Abstractions/ICqrsRuntime.cs`，在 `SendAsync<TCommand>(TCommand)` 之后新增同样签名：

```csharp
        UniTask SendAsync<TCommand>(Action<TCommand> configure)
            where TCommand : class, IPooledAsyncCommand, new();
```

- [ ] **Step 6: 在 CqrsBus 加异步实现**

修改 `Cqrs/Core/CqrsBus.cs`，在 Task 1 新增的同步 `Send<TCommand>(Action<TCommand>)` 之后追加：

```csharp
        public async UniTask SendAsync<TCommand>(Action<TCommand> configure)
            where TCommand : class, IPooledAsyncCommand, new()
        {
            var command = Pool<TCommand>.Get();
            try
            {
                configure(command);
                await command.ExecuteAsync();
            }
            finally
            {
                // await 完成后才 Release，避免异步竞态；Release 内部调用 Reset()
                Pool<TCommand>.Release(command);
            }
        }
```

- [ ] **Step 7: 运行测试验证通过**

```bash
"/Applications/Unity/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.PooledAsyncCommandDispatchTests" \
  -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-pooled-async.xml" \
  -logFile "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-pooled-async.log"
```
Expected: 5 个测试全部 PASS，`failures="0"`。

- [ ] **Step 8: 提交**

```bash
cd /Users/andy/Workspace/github/andycai/fun
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Change.Framework.EditModeTests.asmdef \
        UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBus.cs \
        UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntime.cs \
        UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/PooledAsyncCommandDispatchTests.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/PooledAsyncCommandDispatchTests.cs.meta
git commit -m "feat(cqrs): add pooled class async command dispatch"
```

---

### Task 3: struct 异步 command 测试（补既有 IAsyncCommand / SendAsync）

**Files:**
- Test: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/AsyncCommandDispatchTests.cs`

**Interfaces:**
- Consumes: 既有 `IAsyncCommand`（`UniTask ExecuteAsync()`）、既有 `CqrsBus.SendAsync<TCommand>(TCommand) where T : struct, IAsyncCommand`；Task 2 已加的测试 asmdef `UniTask` 引用；`UniTask.Yield()`

- [ ] **Step 1: 写测试（被测代码已存在，直接验证行为）**

创建 `Tests/EditMode/Cqrs/AsyncCommandDispatchTests.cs`：

```csharp
using System;
using Change.Framework.Cqrs;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class AsyncCommandDispatchTests
    {
        // readonly struct 的异步执行委托给 static async helper，
        // 避免在 struct 实例上承载 async 状态机。
        private readonly struct AsyncIncrementCommand : IAsyncCommand
        {
            private readonly CounterState _state;
            private readonly int _amount;

            public AsyncIncrementCommand(CounterState state, int amount)
            {
                _state = state;
                _amount = amount;
            }

            public UniTask ExecuteAsync() => ExecuteAsyncCore(_state, _amount);

            private static async UniTask ExecuteAsyncCore(CounterState state, int amount)
            {
                await UniTask.Yield();
                state.Value += amount;
            }
        }

        private readonly struct AsyncThrowCommand : IAsyncCommand
        {
            public UniTask ExecuteAsync() => ThrowCore();

            private static async UniTask ThrowCore()
            {
                await UniTask.Yield();
                throw new InvalidOperationException("boom");
            }
        }

        private sealed class CounterState
        {
            public int Value;
        }

        [Test]
        public async UniTask SendAsync_ExecutesSelfHandlingCommand()
        {
            var state = new CounterState();
            var bus = new CqrsBus();

            await bus.SendAsync(new AsyncIncrementCommand(state, 4));

            Assert.AreEqual(4, state.Value);
        }

        [Test]
        public async UniTask SendAsync_AwaitsCompletion()
        {
            var state = new CounterState();
            var bus = new CqrsBus();

            // ExecuteAsync 内部 await UniTask.Yield() 推迟完成；
            // 若 bus 不 await（fire-and-forget），断言时 Value 仍为 0。
            await bus.SendAsync(new AsyncIncrementCommand(state, 6));

            Assert.AreEqual(6, state.Value);
        }

        [Test]
        public async UniTask SendAsync_PropagatesException()
        {
            var bus = new CqrsBus();

            try
            {
                await bus.SendAsync(new AsyncThrowCommand());
                Assert.Fail("Expected InvalidOperationException");
            }
            catch (InvalidOperationException) { }
        }
    }
}
```

- [ ] **Step 2: 运行测试验证通过**

```bash
"/Applications/Unity/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.AsyncCommandDispatchTests" \
  -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-async-command.xml" \
  -logFile "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-async-command.log"
```
Expected: 3 个测试全部 PASS，`failures="0"`。

- [ ] **Step 3: 提交**

```bash
cd /Users/andy/Workspace/github/andycai/fun
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/AsyncCommandDispatchTests.cs \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/AsyncCommandDispatchTests.cs.meta
git commit -m "test(cqrs): add struct async command dispatch tests"
```

---

### Task 4: 重写 CQRS 使用指南文档

**Files:**
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Documentation/CQRS-Guide.md`（git 已将 `CQRS-DualMode-Guide.md` 重命名为此）

**Interfaces:**
- Consumes: Task 1/2 的最终 API（`IPooledCommand` / `IPooledAsyncCommand` / `Send<T>(Action<T>)` / `SendAsync<T>(Action<T>)`）；既有 struct 接口。

- [ ] **Step 1: 用下列完整内容覆盖 `CQRS-Guide.md`**

```markdown
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
```

- [ ] **Step 2: 验证文档无引用已删除的 API**

```bash
cd /Users/andy/Workspace/github/andycai/fun
grep -nE "ICommandHandler|IAsyncCommandHandler|RegisterCommand|RegisterAsyncCommand|ModeConflictException|DynamicMethod|IL2CPP" \
  UnityProject/Assets/Change/Framework/Cqrs/Documentation/CQRS-Guide.md
```
Expected: 无输出（文档不再引用已删除的旧 Class 模式 API）。

- [ ] **Step 3: 提交**

```bash
cd /Users/andy/Workspace/github/andycai/fun
git add UnityProject/Assets/Change/Framework/Cqrs/Documentation/CQRS-Guide.md
git commit -m "docs(cqrs): rewrite guide for struct + pooled class self-handling modes"
```

---

### Task 5: 全量验证（编译回归 + 全部 Cqrs EditMode 测试）

**Files:**
- 无新增/修改；仅运行验证。

- [ ] **Step 1: 编译回归**

```bash
"/Applications/Unity/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" \
  -logFile "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/cqrs-final-compile.log" \
  -quit
```
Expected: 进程退出码 0，log 无 `error CS` 编译错误。

- [ ] **Step 2: 逐类跑全部 Cqrs EditMode 测试（-testFilter 不支持逗号，逐个运行）**

对以下每个类各跑一次（命令模板，替换 `<CLASS>` 与输出文件名）：

类列表：`PooledCommandDispatchTests`、`PooledAsyncCommandDispatchTests`、`AsyncCommandDispatchTests`、`CommandDispatchTests`、`QueryDispatchTests`、`EventDispatchTests`、`CqrsBootstrapLifecycleTests`、`CqrsArchitectureGuardTests`、`CqrsLoggingIntegrationTests`、`MonitoringTests`、`MonitoringWiringTests`、`SelfHandlingZeroGcTests`、`ZeroAllocationDispatchTests`。

```bash
"/Applications/Unity/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.<CLASS>" \
  -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-<CLASS>.xml" \
  -logFile "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-<CLASS>.log"
```
Expected: 每个类的 XML `failures="0"`、`errors="0"`。

- [ ] **Step 3: 确认改动范围（surgical）**

```bash
cd /Users/andy/Workspace/github/andycai/fun
git diff --stat main -- UnityProject/Assets/Change/Framework/Cqrs UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs
```
Expected: 仅 Cqrs 模块与其测试受影响；无 Pooling / 其它 framework 模块实现文件被改动。

---

## Self-Review 结果

**Spec coverage：**
- 接口（spec §3）→ Task 1 Step 3 ✓
- 同步分发（spec §4）→ Task 1 Step 6 ✓
- 异步分发（spec §4）→ Task 2 Step 6 ✓
- 池来源 / Pool<T>（spec §5）→ Task 1/2 实现用 `Pool<T>` ✓
- ICqrsBus / ICqrsRuntime 签名（spec §6）→ Task 1 Step 4-5、Task 2 Step 4-5 ✓
- 文档重写（spec §7）→ Task 4 ✓
- struct 异步测试（spec §8.1）→ Task 3 ✓
- class 同步测试（spec §8.2）→ Task 1 ✓
- class 异步测试（spec §8.3）→ Task 2 ✓

**Placeholder scan：** 无 TBD/TODO；命令、代码、断言均完整。

**Type consistency：** `IPooledCommand` / `IPooledAsyncCommand` / `Send<T>(Action<T>)` / `SendAsync<T>(Action<T>)` 全文一致；`Pool<T>.InactiveCount` / `Clear` / `Get` / `Release` 与 `Pooling` 模块签名一致。
