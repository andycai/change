# CQRS 池化 class Command 设计

- 日期：2026-06-21
- 模块：`UnityProject/Assets/Change/Framework/Cqrs`
- 关联：`UnityProject/Assets/Change/Framework/Pooling`

## 1. 背景与目标

CQRS 模块当前的 command 全部为 **struct 自处理**（`ICommand.Execute()` / `IAsyncCommand.ExecuteAsync()`），由 `CqrsBus.Send` / `SendAsync` 直接调用，栈分配、0GC，适合高频战斗逻辑。

但部分 command 场景不适合 struct：
- 需要复杂可变状态或异步 I/O（存档、网络、资源加载）。
- 实例本身较重，希望复用而非每次栈拷贝构造。

为此引入 **class 池化自处理 command**：command 是一个 class、自己包含执行逻辑（与 struct 一样自处理，不拆分 command message 与 handler），实例由对象池管理（`Get → Execute → Reset → Release`），适合非热路径。

同时，当前 struct `IAsyncCommand` / `SendAsync` **完全没有测试**，本次一并补齐。

### 目标

1. 为同步与异步 command 增加 class 池化自处理模式，复用 `Change.Framework.Pooling` 的对象池。
2. 保持 struct 自处理模式不变，两轨互不干扰。
3. 补齐 struct 异步 command、class 池化同步 command、class 池化异步 command 的 EditMode 测试。
4. 更新过时的 `CQRS-DualMode-Guide.md` 文档。

### 非目标（YAGNI）

- 不引入 command message + handler 分离的旧 Class 模式（已废弃）。
- 不为 class command 做 0GC 断言（configure lambda + 池化天然非 0GC）。
- 不引入 handler factory 注册 / DI 框架（framework 层零外部依赖）；依赖由 command 自行 resolve。
- 不改动 query / event 路径。

## 2. 架构总览

command 分发进入双轨，均自处理：

| 轨 | 接口 | 分发入口 | 分配语义 | 适用 |
|----|------|---------|---------|------|
| struct 自处理（既有） | `ICommand` / `IAsyncCommand` | `Send<T>(in T)` / `SendAsync<T>(T)` | 栈分配，0GC | 高频热路径 |
| class 池化自处理（新增） | `IPooledCommand` / `IPooledAsyncCommand` | `Send<T>(Action<T>)` / `SendAsync<T>(Action<T>)` | 池复用，少量分配 | 复杂状态 / 异步 I/O |

两轨通过泛型约束（`struct, ICommand` vs `class, IPooledCommand, new()`）区分 `Send` 重载，编译期互斥，运行期无分支。

## 3. 新增接口（`Abstractions/`）

```csharp
namespace Change.Framework.Cqrs
{
    using Change.Framework.Pooling;
    using Cysharp.Threading.Tasks;

    /// <summary>
    /// 池化自处理同步 command。实现为 class，配合 <c>CqrsBus.Send&lt;T&gt;(Action&lt;T&gt;)</c> 分发。
    /// 实例从对象池借出，Execute 后由 bus 调 Reset() 并归还。per-dispatch 数据通过 configure 回调设置。
    /// </summary>
    public interface IPooledCommand : IPoolable
    {
        void Execute();
    }

    /// <summary>
    /// 池化自处理异步 command。实现为 class，配合 <c>CqrsBus.SendAsync&lt;T&gt;(Action&lt;T&gt;)</c> 分发。
    /// bus 在 await 完成后才 Reset() 并归还，避免异步竞态。
    /// </summary>
    public interface IPooledAsyncCommand : IPoolable
    {
        UniTask ExecuteAsync();
    }
}
```

放在单个文件 `Abstractions/IPooledCommand.cs`（两个接口合一文件，与 `ICommand.cs` / `IAsyncCommand.cs` 的拆分风格略有不同，但二者强相关，合并可读性更好）。

> 决策记录：class command **不**实现 `ICommand` / `IAsyncCommand`，避免与 struct 的 `Send(in T)` 语义混淆，靠独立接口 + 独立重载清晰分离。

## 4. 分发 API（`Core/CqrsBus.cs`）

新增两个重载。池化生命周期统一为 `Get → configure → Execute(Async) → Reset → Release`，`Reset + Release` 放在 `finally`，保证 configure 或 Execute 抛异常时实例仍归还。

```csharp
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
        Pool<TCommand>.Release(command); // PoolEngine.Release 内部已调用 Reset()
    }
}

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
        Pool<TCommand>.Release(command); // PoolEngine.Release 内部已调用 Reset()
    }
}
```

### 关键点

- **重载区分**：既有 `Send<TCommand>(in TCommand) where T : struct, ICommand` 与新 `Send<TCommand>(Action<TCommand>) where T : class, IPooledCommand, new()`，参数类型与约束均不同，C# 重载解析可区分。
- **异步生命周期**：`await command.ExecuteAsync()` 完成后（成功或异常）才 `Reset + Release`。ExecuteAsync 进行中实例不会进池，杜绝复用竞态。
- **configure 异常安全**：configure 放进 try，抛异常时 finally 仍 Release；因此 `Reset()` 必须幂等且容忍半配置状态。
- **不加 `[MethodImpl(AggressiveInlining)]`**：class 路径非热路径，池化 + lambda 本就有开销，无需内联提示。
- **Reset 由 Release 内部完成**：`Change.Framework.Pooling` 的 `PoolEngine<T>.Release(item)` 归还前会调用 `item.Reset()`（见 `Pooling/Internal/PoolEngine.cs`），故 bus finally 只 `Release`，不重复 `Reset`。

## 5. 池来源与依赖注入

