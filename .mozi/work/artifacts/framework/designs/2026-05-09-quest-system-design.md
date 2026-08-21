# 任务系统（主线 / 支线 / 日常）设计 — MVP

- Date: 2026-05-09
- Project: `UnityProject` — `Change` 框架 + `GameScript` 热更业务
- Scope: 基于现有 CQRS 与 UI 壳层，实现本地假数据的任务窗口（三 Tab）及完整本地交互闭环
- Status: Design baseline（待实现计划拆分）

## 1. 结论与设计立场

在**不引入新横切框架**的前提下，任务系统采用与现有 **Inventory 样例** 一致的分层：

- **Presenter** 仅依赖**用例接口**，不直接依赖 `ICqrsBus`（与 `GameScriptSampleContractTests` 架构约束一致）。
- **用例** 通过 `ICqrsBus` 发送 `readonly struct` 的 Query/Command，由显式注册的 Handler 操作内存状态。
- **FairyGUI 视图** 通过 `Apply(in …ViewModel)` 显式刷新，不在 View 内触达业务总线。

**不采用**第一版即引入 `Fsm` 驱动主线（避免过重）；主线线性用**显式状态字段**（当前任务 id + 进度）即可。

## 2. 目标与非目标

### 2.1 目标

1. 提供一个 **FairyGUI 窗口**，含 **三个 Tab**：主线任务、支线任务、日常任务。
2. **数据源**：本地假数据，**内存模型**；**冷启动**后恢复为初始假数据（无持久化）。
3. **交互**：完整本地闭环 — 进度更新、提交/领奖等流程均在内存中完成；奖励写入 **纯内存假账户**（如整型金币/代币），不接真实背包与服务器。
4. **主线**：**线性** — UI 以「当前一步」为主，完成后解锁下一步。
5. **支线**：**并行列表** — 多条任务独立进度与领奖状态。
6. **日常**：**本会话**内一批日常任务；**不模拟跨天**、不按真实日历重置。
7. **入口**：仅通过 **开发调试路径** 打开任务窗口（不接入正式 Lobby 流程）。
8. **窗口集成**：新 `WindowId`、YooAsset 加载 prefab、`UIPanel` + `GComponent` 根，与现有 `FairyGuiWindowFactory` / `WindowManager` 一致。

### 2.2 非目标（本 spec 范围外）

1. 服务器权威、反作弊、与真实经济/背包联动。
2. 跨天日常、`PlayerPrefs` 或其它本地持久化。
3. 正式大厅入口与运营配置管线（Luban 等可后续再接）。
4. 主线分支剧情树、回退、多结局（可留扩展点，不实现）。

## 3. 领域模型与状态（内存）

### 3.1 假奖励账户

- 由 DI **单例**持有（例如「演示用玩家任务账户」）。
- Command 在合法领奖时更新；Query 供 UI 展示余额或累计领奖摘要。

### 3.2 主线（线性）

- 维护 **当前主线任务标识** 与 **当前进度**（字段含义与假数据表一致即可）。
- 完成当前任务后：推进到下一主线任务（由 Handler 内规则决定）；无下一步时进入明确 UI 状态（如「本章完结」占位文案）。

### 3.3 支线（并行）

- 多条记录，每条至少包含：标识、进度、是否可领奖、是否已领奖（或等价枚举），以满足列表展示与按钮态。

### 3.4 日常（本会话）

- **会话开始**时生成固定集合的日常条目（与冷启动重置一致：每次进程启动重新生成）。
- **默认规则**：每条日常在本会话内 **仅可领取一次奖励**（避免同会话无限刷）；进度仍可按假规则更新至「可领」再领一次。若产品后续改为可重复领，属显式规则变更，需改本句并同步 Handler。

## 4. CQRS 与用例边界

### 4.1 消息形态

- 使用 `readonly struct`，Query/Command 定义于 `GameScript`（或项目约定的热更程序集）中，与现有 CQRS 规范一致。
- Handler 与内存状态驻留在热更侧；注册发生在 **Composition / Installer**（例如扩展 `GameHotfixInstaller` 或项目当前实际使用的等效挂载点）。

### 4.2 Query / Command 划分原则

- **Query**：读模型 — 当前主线展示、支线列表、日常列表、假账户摘要等；实现阶段可合并为 **1～2 个**「整面板 Query」以减少往返，或按 Tab 拆分，以可读性与包大小权衡为准。
- **Command**：写模型 — 例如推进主线、更新支线进度、领取支线奖励、领取日常奖励等；在实现计划中落为 **最小完备集合**，避免重复命令。

### 4.3 非法操作

- **推荐**：Handler 对非法操作 **fail-fast**（如 `InvalidOperationException`），便于 EditMode 断言与调试。
- 若团队更偏好「静默拒绝」，须在实现计划中显式写明并配套测试；本 spec **默认 fail-fast**。

## 5. UI 与 FairyGUI

1. **单个 prefab**：根节点含 `UIPanel`，三个 Tab 与对应内容区（推荐各 Tab 下 `GList` 或等价列表控件）。
2. **WindowId**：在 `Change.Framework`（或项目存放 `WindowIds` 的现有位置）新增常量，例如 `Quest`，与资源定位器约定路径一致。
3. **View 接口**：如 `IQuestWindowView` + `Apply(in QuestWindowViewModel)`；或按 Tab 拆分子 `Apply`，由 Presenter 编排。
4. **Presenter**：`OnOpen` 至少加载默认 Tab 或全面板；**切换 Tab** 时发 Query 刷新该 Tab 数据（避免首帧过重时可懒加载，行为在实现计划中写明）。
5. **禁止**：View 内直接调用 `ICqrsBus` 或修改领域单例。

## 6. 开发入口与依赖注册

1. **打开方式**：在现有 **Demo / 调试驱动**（如 `GameFlowDemoDriver` 或等价类型）中增加打开 `WindowRequest` 的调用路径；不修改 Lobby 正式流程。
2. **DI**：注册内存状态单例、各 Handler、`QuestWindowPresenter` 工厂所需依赖（与 `IWindowPresenterHost` 集成方式遵循现有 Inventory 注册模式）。

## 7. 测试与验收

1. **EditMode**：覆盖 Handler/用例 — 主线顺序推进、支线并行领奖互不影响、日常会话内单条仅领一次奖、非法领奖抛错（若采用 fail-fast）。
2. **可选**：窗口契约类测试参照 `GameScriptSampleContractTests` 扩展，防止 Presenter 直接依赖 Bus 退化。

## 8. 后续扩展（不在本 MVP 交付）

- 服务端同步、日常跨天、本地存档、大厅入口、Luban 配表、与真实背包/任务链编辑器联动。

## 9. 方案备选记录（已定案）

| 方案 | 摘要 | 结论 |
|------|------|------|
| 一 | 对齐 Inventory：多小用例 + 显式 Query/Command | **采用** |
| 二 | 粗粒度门面 + 大块 DTO | 弃用（易膨胀） |
| 三 | 主线 FSM | 弃用（v1 过重） |

---

本文件为已评审设计基线；实现请另附 `docs/superpowers/plans/` 下的分步计划并遵守 `AGENTS.md` 中的分层与测试路径约定。
