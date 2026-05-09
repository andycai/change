# Change 客户端壳层：组合根、窗口宿主与双通道数据流（演进设计）

- Date: 2026-05-09
- Project: `UnityProject/Assets/Change`
- Status: Approved (brainstorming closure)
- Related: `docs/superpowers/specs/2026-04-26-change-ui-application-shell-design.md`（UI 壳 AFA 基线）、`docs/mozi/ideas/2026-05-09-change-huge-game-framework-design-idea.md`（外部参考）

## 0. 与既有基线的关系

2026-04-26 的 **AFA（Application Facade + UI Adapter）** 基线继续有效：薄应用层、显式绑定、`WindowManager` 并发与资源入口统一等目标不变。

本设计文档在基线之上做 **可落地的演进定稿**，解决以下在大型团队中必须写死的问题：

1. **VContainer** 仅存在于 `Change.Runtime` / `GameScript`；`Change.Framework` 保持零第三方依赖，不参与容器装配。
2. **组合根拓扑**：`Root`（引擎/基础设施）与 **`GameRoot`（业务子树）** 的职责边界与释放策略。
3. **`WindowManager` 内聚为 Presenter 宿主**（打开创建/启动，关闭 Dispose），以及 **窗口级 DI 策略 C**（简单 Transient / 复杂 `WindowScope`）。
4. **双通道数据流**：Framework 同步 struct CQRS 与 **热更异步网络 Gateway** 严格分离。
5. **跨模块通信**：混合模型 **C** + **壳阶段强类型事件白名单**。

与 2026-04-26「MVP 不新增平行消息体系」的表述对齐方式：**不在 `Change.Framework` 内新增第二套 CQRS 总线**；网络写走 **热更侧独立 Gateway（UniTask）**，视为应用层基础设施通道，而非 Framework 消息体系的一部分。

## 1. 目标

1. 建立可复制的 **启动与组合** 模型：`Root` 与 **`GameRoot`**，并支持 **整棵 `GameRoot` 释放**（换号、回登录等流程预留）。
2. 建立 **UI 壳工程化模板**：`Change.Runtime.UI.WindowManager` 作为 **Presenter 生命周期宿主**，与 VContainer 生命周期一致。
3. 建立 **数据与通信规范**：本地同步逻辑 vs 网络异步写分离；跨模块通信采用 **混合模型** 且 **壳阶段事件白名单**。

## 2. 非目标（本期）

1. **不引入 ECS**（不选型 Arch/Entitas，不做逻辑—渲染桥接闭环）；战斗/SLG 仿真留在未来域。
2. 不在 `Change.Framework` 引入 VContainer、UniTask、FairyGUI、YooAsset 等第三方包。
3. 本 spec **不**规定具体网络协议、服务器权威状态机细节；仅规定客户端侧 **通道边界与装配位置**。

## 3. 硬约束

1. `Change.Framework.Cqrs`：**仅**承载本地 **同步** struct 消息分发路径；handler **禁止** `async/await`、网络 IO、以及 Unity 引擎耦合（与既有架构守卫测试方向一致）。
2. 所有 **网络写、服务器权威同步编排**：位于 **热更**（`GameScript`）侧，经 **`INetworkGateway`（或等价命名）** 与 UniTask 表达；**不得**通过扩展 Framework `ICommandHandler` 来“假装同步”。
3. VContainer：**仅** `Change.Runtime` / `GameScript` 引用；Framework **不**引用容器、不参与 `Install`。

## 4. 组合与 DI

### 4.1 `Root`（由 `Change.Runtime` / AOT 引导创建）

注册内容限定为 **引擎与基础设施**，例如：日志、YooAsset 适配、平台服务、网络底层（若固定在 AOT）、以及构建 **`GameRoot` 所需的最小父依赖**。

**禁止**将具体玩法 Presenter、业务单例绑定进 `Root`，避免 Root 无限膨胀与热更边界模糊。

### 4.2 `GameRoot`（热更侧创建子 Scope，挂在 `Root` 下）

注册内容：`GameRoot` 子树内的业务服务、窗口工厂、**`INetworkGateway`**、导航/应用状态、**壳阶段白名单应用事件**入口，以及各特性模块 Installer 的挂载点。

**释放策略**：回登录 / 换号时 **Dispose `GameRoot`**。规范要求：**Presenter**、可选 **`WindowScope`**、UniTask 取消源、以及可跟踪资源句柄必须挂在此释放链上，避免泄漏与晚到回调。