- 使用 `Change.Framework.Pooling.Pool<T>`（全局静态池，`where T : class, IPoolable, new()`），要求 command 无参构造，**零注册**。
- **per-dispatch 数据**：由 configure 回调设置可变字段，`Reset()` 负责清理。
- **长期服务依赖**：command 在无参构造中自行 resolve（业务层服务定位器；framework 不强制 DI，保持零外部依赖）。`Reset()` 只清 per-dispatch 瞬态字段，不清长期依赖。
- **`Reset()` 契约**：必须幂等；必须容忍"仅 configure 部分 / 未 Execute"的半配置状态被 Reset（异常路径下会发生）。

> 决策记录：不引入 `RegisterPoolFactory<T>(Func<T>)` / InstancePool 注入（YAGNI）。如后续确需构造期依赖注入，再扩展 bus 维护 `Type → IPool` 注册表。

## 6. 接口表面更新

`ICqrsBus` 与 `ICqrsRuntime` 各新增两个方法签名（与 `CqrsBus` 实现一致）。`ICqrsBootstrap` / `ICqrsRegistry` 不变（class command 无需注册）。

## 7. 文档更新

`CQRS-DualMode-Guide.md` 当前描述的 `CommandStruct + HandlerClass` 分离模式、`RegisterCommand`、`ModeConflictException` 等均已在性能迁移中移除，与新代码矛盾。重写为：

- **struct 自处理模式**：`ICommand` / `IAsyncCommand`，0GC 热路径。
- **class 池化自处理模式**：`IPooledCommand` / `IPooledAsyncCommand`，对象池复用，configure 回调传参。
- 删除已不存在的旧 Class 模式、模式互斥、已知限制中关于 DynamicMethod 自处理在 IL2CPP 不可用的描述（struct 自处理现在是直接 constrained callvirt，非 DynamicMethod）。

> 注：struct 自处理当前实现为 `CqrsBus.Send` 内 `var cmd = command; cmd.Execute();`（constrained callvirt，零装箱），非 DynamicMethod。故文档"已知限制"中关于 DynamicMethod 在 IL2CPP/AOT 不可用的条目应删除。

## 8. 测试计划（`Tests/EditMode/Cqrs/`，Edit-only）

assembly `Change.Framework.EditModeTests` 已引用 `Change.Framework`（含 Pooling 与 Cqrs）；UniTask 经 `Change.Framework` 传递可用。

### 8.1 `AsyncCommandDispatchTests.cs`（补 struct 异步）
既有 struct `IAsyncCommand` + `SendAsync<T>` 零测试，补：
- `SendAsync_ExecutesSelfHandlingCommand`：执行后副作用生效。
- `SendAsync_AwaitsCompletion`：用 `UniTask.Delay`/手动完成源验证 await 真正等待。
- `SendAsync_PropagatesException`：ExecuteAsync 抛异常时异常传播到调用方。

### 8.2 `PooledCommandDispatchTests.cs`（class 同步）
- `Send_ExecutesPooledCommand`：Execute 副作用生效。
- `Send_ConfigureSetsFields`：configure 回调设置的 per-dispatch 字段在 Execute 中可见。
- `Send_ResetsAndReleasesAfterExecute`：每次 Execute 后实例归还（`Pool<T>.InactiveCount` 增加），且 `Reset()` 被调用（`PoolEngine.Release` 归还时内部触发，command 内 flag 验证）。
- `Send_ReusesPooledInstance`：连续两次 Send 复用同一实例（捕获引用比较，需注意池 Prewarm/Get 顺序）。
- `Send_ReleaseOnException`：Execute 抛异常时实例仍 Release（InactiveCount 恢复），异常传播。

### 8.3 `PooledAsyncCommandDispatchTests.cs`（class 异步）
- `SendAsync_ExecutesPooledCommand`：异步副作用生效。
- `SendAsync_ConfigureSetsFields`：configure 生效。
- `SendAsync_ReleasesOnlyAfterAwait`：ExecuteAsync 完成前实例未归还（InactiveCount 未增），完成后归还（生命周期核心验证）。
- `SendAsync_ResetsAfterAwait`：await 完成后实例 Release，`Reset()` 随之被调（Release 内部触发）。
- `SendAsync_ReleaseOnException`：ExecuteAsync 抛异常时仍 Release，异常传播。

### 测试约束

- class 路径**不写** 0GC 断言。
- 每个测试前 `Pool<T>.Clear()` 隔离池状态，避免跨测试污染。
- 池复用断言用 `InactiveCount` + 引用相等，不依赖时序。
- 遵循既有测试风格：private 嵌套 command 类型 + sink class，namespace `Change.Framework.Tests`，NUnit `[Test]`。

## 9. 验收标准

1. 新接口与分发 API 编译通过，既有 struct 测试全绿。
2. 三个测试文件全部通过（Unity EditMode）。
3. `CQRS-DualMode-Guide.md` 与代码一致，无引用已删除 API。
4. 改动局限于 Cqrs 模块 + 其测试 + 文档，不触碰 Pooling / 其它 framework 模块实现。

## 10. 风险

- **池跨测试污染**：测试间共享全局 `Pool<T>`。缓解：每个测试 `Pool<T>.Clear()`。
- **异步 Release 时机误判**：若未来有人把 Release 移到 await 前，会引入竞态。缓解：`SendAsync_ReleasesOnlyAfterAwait` 测试锁定该语义。
- **Reset 半配置状态**：异常路径下 Reset 可能在 Execute 前调用。缓解：Reset 契约明确要求幂等 + 容忍半配置；测试 `Send_ReleaseOnException` 验证不泄漏。
