# CQRS 自处理 Command/Query 功能需求文档

> 日期: 2026-06-21 | 状态: 草稿
> 父功能: CQRS 模块重构 - [README.md](./README.md)
> 迭代更新: 替代 `2026-06-18-framework-cqrs-dual-mode-command-query-frd.md`（取消双模式，统一为 Struct 自处理）

## 概述

将 Command/Query 从双模式（Class Handler + Struct SelfHandling）简化为纯 Struct 自处理模式。Command/Query struct 自身实现执行逻辑，不再需要外部 Handler 类注册。异步支持改用 UniTask，消除 Task 相关分配。

## 用户故事

| 角色 | 场景 | 目标 |
|------|------|------|
| UI 业务开发者 | 实现用户操作逻辑（如任务完成、奖励发放） | 定义 Command struct，在构造函数中注入服务依赖，直接 `Send()` 即可执行 |
| 战斗逻辑开发者 | 实现高频 AI 决策命令（每帧数百次） | 使用 readonly struct 的 `Execute()`，栈分配，0GC |
| 异步流程开发者 | 实现网络保存、资源加载等异步操作 | 实现 `IAsyncCommand.ExecuteAsync()` 返回 UniTask，通过 `SendAsync()` 分发 |

## 功能边界

**范围内：**
- **统一 Command 接口：**
  - `ICommand` 新增 `void Execute()` 方法（非 marker）
  - 删除 `ISelfHandlingCommand`（功能合并到 `ICommand`）
  - Command struct 实现 `ICommand` 并提供 `Execute()` 即可被 `Send()` 分发

- **统一 Query 接口：**
  - `IQuery<TResult>` 新增 `TResult Query()` 方法（非 marker）
  - 删除 `ISelfHandlingQuery<TResult>`（功能合并到 `IQuery<TResult>`）
  - Query struct 实现 `IQuery<TResult>` 并提供 `Query()` 即可被 `Ask()` 分发

- **删除 Handler 接口：**
  - 删除 `ICommandHandler<TCommand>`
  - 删除 `IQueryHandler<TQuery, TResult>`
  - 删除 `IAsyncCommandHandler<TCommand>`
  - 删除 `IAsyncQueryHandler<TQuery, TResult>`
  - 删除 `ModeConflictException`

- **异步接口（UniTask）：**
  - `IAsyncCommand`（独立接口，不继承 `ICommand`）：`UniTask ExecuteAsync()`
  - `IAsyncQuery<TResult>`（独立接口，不继承 `IQuery<TResult>`）：`UniTask<TResult> QueryAsync()`
  - 允许一个 struct 同时实现 `ICommand` + `IAsyncCommand`（或 `IQuery` + `IAsyncQuery`）
  - `SendAsync` 约束 `where TCommand : struct, IAsyncCommand`
  - `AskAsync` 约束 `where TQuery : struct, IAsyncQuery<TResult>`

- **简化 Bus 分发：**
  - `Send<TCommand>(in TCommand)` — 直接调用 `command.Execute()`
  - `Ask<TQuery, TResult>(in TQuery)` — 直接调用 `query.Query()`
  - `SendAsync<TCommand>(TCommand)` — 直接调用 `command.ExecuteAsync()`
  - `AskAsync<TQuery, TResult>(TQuery)` — 直接调用 `query.QueryAsync()`
  - 删除 Handler 字典查找、`ResetIfPoolable`、`DynamicMethod` 约束调用

- **删除注册 API：**
  - `ICqrsBootstrap`、`ICqrsRegistry` 中删除 `RegisterCommand`/`RegisterQuery`/`RegisterAsyncCommand`/`RegisterAsyncQuery`
  - `CqrsBus` 中删除对应注册/反注册实现
  - `ICqrsRuntime` 中删除 `Query` 方法（保留 `Ask` 统一命名）

- **Event 保持不动：**
  - `IEventHandler<TEvent>`、`Subscribe/Publish/Unsubscribe` 不变
  - `ICqrsBootstrap` 中 Subscribe 方法保持不变

**范围外：**
- Event 接口和机制不修改
- 性能监控系统（#4 FRD）不修改
- 不引入新的 DI 机制

## 验收条件