### 4.3 热更入口（Install）

HybridCLR 加载完成后，由 Runtime 调用约定热更入口（例如 `IGameHotfixEntry.Install(...)`），将绑定写入 **`GameRoot` 子树**，**不向 `Root` 追加业务绑定**。

## 5. UI：`WindowManager` 与窗口 DI 策略 C

### 5.1 现状事实

`Change.Runtime.UI.WindowManager` 已具备基于 `WindowRequest` / `IWindowFactory` / `IWindowView` 的 **并发打开、复用、inflight、取消** 等语义（见 `WindowManagerCoreTests`）。

### 5.2 演进方向（Presenter 宿主 + 策略 C）

1. **`WindowManager` 为 Presenter 宿主**：`OpenAsync` 成功获得 `IWindowView` 后，进入 **Presenter 启动**；关闭路径 **必须 Dispose Presenter**（及子 Scope 若存在）。
2. **策略 C**  
   - **默认（简单窗）**：Presenter **Transient**；关闭仅释放 presenter 链。  
   - **升级（复杂窗）**：为该窗口实例创建 **`WindowScope`**（`GameRoot` 下的子 Scope），Presenter 与窗内专用服务注册于此；关闭时 **Dispose 整个 `WindowScope`**。  
   - **触发条件（须写入团队规范与 Code Review 清单）**：多 Tab、嵌套子 Presenter、多并行异步加载、需要隔离取消与资源句柄等；满足任一则 **强制** `WindowScope`。

## 6. 数据流：双通道 A

| 通道 | 位置 | 用途 |
|------|------|------|
| Framework CQRS | `Change.Framework` + 注册方 | 纯本地、可同步完成的命令/查询/域事件 |
| 网络 Gateway | `GameScript`（热更） | 写请求、异步回包编排、与服务器权威对齐 |

Presenter / 应用编排层负责 **分流**：本地规则走 `ICqrsBus`；网络写走 `INetworkGateway`，不在 Framework dispatcher 中混写。

## 7. 模块通信：混合 C + 壳阶段事件白名单

1. **同域 / 同特性包内**：优先 **DI + 显式接口** 调用，依赖可见、可测。  
2. **跨大模块通知**：仅允许 **强类型、可枚举、数量可控** 的 **应用级事件**（白名单）；用于登录态、热更完成、语言切换、切号开始/结束等 **通知型** 场景。  
3. **禁止**：在事件 handler 中承载复杂业务流程或隐式编排；网络状态更新优先 **Gateway → 更新 Model → UI 刷新**，仅在需要广播时发应用级事件。

## 8. 错误处理、测试与评审门槛

### 8.1 启动与诊断

启动链须分阶段可观测（日志与错误码策略由实现计划细化）：资源初始化、热更检查、元数据补充、组合根构建、`GameRoot` 构建。

### 8.2 UI 与取消

`WindowManager` 的取消与 inflight 语义继续作为 **防晚到调用** 的底线；Presenter **必须**尊重 `CancellationToken`，与策略 C 的 `WindowScope` 释放一致。

### 8.3 测试

1. 继续以 **EditMode** 覆盖 `WindowManager` 并发/取消/复用（扩展现有测试集而非替代）。  
2. 组合根：提供 **可替换 fake** 的测试入口，避免所有测试必须加载热更 DLL。

### 8.4 Code Review 门槛（最小集）

1. Framework CQRS handler：**无 await、无网络**。  
2. 新窗口：Presenter 生命周期归属与（若触发）**`WindowScope`** 须在 PR 中可解释。  
3. 跨模块通信：新事件类型 **必须**落在白名单流程（评审同意）内。

## 9. 验收标准（设计层）

1. 文档与代码目录约定一致：**Framework 零三方**、VContainer **仅** Runtime/GameScript、`Root` / `GameRoot` 边界清晰。  
2. `WindowManager` 路径上，Presenter 生命周期与窗口打开/关闭 **一一对应**，并与策略 C 一致。  
3. 任意网络写路径 **不**经过 Framework `ICommandHandler` 的 async 伪装。  
4. 壳阶段跨模块通知 **仅**通过白名单应用级事件或显式接口，规则可审计。

## 10. 实现计划入口

本文件为 **设计基线**。实现拆解与任务顺序由后续 **`writing-plans`** 产出的 `docs/superpowers/plans/` 文档承接（不在本 spec 内展开）。
