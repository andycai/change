# CQRS Event 双模式订阅 功能需求文档

> 日期: 2026-06-18 | 状态: 草稿
> 父功能: CQRS 模块重构 - [README.md](./README.md)

## 概述

简化 Event 订阅机制，支持 Class Handler 和静态委托两种订阅方式，消除不必要的 Handler 类，同时保持 0GC 承诺。

## 用户故事

| 角色 | 场景 | 目标 |
|------|------|------|
| 业务开发者 | 实现复杂的领域事件处理（如多步骤状态更新、需要 DI） | 使用 Class Handler 实现 `IEventHandler<TEvent>` |
| 监控开发者 | 收集事件日志、指标统计 | 使用静态委托订阅，简洁轻量 |
| 框架维护者 | 防止闭包捕获导致 GC 分配 | 编译时或运行时检测闭包，拒绝捕获变量的委托 |

## 功能边界

**范围内：**
- **Class Handler 模式：**
  - 实现 `IEventHandler<TEvent>`
  - 支持 DI 构造注入
  - 注册：`RegisterEventHandler<TEvent>(IEventHandler<TEvent> handler)`
  - 允许多个 Handler 订阅同一 Event（1:N）

- **静态委托模式：**
  - 注册：`Subscribe<TEvent>(Action<TEvent> handler)` 或 `Subscribe<TEvent>(静态方法引用)`
  - **严格禁止闭包捕获**：运行时检测 `handler.Target != null` 时抛异常
  - 允许多个委托订阅同一 Event

- **发布 API：**
  - `Publish<TEvent>(in TEvent @event)` - 调用所有订阅者（Class Handler + 委托）
  - 失败策略：聚合异常（`AggregateException`），不中断其他订阅者

- **反注册：**
  - `UnregisterEventHandler<TEvent>(IEventHandler<TEvent> handler)`
  - `Unsubscribe<TEvent>(Action<TEvent> handler)` - 需精确匹配委托实例

**范围外：**
- Command/Query 处理（#2 FRD）
- Event 异步处理（当前不支持，未来扩展）
- Event 发布顺序保证（按注册顺序调用，但不保证跨类型顺序）

## 验收条件

- [ ] Class Handler 订阅和调用正常工作
- [ ] 静态委托订阅正常工作
- [ ] 闭包检测正常：捕获变量的委托注册时抛异常
- [ ] 同一 Event 多订阅者都被调用
- [ ] `AggregateException` 聚合失败，不影响其他订阅者
- [ ] 反注册正常工作
- [ ] 单元测试覆盖两种订阅模式和闭包检测
- [ ] 0GC 分配初步验证（详细验证在 #4）

## Decisions

| # | 决策点 | 选择 | 理由 | 影响范围 |
|---|--------|------|------|----------|
| 1 | Event 订阅双模式 | Class Handler + 静态委托 | 平衡复杂处理和轻量订阅 | API 设计 |
| 2 | 闭包检测 | 运行时检测 `handler.Target != null` | 编译时检测需要 Roslyn 分析器，运行时检测实现简单 | 注册逻辑 |
| 3 | 失败策略 | `AggregateException` 聚合，不中断 | Event 是"已发生的事实"，不应因某个订阅者失败而中断 | 发布逻辑 |
| 4 | 订阅顺序 | 按注册顺序调用 | 可预测性，便于调试 | 发布逻辑 |
| 5 | 统一 IEvent | 移除 `IDomainEvent` | 减少概念负担（决策继承自 #1） | 所有 Event 定义 |

## 未决问题

| # | 问题 | 建议解决阶段 |
|---|------|-------------|
| 1 | 静态委托如果需要访问外部服务，是否允许通过服务定位器？还是强制使用 Class Handler？ | design |
| 2 | 是否需要支持"条件订阅"（如只订阅满足特定条件的 Event）？ | explore（评估需求） |
| 3 | 是否需要支持订阅优先级（高优先级订阅者先执行）？ | explore（评估需求） |

## 约束与假设

- 假设：Event 订阅/反注册频率较低（不在热路径）
- 假设：静态委托主要用于简单场景（日志、指标），不需要复杂依赖
- 约束：严格禁止闭包捕获，保持 0GC
- 约束：Event Handler 不允许 dispatch Command（架构约束，继承自 2026-05-07 设计）
