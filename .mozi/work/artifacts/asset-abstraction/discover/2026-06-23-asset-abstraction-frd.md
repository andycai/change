# 资源管理抽象层 功能需求文档

> 日期: 2026-06-23 | 状态: 草稿

## 概述

为 Unity 项目创建统一的资源管理抽象层，隔离底层 YooAsset 实现细节，让 10+ 人前端团队通过统一接口加载资源，降低学习成本和未来更换资源管理库的迁移成本。

## 用户故事

| 角色 | 场景 | 目标 |
|------|------|------|
| 前端工程师 | 需要加载游戏资源（纹理、音频、预制体等） | 使用统一的接口加载资源，无需了解 YooAsset 的实现细节 |
| 架构师 | 评估或更换底层资源管理库 | 通过抽象层解耦，可以低成本替换底层实现 |
| 测试工程师 | 验证资源加载功能 | 通过集成测试确保接口在真实 Unity 环境中正常工作 |

## 功能边界

**范围内：**
- 提供 `IAssetManager` 接口定义，支持泛型资源加载
- 实现 YooAsset 适配器 `YooAssetManager`
- 支持 `LoadAsync<T>` 加载任意 Unity 资源类型（Texture、AudioClip、Material、Prefab 等）
- 支持 `LoadAndInstantiateAsync` 加载并实例化 GameObject/Prefab
- 支持默认包 + 可选指定包的分包管理
- 资源句柄实现 `IDisposable`，自动管理引用计数
- 支持 `CancellationToken` 取消操作
- 支持 `IProgress<float>` 查询加载进度
- 加载失败抛出详细异常
- 通过 VContainer 依赖注入提供接口
- 提供集成测试覆盖主要场景

**范围外：**
- 不负责 YooAsset 的初始化配置（假设应用启动时已完成）
- 不提供同步加载接口（仅支持异步）
- 不提供强类型资源 ID 系统（仅支持字符串路径）
- 不包含资源预加载专用接口（通过提前调用 LoadAsync 并持有句柄实现）
- 不替换现有 UI 模块的 `YooUiAssetLoader`（可继续使用或后续迁移）
- 不处理资源打包、版本管理、热更新逻辑（属于 YooAsset 配置层职责）

## 验收条件

- [ ] 团队可以通过 VContainer DI 获取 `IAssetManager` 实例
- [ ] 可以通过 `LoadAsync<T>` 加载各类资源（Texture、AudioClip、Material、Prefab）
- [ ] 可以通过 `LoadAndInstantiateAsync` 加载并实例化 GameObject
- [ ] 资源句柄 Dispose 后正确释放 YooAsset 引用计数
- [ ] 支持传入 `CancellationToken` 取消加载操作
- [ ] 支持传入 `IProgress<float>` 查询加载进度
- [ ] 加载失败时抛出包含资源路径和失败原因的异常
- [ ] 集成测试覆盖：加载纹理、加载音频、加载并实例化预制体、取消加载、进度回调

## Decisions

> 以下决策供下游技能（research、design）继承使用。

| # | 决策点 | 选择 | 理由 | 影响范围 |
|---|--------|------|------|----------|
| 1 | 抽象层范围 | 创建通用资源管理抽象层，而非扩展现有 UI 资源加载 | UI 模块已有封装，通用抽象层能为整个团队提供统一接口，降低未来迁移成本 | design, plan |
| 2 | 资源类型支持 | 泛型接口 `LoadAsync<T>` 支持所有 Unity 资源类型 | 灵活且类型安全，未来新增资源类型无需修改接口 | design, plan |
| 3 | 分包管理 | 默认包 + 可选指定包（90% 场景用默认包，特殊情况显式指定） | 简化常见用法，保留灵活性 | design, plan |
| 4 | 生命周期管理 | 返回 `IDisposable` 句柄自动管理引用计数 | 类似现有 `UiAssetLease` 模式，更安全，减少内存泄漏 | design, plan |
| 5 | 加载方式 | 仅提供异步加载接口 | 大型手游应避免同步加载卡顿，统一异步模式配合 UniTask 体验良好 | design, plan |
| 6 | 失败处理 | 加载失败抛出异常 | 与现有 `YooUiAssetLoader` 一致，配合 UniTask 异常处理机制自然 | design, plan |
| 7 | 实例化支持 | 提供 `LoadAsync<T>` 和 `LoadAndInstantiateAsync` 双接口 | 前者用于纹理、音频等不需实例化的资源，后者用于 Prefab，职责清晰 | design, plan |
| 8 | 进度和取消 | 支持 `CancellationToken` + `IProgress<float>` | 大型手游需要显示加载进度，配合 UniTask 是标准做法 | design, plan |
| 9 | 寻址方式 | 仅支持字符串路径 | YooAsset 原生使用字符串，保持简单，未来可在上层封装强类型 | design, plan |
| 10 | 访问方式 | 通过 VContainer 依赖注入 `IAssetManager` | 项目已使用 VContainer，DI 更易测试，符合现有架构 | design, plan |
| 11 | 初始化职责 | 抽象层不负责初始化，假设 YooAsset 在启动时已完成初始化 | YooAsset 初始化涉及复杂配置（运行模式、CDN、版本管理），属于应用级关注点，不应藏在加载抽象层 | design, plan |
| 12 | 测试策略 | 提供集成测试在真实 Unity 环境中验证 | 项目有完善测试基础设施，集成测试能真实验证适配器正确性，为未来替换底层库提供回归测试保障 | plan |

## 未决问题

| # | 问题 | 建议解决阶段 |
|---|------|-------------|
| 1 | YooAsset 的 ResourcePackage 如何获取（单例、注入、还是配置中心）？ | research |
| 2 | 资源句柄的具体设计（是否需要区分 Asset 和 GameObject 两种句柄类型）？ | design |
| 3 | 异常类型设计（是否需要自定义 AssetLoadException）？ | design |
| 4 | 命名空间和目录结构（放在 `Change.Runtime.Asset` 还是其他位置）？ | design |

## 约束与假设

**技术约束：**
- 必须在 Unity 环境中运行
- 依赖 YooAsset 2.3.18（当前项目版本）
- 依赖 UniTask（项目已引用）
- 依赖 VContainer（项目已引用）

**假设：**
- YooAsset 已在应用启动流程中完成初始化
- 默认 ResourcePackage 已创建并可用
- 团队成员熟悉 async/await 和 UniTask 用法
- 集成测试可以在 Unity 编辑器的 PlayMode 或 EditMode 中运行

**工作量评估：**
- 预估 3-5 个工作日（≤ 1 周），不需要拆解为多个 FRD
