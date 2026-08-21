# CQRS 双模式 Command/Query 功能需求文档

> 日期: 2026-06-18 | 状态: 草稿
> 父功能: CQRS 模块重构 - [README.md](./README.md)

## 概述

实现 Command/Query 双模式执行：Class 模式（复杂业务逻辑，支持 DI 和对象池）和 Struct 模式（高频战斗逻辑，自处理，0GC）。

## 用户故事

| 角色 | 场景 | 目标 |
|------|------|------|
| UI 业务开发者 | 实现复杂的用户操作逻辑（如任务完成、奖励发放） | 使用 Class Handler，注入依赖服务，复用对象池实例 |
| 战斗逻辑开发者 | 实现高频 AI 决策命令（每帧数百次） | 使用 Struct 自处理，栈分配，0GC |
| 异步流程开发者 | 实现网络保存、资源加载等异步操作 | 使用 Class Handler 的 `ExecuteAsync` 方法 |

## 功能边界

**范围内：**
- **Class 模式：**
  - Handler 实现 `ICommandHandler<TCommand>` 和 `IPoolable`
  - 支持 DI 构造注入
  - 集成 `Change.Framework.Pooling` 对象池
  - 框架自动调用 `Reset()`（`Handle()` 执行后）
  - 支持异步：`IAsyncCommandHandler<TCommand>` 提供 `ExecuteAsync()`
  - Query 同理：`IQueryHandler<TQuery, TResult>` 和 `IAsyncQueryHandler<TQuery, TResult>`
  
- **Struct 模式：**
  - Command/Query struct 实现 `ICommand.Execute()` 或 `IQuery<TResult>.Execute()`
  - 无需注册 Handler，直接调用 struct 方法
  - 栈分配，0GC
  - 仅支持同步执行

- **注册分离：**
  - Class 模式：`RegisterCommandHandler<TCommand>(ICommandHandler<TCommand> handler)`
  - Struct 模式：无需注册，执行时直接调用 `command.Execute()`

- **执行 API：**
  - `Send<TCommand>(in TCommand command)` - 自动识别 Class 或 Struct 模式
  - `SendAsync<TCommand>(TCommand command)` - 仅支持 Class 模式异步 Handler
  - `Ask<TQuery, TResult>(in TQuery query)` - 自动识别模式
  - `AskAsync<TQuery, TResult>(TQuery query)` - 仅支持 Class 模式异步 Handler

**范围外：**
- Event 处理（#3 FRD）
- 性能验证测试（#4 FRD）
- 具体业务代码迁移示例（#4 FRD）

## 验收条件

- [ ] Class 模式 Handler 正常工作，对象池集成成功
- [ ] `Reset()` 自动调用，无泄漏
- [ ] 异步 Handler（`ExecuteAsync`）正常工作
- [ ] Struct 模式自处理执行路径正常工作
- [ ] `Send/Ask` API 自动识别 Class/Struct 模式
- [ ] Command/Query 注册冲突检测正常（Class 已注册时 Struct 调用报错，反之亦然）
- [ ] 单元测试覆盖两种模式
- [ ] 0GC 分配初步验证（详细验证在 #4）

## Decisions

| # | 决策点 | 选择 | 理由 | 影响范围 |
|---|--------|------|------|----------|
| 1 | 双模式并存 | Class（DI + 对象池）+ Struct（自处理） | 平衡复杂业务和高频性能需求 | 整体架构 |
| 2 | 对象池管理 | 使用 `Change.Framework.Pooling`，框架自动 `Reset()` | 统一池实现，防止泄漏 | Class Handler 生命周期 |
| 3 | 异步支持 | 仅 Class 模式支持 `ExecuteAsync` | 高频 Struct 场景不需要异步 | API 设计 |
| 4 | 模式识别 | 编译时（通过接口约束）+ 运行时检测 | Struct 实现 `ICommand.Execute()` 则走 Struct 路径 | 执行路径分发 |
| 5 | 对象池职责边界 | 用户负责创建和注册 Handler 实例（通过 DI 或工厂），框架负责执行后调用 `Reset()` 但不负责 Return 到池（用户自行管理池生命周期） | 职责清晰，避免框架侵入用户池管理 | 注册 API 和生命周期设计 |

## 未决问题

| # | 问题 | 建议解决阶段 |
|---|------|-------------|
| 1 | Struct 模式如果需要访问外部服务（如 `QuestSessionState`），如何传递？通过 Struct 字段还是全局服务定位器？ | design |
| 2 | 异步 Command 执行时如果用户立即反注册 Handler，是否需要取消执行？ | design |
| 3 | Class 和 Struct 模式能否混用（同一个 Command 类型）？还是编译时互斥？ | design |

## 约束与假设

- 假设：Class Handler 的对象池由 DI 容器或用户手动管理
- 假设：Struct 模式的 Command/Query 是简单逻辑，不需要复杂依赖
- 约束：严格 0 字节 GC 分配（热路径执行）
- 约束：Struct 模式不支持异步
