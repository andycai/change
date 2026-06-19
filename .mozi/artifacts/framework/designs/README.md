# CQRS 模块重构 设计总览

## FRD → Design 映射

| FRD | Design 文档 | 切片数 | 依赖 | 状态 |
|-----|------------|--------|------|------|
| [接口层简化与运行时注册](../discover/2026-06-18-framework-cqrs-interface-runtime-registration-frd.md) | [设计文档](./2026-06-18-framework-cqrs-interface-runtime-registration-design.md) | 4 | 无 | 设计中 |
| [双模式 Command/Query](../discover/2026-06-18-framework-cqrs-dual-mode-command-query-frd.md) | 待创建 | — | #1 | 待开始 |
| [Event 双模式订阅](../discover/2026-06-18-framework-cqrs-dual-mode-event-frd.md) | 待创建 | — | #1 | 待开始 |
| [性能验证与迁移](../discover/2026-06-18-framework-cqrs-performance-migration-frd.md) | 待创建 | — | #1, #2, #3 | 待开始 |

## 依赖关系图

```
接口层简化与运行时注册 设计 (#1)
  ├── 双模式 Command/Query 设计 (#2)
  ├── Event 双模式订阅 设计 (#3)
  └── 性能验证与迁移 设计 (#4)
```

## 实施顺序

1. 接口层简化与运行时注册 设计（基础设施，无上游依赖）
2. 双模式 Command/Query 设计 / Event 双模式订阅 设计（可并行，都依赖 #1）
3. 性能验证与迁移 设计（收尾，依赖 #1, #2, #3）
