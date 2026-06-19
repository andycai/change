# CQRS 模块重构 计划总览

## FRD → Plan 映射

| FRD | Plan 文档 | 任务数 | 依赖 | 状态 |
|-----|----------|--------|------|------|
| [接口层简化与运行时注册](../discover/2026-06-18-framework-cqrs-interface-runtime-registration-frd.md) | [实现计划](./2026-06-18-framework-cqrs-interface-runtime-registration-plan.md) | 11 | 无 | 待开始 |
| [双模式 Command/Query](../discover/2026-06-18-framework-cqrs-dual-mode-command-query-frd.md) | 待创建 | — | #1 | 待开始 |
| [Event 双模式订阅](../discover/2026-06-18-framework-cqrs-dual-mode-event-frd.md) | 待创建 | — | #1 | 待开始 |
| [性能验证与迁移](../discover/2026-06-18-framework-cqrs-performance-migration-frd.md) | 待创建 | — | #1, #2, #3 | 待开始 |

## 依赖关系图

```
接口层简化与运行时注册 计划 (#1)
  ├── 双模式 Command/Query 计划 (#2)
  ├── Event 双模式订阅 计划 (#3)
  └── 性能验证与迁移 计划 (#4)
```

## 实施顺序

1. 接口层简化与运行时注册 计划（基础设施，无上游依赖）
2. 双模式 Command/Query 计划 / Event 双模式订阅 计划（可并行）
3. 性能验证与迁移 计划（收尾）
