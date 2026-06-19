# CQRS 模块重构总览

## 功能拆解

| FRD | 工作量 | 依赖 | 状态 |
|-----|--------|------|------|
| [接口层简化与运行时注册](./2026-06-18-framework-cqrs-interface-runtime-registration-frd.md) | 3 天 | 无 | 待开始 |
| [双模式 Command/Query](./2026-06-18-framework-cqrs-dual-mode-command-query-frd.md) | 4 天 | #1 | 待开始 |
| [Event 双模式订阅](./2026-06-18-framework-cqrs-dual-mode-event-frd.md) | 2 天 | #1 | 待开始 |
| [性能验证与迁移](./2026-06-18-framework-cqrs-performance-migration-frd.md) | 2 天 | #1, #2, #3 | 待开始 |

## 依赖关系图

```
接口层简化与运行时注册 (#1)
  ├── 双模式 Command/Query (#2)
  ├── Event 双模式订阅 (#3)
  └── 性能验证与迁移 (#4)
```

## 实施顺序

1. 接口层简化与运行时注册（基础设施）
2. 双模式 Command/Query / Event 双模式订阅（可并行）
3. 性能验证与迁移（收尾验证）

## 背景

基于 idea 文件 `.mozi/artifacts/framework/ideas/2025-06-18-change-framework-cqrs-refactor-idea.md` 和现有设计文档 `.mozi/artifacts/framework/designs/2026-05-07-cqrs-architecture-review-and-optimization-design.md`，针对以下痛点进行重构：

1. 高频调用场景 GC 压力
2. Handler 类实例化性能问题
3. 动态模块加载需求
4. 接口层次冗余

## 核心目标

- **性能：** 严格 0 字节 GC 分配
- **灵活性：** 支持运行时动态注册/反注册
- **简洁性：** 精简接口层次，减少概念负担
- **兼容性：** 完全不兼容旧代码，需全部改写
