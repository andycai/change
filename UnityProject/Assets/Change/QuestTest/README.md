# Quest system test assets（任务系统测试资源）

本目录提供 **无需 FairyGUI 编辑器导出包** 即可加载的任务窗口预制体，以及 YooAsset 收集器配置入口。

## 资源布局

| 路径 | 说明 |
|------|------|
| `Prefabs/quest_panel.prefab` | 根节点挂 `QuestFairyGuiRootSource`，运行时由 `QuestUiRootBuilder` 生成与 `QuestFairyGuiView` 一致的 `GComponent` 树。 |
| `../YooSpaceShooter/GameSetting/AssetBundleCollectorConfig.xml` | 已增加对 `Assets/Change/QuestTest/Prefabs` 的 Collector；`AddressByFileName` 下地址为 **`quest_panel`**。 |

`GameHotfixRootScope` 中任务窗口地址已配置为 **`quest_panel`**（与收集器一致）。

## 运行时加载链

1. `WindowManager` → `FairyGuiWindowFactory` 加载 `quest_panel`。
2. 若 prefab 上 **无可用 `UIPanel.ui`**，则使用 `IFairyGuiWindowRootSource`（`QuestFairyGuiRootSource`）提供 `GComponent` 根节点。

## Unity 内手动验收步骤

1. 打开 **YooAsset** 资源收集窗口，确认 `DefaultPackage` 已包含 `QuestTest/Prefabs` 组（见 `AssetBundleCollectorConfig.xml`）。
2. 执行 **构建资源 / Build**（具体菜单以 YooAsset 版本为准），保证包内存在地址 `quest_panel`。
3. 在场景中放置：
   - 任意带 `ResourcePackage` 的 Yoo 引导对象（与项目现有 Boot 流程一致）；
   - `GameHotfixRootScope`，将 `_uiPackage` 指向上一步构建使用的 `ResourcePackage`，`_engineScope` 指向父级 `LifetimeScope`（可为空则仅用于本地试跑时自行保证 VContainer 父引用）。
   - `GameFlowDemoDriver`，将 `_hotfixScope` 拖到该 `GameHotfixRootScope`。
4. **Play**：按 **F4** 打开任务窗口；主线/支线/日常数据来自内存种子，领奖会累加金币显示在 `txtWallet`。

## 自动化测试

- **EditMode**：`GameScript.EditModeTests` — `QuestSessionStateTests`、`QuestUiRootBuilderTests`。
- **契约**：`Change.Runtime.EditModeTests` — `GameScriptSampleContractTests` 中 Quest 相关用例。

## 与正式 FairyGUI 包的关系

若日后改用 FairyGUI 导出的 `UIPackage` + `UIPanel`：

- 可在 prefab 上去掉 `QuestFairyGuiRootSource`，改为标准 `UIPanel`（`packageName` / `componentName` / `packagePath`）；  
- `FairyGuiWindowFactory` 会优先使用 `UIPanel.ui`，无需 `IFairyGuiWindowRootSource`。