- [ ] `ICommand` 新增 `void Execute()`，`ISelfHandlingCommand` 已删除
- [ ] `IQuery<TResult>` 新增 `TResult Query()`，`ISelfHandlingQuery<TResult>` 已删除
- [ ] 所有 Handler 接口（`I*Handler`）和 `ModeConflictException` 已删除
- [ ] `IAsyncCommand.ExecuteAsync()` 返回 `UniTask`，`IAsyncQuery<TResult>.QueryAsync()` 返回 `UniTask<TResult>`
- [ ] `Send` 直接调用 `command.Execute()`，`Ask` 直接调用 `query.Query()`
- [ ] `SendAsync` 约束 `IAsyncCommand`，`AskAsync` 约束 `IAsyncQuery<TResult>`
- [ ] Bus 中不再保留 Command/Query Handler 字典和注册逻辑
- [ ] `RegisterCommand/RegisterQuery/RegisterAsyncCommand/RegisterAsyncQuery` 已从所有接口和实现中删除
- [ ] `ICqrsRuntime.Query` 已删除（保留 `Ask`）
- [ ] Event Subscribe/Publish 功能不受影响
- [ ] 所有现有测试迁移后全部通过
- [ ] Bus 内部不再使用 `DynamicMethod` 和 `ResetIfPoolable`（针对 Command/Query 路径）

## Decisions

| # | 决策点 | 选择 | 理由 | 影响范围 |
|---|--------|------|------|----------|
| 1 | 双模式去留 | 取消双模式，仅保留 Struct 自处理 | 简化 API，减少概念负担，Handler 类带来的 DI/对象池复杂度大于收益 | 整体架构 |
| 2 | ICommand 是否保留 Execute | ICommand 增加 `void Execute()`，ISelfHandlingCommand 删除 | 统一入口，任何 Command 都自带执行逻辑 | 所有 Command 定义 |
| 3 | IAsyncCommand 是否继承 ICommand | 不继承，独立接口 | 区分同步/异步约束，`Send` 只接受 `ICommand`，`SendAsync` 只接受 `IAsyncCommand`；允许 struct 同时实现两个接口 | API 设计 |
| 4 | Query 方法命名 | `IQuery.Query()` / `IAsyncQuery.QueryAsync()` | 与使用方调用 `Ask`/`AskAsync` 语义一致 | 所有 Query 定义 |
| 5 | Handler 删除后注册 API | 全部删除 Command/Query 注册 | 自处理 struct 无需注册，Event 仍保留 Subscribe | 所有注册接口和实现 |
| 6 | 保留 `Query` 别名 | 删除 `ICqrsRuntime.Query`，保留 `Ask` 统一命名 | 减少 API 冗余 | ICqrsRuntime |
| 7 | DynamicMethod 是否保留 | 删除（Command/Query 路径），Event 路径无需 DynamicMethod | 自处理 struct 通过编译器约束调用 `Execute()`/`Query()`，不需要运行时 DynamicMethod | CqrsBus 实现 |
| 8 | ResetIfPoolable 处理 | 删除（Command/Query 路径） | 自处理 struct 不是 IPoolable，不需要 Reset | CqrsBus 实现 |
| 9 | 允许同时实现 ICommand + IAsyncCommand | 允许 | 一个 struct 可同时具备同步/异步能力，按场景选择 `Send` 或 `SendAsync` | 使用灵活性 |

## 未决问题

| # | 问题 | 建议解决阶段 |
|---|------|-------------|
| 1 | Struct 命令需要访问外部服务（如 `QuestSessionState`），通过构造字段注入。但对于复杂命令，字段过多是否影响 struct 设计原则？ | design |
| 2 | 现有业务代码（Quest UI）中的 Class Handler 迁移为 Self-Handling Struct 时，依赖注入方式需要明确模式 | design |

## 约束与假设

- 假设：项目中已安装 `Cysharp.Threading.Tasks`（UniTask）包
- 假设：现有 Class Handler 迁移为 Struct 自处理时，依赖通过 struct 构造函数字段注入
- 约束：严格 0 字节 GC 分配（热路径执行，struct 栈分配保证）
- 约束：Command/Query 只支持 struct 值类型实现
- 约束：Event 接口和机制不在此 FRD 范围内
