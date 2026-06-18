# Change UI 应用编排与壳层框架设计（MVP）

- Date: 2026-04-26
- Project: `UnityProject/Assets/Change`
- Scope: 在已落地 `Change.Framework.Cqrs` 基础上，为业务层补齐应用编排与 UI 壳层能力
- Status: Approved (design baseline)

## 1. 结论与设计立场

在当前项目中，**需要补一套“类似 MVC”的框架能力**，但不建议引入传统重 MVC/MVVM 大框架。  
选型为：**AFA（Application Facade + UI Adapter）**，即“薄应用层 + 薄 UI 壳层”。

核心原因：

1. 优先级是性能与边界可控（优先 B），不适合引入反射绑定和重型通用框架。
2. 已有 CQRS 已解决消息与执行语义，但还缺业务流程编排、窗口生命周期、资源加载统一入口。
3. 未来 UI 以 FairyGUI 为主，且要统一 YooAsset + UniTask 资源加载与窗口管理（缓存、层级、并发）。

## 2. 目标与非目标

## 2.1 目标

1. 建立严格分层：`Framework` / `Runtime` / `GameScript` 各司其职。
2. 业务交互强制通过应用层接口（UseCase/Facade + CQRS），UI 不得直接改业务状态。
3. 禁用运行时反射绑定，采用显式调用路径，保证热路径可预测。
4. 提供可复用窗口管理与资源加载能力，减少业务侧重复造轮子。
5. 以 1~2 个业务场景完成 MVP 验证，再增量扩展。

## 2.2 非目标（MVP 不做）

1. 不做完整 MVVM 自动绑定系统。
2. 不做一次性全量 UI 框架（导航图可视化、全局动画编排、重型编辑器工具）。
3. 不替代现有 CQRS 总线，不新增平行消息体系。
4. 不在 MVP 引入反射扫描、动态注入或 AOP 管线。

## 3. 分层架构与程序集归属

## 3.1 `Change.Framework`（纯抽象，零三方依赖）

职责：

1. 定义应用编排最小契约：`IUseCase`/`IAppFacade`/`IPresenter` 等。
2. 定义 UI 窗口协议：窗口标识、窗口状态枚举、生命周期回调抽象。
3. 定义统一错误模型抽象（框架级错误码/结果对象协议）。

约束：

- 不引用 Unity/FairyGUI/YooAsset/UniTask。
- 不包含业务实现。

## 3.2 `Change.Runtime`（适配与基础设施）

职责：

1. FairyGUI 适配：窗口壳基类、视图生命周期驱动、UI 组件显式绑定辅助。
2. YooAsset + UniTask 统一加载器：资源定位、加载、取消、超时、释放。
3. WindowManager：层级管理、缓存策略、实例复用、并发打开去重。

约束：

- 不直接包含业务流程判断。
- 不绕过应用层直接触达业务状态。

## 3.3 `GameScript`（业务实现，热更）

职责：

1. 实现具体 `Presenter/Controller` 与 `UseCase/Facade`。
2. 通过 `ICqrsBus.Send/Query/Publish` 执行业务读写。
3. 输出 ViewModel 并驱动 View 显式刷新。

约束：

- UI 逻辑必须经 Presenter -> UseCase/Facade -> CQRS。
- 禁止 View 直接改业务状态。

## 4. 核心组件设计（MVP）

## 4.1 WindowManager（`Change.Runtime`）

能力：

1. 统一 `Open/Close/Hide/Show/BringToFront`。
2. 支持窗口层级：`Bottom/Normal/Popup/Top`（后续可扩展）。
3. 窗口缓存策略：单例窗口复用、可多开窗口按实例 ID 管理。
4. 并发去重：同一窗口同一时刻多次 `Open` 请求合并。

边界：

- 只管理窗口壳生命周期，不存业务状态。

## 4.2 UIAssetLoader（`Change.Runtime`）

能力：

1. 封装 YooAsset 异步加载并以 UniTask 暴露统一接口。
2. 统一处理超时、取消、失败映射与资源释放。
3. 向 WindowManager 提供“可实例化的 UI 资源句柄”。

边界：

- 不泄漏 YooAsset 细节到 GameScript 业务层。

## 4.3 Presenter + UseCase/Facade（`GameScript`）

能力：

1. Presenter 处理 View 事件、参数校验、流程编排。
2. UseCase/Facade 承接业务动作，统一走 CQRS。
3. 显式构建 ViewModel 并调用 View `Apply(viewModel)` 更新展示。

边界：

- Presenter 不直接访问底层资源加载器与 FairyGUI 原生对象。
- View 不包含业务决策逻辑。

## 5. 数据流与执行语义

统一单向数据流：

`View(用户输入)` -> `Presenter` -> `UseCase/Facade` -> `ICqrsBus` -> `ViewModel` -> `View.Apply(viewModel)`

语义规则：

1. 禁用运行时反射绑定与隐式双向绑定。
2. 视图刷新必须显式调用，不依赖魔法订阅。
3. CQRS 保持同步语义；UI 异步仅用于资源加载与展示时序控制。

## 6. 错误处理与可观测性

## 6.1 错误分层

1. Runtime 层输出 `UiError`（如 `AssetNotFound`、`WindowBusy`、`OpenTimeout`）。
2. 业务层输出业务错误码/失败结果，避免直接抛底层异常给 View。
3. View 层只负责展示与用户交互反馈（重试/取消/提示）。

## 6.2 可观测指标（MVP）

1. 窗口打开耗时（P50/P95）。
2. 资源加载耗时与失败率。
3. 活跃窗口数、缓存命中率。
4. 并发打开去重命中次数。

## 7. 性能约束（硬约束）

1. 禁止运行时反射绑定（已确认）。
2. 高频交互路径避免闭包与临时对象。
3. 允许首次加载分配；稳态重复打开应可预测并受控。
4. 业务状态读取与修改必须通过 CQRS，避免旁路导致不可控分配/时序。

## 8. 测试与验收标准

## 8.1 功能测试

1. 窗口打开/关闭/显示/隐藏行为正确。
2. 窗口层级挂载正确，前后台切换正确。
3. 重复打开同一窗口命中复用或实例策略。
4. 资源加载失败、超时、取消可恢复且无脏实例残留。

## 8.2 架构守卫测试

1. UI 不可绕过 Presenter 直接写业务状态。
2. Presenter 必须通过 UseCase/Facade 调用 CQRS。
3. Runtime 不依赖业务程序集（保持方向单向）。

## 8.3 性能基线测试

1. 稳态重复打开同一窗口的 GC 分配在阈值内。
2. 高频 UI 事件处理无明显持续性分配峰值。

## 9. MVP 范围与落地策略

MVP 交付物：

1. `Framework`：应用层与窗口协议最小集合。
2. `Runtime`：WindowManager + UIAssetLoader + FairyGUI 适配壳层。
3. `GameScript`：至少 1~2 个业务窗口全链路接入样例。

落地节奏：

1. 先建最小骨架与一个典型窗口。
2. 用第二个窗口验证复用性与边界稳定性。
3. 验证通过后再扩展导航动画、调试能力等增强项。

## 10. 后续可扩展方向（非 MVP）

1. 窗口过渡动画策略接口。
2. 主题/皮肤切换能力。
3. 列表虚拟化增强组件。
4. 调试面板与性能埋点可视化。

## Implementation Status

- MVP implementation tracked by `docs/superpowers/plans/2026-04-26-change-ui-application-shell-plan.md`.
- Framework contracts, runtime shell core, and GameScript sample are validated by EditMode tests.
