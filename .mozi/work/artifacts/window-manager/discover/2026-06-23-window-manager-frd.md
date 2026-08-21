# 窗口管理增强 功能需求文档

> 日期: 2026-06-23 | 状态: 草稿

## 概述

增强现有的 Unity UI 窗口管理系统，提供层级管理、窗口分组互斥、LRU 缓存和延迟释放机制，以支持复杂的多窗口业务场景，同时优化资源加载性能和内存占用。

## 用户故事

| 角色 | 场景 | 目标 |
|------|------|------|
| 玩家 | 在联盟系统中浏览多个窗口（列表、成员管理） | 多个窗口层叠显示，可以点击切换焦点 |
| 玩家 | 从联盟系统切换到商店系统 | 联盟窗口自动关闭，商店窗口打开，再次打开联盟时快速恢复 |
| 玩家 | 在商店中点击购买弹出确认框 | 确认框浮在商店之上，确认后回到商店，商店保持打开状态 |
| 开发者 | 添加新的业务系统（如公会战） | 在业务层注册新窗口，无需修改 Runtime 层代码，自动享受层级、分组、缓存等功能 |
| 开发者 | 查看窗口层级和分组配置 | 在集中的注册代码中能看到所有窗口的元数据 |

## 功能边界

**范围内：**
- 5 层标准窗口层级管理（Background、Normal、Popup、Guide、System）
- 窗口分组与组间互斥（同时只显示一个组的窗口）
- Overlay 特殊组（不参与互斥，可浮在任何组之上，通过 null 或特殊字符串标识，具体方式在 design 阶段确定）
- 全局 LRU 缓存（最多 10 个窗口，包含完整实例）
- 30 秒延迟释放机制（缓存淘汰后进入延迟释放倒计时）
- 窗口注册系统（IWindowRegistry 接口，业务层自由配置）
- 资源加载统一到 IAssetManager（移除 YooUiAssetLoader）
- 窗口唯一性标识（WindowId + 可选 Context 参数，Context 类型和结构在 design 阶段确定）
- 并发打开合并机制（多个调用方同时打开同一窗口，只加载一次）
- 同层级内后打开的窗口在上面，支持手动置顶（BringToFront）

**范围外：**
- 窗口打开/关闭动画（由各窗口 Presenter/View 自己实现）
- 使用 FairyGUI 原生 Window 类（继续使用 GComponent）
- 关闭动画等待（组切换时立即关闭旧组窗口）
- 向后兼容旧接口（可以直接重构现有代码）
- Runtime 层硬编码业务窗口配置（所有业务窗口元数据由业务层注册）
- 窗口拖拽、缩放等交互功能

## 验收条件

- [ ] 可以在 5 个层级中打开窗口，高层级窗口永远在低层级之上
- [ ] 同组窗口可以层叠显示，打开不同组的窗口会关闭旧组所有窗口
- [ ] Overlay 组窗口可以浮在任何组之上，不触发组互斥
- [ ] 窗口关闭后 30 秒内重新打开，直接复用实例（无需重新加载资源）
- [ ] 同时最多缓存 10 个窗口，超出时淘汰最旧的窗口并进入延迟释放
- [ ] 业务层可以通过 IWindowRegistry 接口添加新窗口，无需修改 Runtime 代码
- [ ] 所有窗口资源加载通过 IAssetManager 完成，YooUiAssetLoader 已移除
- [ ] 同一窗口（相同 WindowId + Context）被多个调用方并发打开时，只加载一次
- [ ] 支持参数化窗口（如物品详情窗口 + itemId），不同参数视为不同窗口实例
- [ ] 同层级内后打开的窗口自动在上面，调用 BringToFront 可手动置顶
- [ ] 缓存中的窗口重新打开时，Presenter 的 OnOpen 回调被触发（用于状态重置）

## Decisions

> 以下决策供下游技能（research、design、plan）继承使用。

