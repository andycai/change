# CQRS 接口层简化与运行时注册 功能需求文档

> 日期: 2026-06-18 | 状态: 草稿
> 父功能: CQRS 模块重构 - [README.md](./README.md)

## 概述

精简 CQRS 接口层次，统一事件接口，支持运行时动态注册/反注册以满足动态模块加载需求。

## 用户故事

| 角色 | 场景 | 目标 |
|------|------|------|
| 框架使用者 | 按需加载游戏功能模块（如特定副本系统） | 运行时注册该模块的 Command/Query/Event Handler |
| 框架使用者 | 模块卸载时 | 反注册该模块的所有 Handler，释放资源 |
| 框架维护者 | 理解 CQRS 接口设计 | 接口层次清晰，职责明确，无冗余中间层 |

## 功能边界

**范围内：**
- 统一 `IEvent` 接口，移除 `IDomainEvent`
- 精简 `ICqrsRegistry`、`ICqrsRuntime`、`ICqrsBus` 接口定义
- 去掉 `ICqrsRuntimeProvider`、`CqrsContextRuntimeProvider` 中间层
- `CqrsBus` 双接口模式：同时实现 `ICqrsRegistry` 和 `ICqrsRuntime`
- 支持运行时主线程注册/反注册
- Command/Query 禁止重复注册，Event 允许追加订阅
- 线程安全由调用者保证（主线程注册约束）

**范围外：**
- Command/Query 双模式执行逻辑（#2 FRD）
- Event 双模式订阅实现（#3 FRD）
- 对象池集成（#2 FRD）
- 性能验证（#4 FRD）

## 验收条件

- [ ] `IDomainEvent` 已移除，所有引用改为 `IEvent`
- [ ] `ICqrsRuntimeProvider`、`CqrsContextRuntimeProvider` 已删除
- [ ] `CqrsBus` 同时实现 `ICqrsRegistry` 和 `ICqrsRuntime`
- [ ] 运行时注册 API 可用：`Register<TCommand>(handler)`、`Unregister<TCommand>()`
- [ ] 重复注册检测正常工作：Command/Query 抛异常，Event 追加成功
- [ ] 单元测试覆盖注册/反注册场景
- [ ] 线程安全使用约束文档完成

## Decisions

| # | 决策点 | 选择 | 理由 | 影响范围 |
|---|--------|------|------|----------|
| 1 | 是否保留 `IDomainEvent` | 统一为 `IEvent` | 减少概念负担，用命名约束区分领域事件 | 所有 Event 定义，#3 FRD |
| 2 | Bootstrap/Runtime 生命周期分离 | 放弃硬分离，采用双接口模式 | 支持运行时注册需求 | 整体架构，#2、#3 FRD |
| 3 | 运行时注册时机 | 主线程注册，调用者保证线程安全 | 简化实现，Unity 主线程模型契合 | 使用文档，模块加载流程 |
| 4 | 重复注册行为 | Command/Query 禁止，Event 追加 | Command/Query 1:1 语义，Event 1:N 语义 | #2、#3 FRD 实现 |
| 5 | 反注册需求 | 支持 `Unregister` API | 模块卸载时释放资源 | 模块生命周期管理 |

## 未决问题

| # | 问题 | 建议解决阶段 |
|---|------|-------------|
| 1 | 运行时注册时如果正在执行 Command 会怎样？是否需要延迟到下一帧？ | design |
| 2 | 反注册时如果有异步 Command 正在执行，是否需要等待完成？ | design |

## 约束与假设

- 假设：模块加载/卸载总是在主线程进行
- 假设：注册/反注册频率较低（不在热路径）
- 约束：不引入分布式/跨进程能力
- 约束：保持现有 `CqrsBus` 的 0GC 分配性能（注册阶段可以分配）