| # | 决策点 | 选择 | 理由 | 影响范围 |
|---|--------|------|------|----------|
| 1 | 增强方式 | 增强现有 WindowManager | 已有架构合理（异步加载、生命周期管理），避免重复造轮 | design, plan |
| 2 | 是否兼容旧接口 | 不兼容，可直接重构 | 用户明确表示不需要兼容，简化设计 | design, plan |
| 3 | FairyGUI 窗口类型 | 继续使用 GComponent | 更灵活可控，现有代码已基于 GComponent，层级管理需要精细控制 sortingOrder | design, plan |
| 4 | 窗口层级数量 | 5 层标准划分（Background、Normal、Popup、Guide、System） | 覆盖大部分游戏场景，保持简洁 | design, plan |
| 5 | 窗口分组互斥逻辑 | 组间互斥（同时只能显示一个组的窗口） | 符合"不同组的其他所有窗口都要关闭"的需求描述 | design, plan |
| 6 | 特殊组处理 | Overlay 组不参与互斥，可浮在任何组之上 | 支持确认框、Toast 等临时提示类窗口 | design, plan |
| 7 | 延迟释放时长 | 30 秒 | 覆盖常见操作间隔，平衡内存占用和性能 | design, plan |
| 8 | LRU 缓存容量和范围 | 全局 10 个（包含 Overlay） | 简单直观，总内存占用可控，支持跨组复用 | design, plan |
| 9 | FairyGUI 包加载策略 | 混合模式（配置驱动） | 常用小窗口共享包，大型独立窗口单独打包，通过配置灵活控制 | design, plan |
| 10 | 资源加载接口 | 移除 YooUiAssetLoader，统一使用 IAssetManager | 统一资源管理，减少重复代码，Asset 模块已封装 YooAsset | design, plan |
| 11 | 窗口组类型 | string 类型，配合常量避免 GC | 业务层自由定义，可读性强，特殊组易识别（null 或 "Overlay"） | design, plan |
| 12 | 窗口动画处理 | 不在 WindowManager 层处理 | 职责单一，不同窗口可以有不同动画风格，Presenter 控制更灵活 | design, plan |
| 13 | 同层级内排序规则 | 后打开的在上面 + 支持手动置顶 | 符合直觉，灵活，现有 BringToFront 接口已支持 | design, plan |
| 14 | 窗口复用策略 | 自动复用缓存实例 + Presenter 重置状态 | 缓存的意义就是复用，性能优先，状态管理是业务逻辑 | design, plan |
| 15 | 组切换时的关闭时序 | 立即关闭旧组，然后打开新窗口 | 简单清晰，组切换是用户主动操作期望立即看到新界面，不感知动画的 WindowManager 不应等待动画 | design, plan |
| 16 | LRU 缓存淘汰时机 | 窗口关闭时立即检查并淘汰 | 内存占用严格可控，逻辑简单，开销可忽略 | design, plan |
| 17 | 窗口打开失败处理 | 抛出异常由调用方处理 | 现有代码已是此模式，调用方最清楚如何处理，错误信息完整 | design, plan |
| 18 | 窗口关闭后的资源清理范围 | 保留完整实例（资源 + GameObject + GComponent） | 与 LRU 缓存语义一致，恢复最快，内存可控（10 个窗口），业务数据清理由 Presenter 负责 | design, plan |
| 19 | Overlay 组的缓存策略 | 参与 LRU 缓存（与普通窗口相同） | 简单统一，LRU 自动优化，实际场景中 Overlay 不会大量积压 | design, plan |
| 20 | 窗口唯一性标识 | WindowId + 可选参数（Context）作为唯一性 | 支持单例窗口和多实例窗口，向后兼容（不带参数行为与只看 WindowId 相同） | design, plan |
| 21 | 并发打开同一窗口的处理 | 保留 inflight 合并机制 | 现有代码已实现且测试过，避免重复加载，对调用方透明 | design, plan |
| 22 | 窗口元数据配置方式 | Runtime 提供 IWindowRegistry 注册接口，业务层自由配置 | 完全解耦，灵活性最大，业务层可混用多种配置方式（代码、JSON、ScriptableObject 等） | design, plan |
| 23 | 拆解决策 | 不拆分，保持单一 FRD | 用户明确要求不拆分，功能需要一起上线 | design, plan |

## 未决问题

| # | 问题 | 建议解决阶段 |
|---|------|-------------|
| 1 | 现有 WindowManager 的并发控制（_gate 锁）是否需要优化为更细粒度的锁？ | design |
| 2 | IWindowRegistry 是否需要支持运行时注销（Unregister）？ | design |
| 3 | 延迟释放的定时器如何实现（UniTask.Delay 还是 Unity Coroutine）？ | design |
| 4 | LRU 缓存数据结构选择（LinkedList + Dictionary 还是其他）？ | design |
| 5 | Overlay 组的具体标识方式（null、空字符串、还是特殊常量如 "Overlay"）？ | design |
| 6 | WindowRequest 是否需要扩展为 class 以支持更灵活的 Context 参数？当前是 struct | design |
| 7 | IAssetManager 是否已支持加载 FairyGUI 包（UIPackage）？需要确认或扩展 | research |
| 8 | 现有 FairyGuiWindowFactory 如何改造为使用 IAssetManager？ | research |
| 9 | sortingOrder 的分配算法（如何避免溢出，如何支持大量窗口）？ | design |
| 10 | 窗口元数据（Group、Layer）应该存储在哪里（WindowRequest、IWindowView、还是注册表）？ | design |

## 约束与假设

**技术约束：**
- 使用 Unity + FairyGUI + YooAsset 技术栈
- 使用 UniTask 进行异步编程
- 使用 VContainer 作为 DI 容器
- Runtime 层代码为 AOT 编译，不能依赖反射和动态代码生成

**假设：**
- 游戏中同时打开的窗口数量不会超过 20 个（10 个缓存 + 10 个当前显示）
- 单个窗口的资源占用不超过 50 MB（10 个窗口缓存约 500 MB）
- 玩家在不同组之间切换的频率不会超过每秒 1 次
- FairyGUI 包的加载时间在 100-500ms 之间（本地资源）
- 业务层会在游戏启动时完成所有窗口的注册（不支持运行时动态注册）
- 窗口的 Presenter 会正确实现 OnOpen/OnClose 回调进行状态管理

**时间约束：**
- 预估工作量 8-11 天
- 需要包含单元测试和集成测试
