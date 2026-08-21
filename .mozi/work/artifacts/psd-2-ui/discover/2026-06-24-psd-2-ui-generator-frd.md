# Unity Prefab 生成与代码框架 功能需求文档

> 日期: 2026-06-24 | 状态: 草稿
> 父功能: PSD 转 UGUI 自动化工具链 ([README](./README.md))
> 依赖: [PSD 解析与智能分析管线](./2026-06-24-psd-2-ui-parser-frd.md) (FRD #1)

## 概述

开发 Unity Editor 插件，读取 FRD #1 输出的 JSON 配置文件，自动生成带自适应锚点和 Layout 组件的 uGUI Prefab，并生成对应的 C# View 基础代码框架。同时提供修正机制，支持程序员手动调整后的增量更新。

## 用户故事

| 角色 | 场景 | 目标 |
|------|------|------|
| 程序员 | 拿到 FRD #1 生成的 JSON 配置和图片资产 | 在 Unity 中运行菜单命令，自动生成可用的 Prefab 和 View 基础代码，快速进入业务逻辑开发 |
| 程序员 | Prefab 生成后发现某个组件的锚点不符合预期 | 在 Unity 编辑器中手动调整锚点，标记为"锁定"，下次重新生成时保留这个调整 |
| 程序员 | 美术更新了 PSD 中的按钮样式 | 重新运行工具，只有按钮图片资产更新，之前手动添加的事件绑定代码和锚点调整保持不变 |
| 程序员 | 查看生成的 View 代码 | 看到所有交互组件的字段声明、空事件方法和基础绑定，直接在 TODO 位置填写业务逻辑 |

## 功能边界

**范围内：**
- Unity Editor 插件开发（放置在 `UnityProject/Assets/Change/Editor/`）
- 读取 JSON 配置文件和图片资产
- 自动导入图片资产，应用九宫格设置（通过 `TextureImporter.spriteBorder`）
- 锚点自动推导引擎，覆盖 6 种移动端常见场景：
  1. 全屏拉伸（背景）
  2. 右上角固定（关闭按钮）
  3. 底部居中（导航栏）
  4. 水平拉伸（标题栏）
  5. 垂直拉伸（侧边栏）
  6. 居中固定（弹窗）
- Layout Group 自动挂载（Vertical/Horizontal/Grid），带模板折叠逻辑
- Prefab 组装：
  - 正确的组件层级结构
  - 组件类型（Image, Text, Button, ScrollRect, InputField 等）
  - RectTransform 属性（锚点、位置、大小）
  - Layout 组件（LayoutGroup, ContentSizeFitter）
- C# View 代码生成（标准版本）：
  - 字段声明（`[SerializeField] private Button btnClose;`）
  - 空事件方法（`private void OnCloseClicked() { // TODO }`）
  - Start 方法中的基础绑定（`btnClose.onClick.AddListener(OnCloseClicked);`）
- 修正机制：
  - JSON 配置文件支持手动编辑修正
  - Unity 中手动调整可标记"锁定"（通过自定义组件 `PSD2UILock`）
  - 增量更新：只更新变化部分，保留锁定的调整和手动编写的代码
- Unity 菜单命令：`Assets/PSD2UGUI/Generate Prefab from Config`

**范围外：**
- 不解析 PSD 文件（由 FRD #1 实现）
- 不生成完整的 MVVM/MVC 架构代码（仅生成基础框架）
- 不生成业务逻辑代码（程序员手动填写 TODO）
- 不支持运行时动态加载 PSD（仅编辑器工具）
- 不支持 UI Toolkit（仅 uGUI）
- 不处理动画/特效（仅静态结构）
- 不提供可视化编辑器（在 Unity 原生编辑器中调整）

## 验收条件

### Phase 1：图片导入与九宫格（必须）
- [ ] 正确导入 JSON 配置中引用的所有图片资产到 Unity 项目
- [ ] 根据 JSON 中的九宫格数据，自动设置 `TextureImporter.spriteBorder`
- [ ] 九宫格应用后，在 Sprite Editor 中可见正确的边界线

### Phase 2：Prefab 基础组装（必须）
- [ ] 根据 JSON 图层树正确构建 GameObject 层级结构
- [ ] 正确识别组件类型并挂载对应组件（Image, Text, Button, ScrollRect, InputField）
- [ ] Text 组件使用 TextMeshProUGUI（如果项目已集成 TMP）或 Text（原生 uGUI）
- [ ] 组件的初始属性设置正确（如 Button 的 TargetGraphic、InputField 的 Placeholder）

### Phase 3：锚点自动推导（必须）
- [ ] 全屏拉伸：检测到节点宽高接近父节点（> 95%），设为 Stretch/Stretch
- [ ] 角落固定：检测到节点靠近父节点某个角（距离 < 15%），设为对应角锚点（TopRight, BottomLeft 等）
- [ ] 边缘居中：检测到节点在父节点某条边的中心（水平或垂直居中），设为对应边居中（Top, Bottom, Left, Right）
- [ ] 完全居中：节点在父节点中心（水平和垂直都居中），设为 Middle/Center
- [ ] 水平/垂直拉伸：检测到节点在某个方向接近拉满（> 90%），另一方向固定，设为对应拉伸模式
- [ ] 保留边距：拉伸模式下，正确设置 `offsetMin` 和 `offsetMax` 保持 PSD 中的边距

### Phase 4：Layout Group 自动挂载（必须）
- [ ] 根据 JSON 中的 Layout 标记，正确挂载 VerticalLayoutGroup/HorizontalLayoutGroup/GridLayoutGroup
- [ ] 设置正确的间距（spacing）和对齐方式（childAlignment）
- [ ] 模板折叠：删除重复的子节点，只保留第一个作为模板
- [ ] 模板节点保持激活状态，便于程序员查看和测试

### Phase 5：C# View 代码生成（必须）
- [ ] 扫描 Prefab 中的所有交互组件（Button, Toggle, Slider, InputField, ScrollRect）
- [ ] 生成字段声明，命名规则：`private <ComponentType> <小驼峰名称>;`（如 `private Button btnClose;`）
- [ ] 生成部分方法声明（`partial void On<名称><事件>();`），程序员在另一文件中实现
- [ ] 生成 Start 方法，包含所有事件绑定代码（`btnClose.onClick.AddListener(OnCloseClicked);`）
- [ ] 生成的代码使用部分类（`partial class`），字段和绑定在 `.Generated.cs`，业务逻辑在程序员文件中
- [ ] 代码符合 C# 命名规范和项目代码风格（需检查项目是否有 .editorconfig）

### Phase 6：修正机制（必须）
- [ ] JSON 配置文件手动修正后，重新生成 Prefab 时应用修正结果
- [ ] 在 Unity 中手动调整 GameObject 后，挂载 `PSD2UILock` 组件标记为"锁定"
- [ ] 锁定的 GameObject 在重新生成时保留手动调整（位置、锚点、组件属性等）
- [ ] 生成的 `.Generated.cs` 文件如果已存在则覆盖更新，程序员编写的业务逻辑文件（如 `ShopWindowView.cs`）不受影响
- [ ] 提供"强制覆盖"选项，忽略所有锁定，完全重新生成

### Phase 7：增量更新（必须）
- [ ] 对比新旧 JSON 配置，识别新增、删除、修改的节点
- [ ] 只更新变化的部分，减少不必要的 Prefab 修改
- [ ] 图片资产变化（如按钮样式更新）时，只重新导入和应用该图片，不影响 Prefab 结构
- [ ] 更新日志记录：生成报告显示本次更新了哪些内容、保留了哪些锁定

## Decisions

| # | 决策点 | 选择 | 理由 | 影响范围 |
|---|--------|------|------|----------|
| 1 | Editor 插件位置 | `UnityProject/Assets/Change/Editor/` | 与项目现有 Change 框架集成，便于共享依赖 | 项目结构、可维护性 |
| 2 | 文本组件选择 | 优先 TextMeshProUGUI，回退到 Text | TMP 性能更好，但需检查项目集成情况 | 性能、兼容性 |
| 3 | 锚点推导策略 | 基于位置和大小的启发式规则 | 不依赖 AI，准确率高，可调试 | 准确率、可维护性 |
| 4 | 代码生成模式 | 部分类（partial class） | 便于程序员在另一文件中扩展，生成部分不被覆盖 | 代码组织、增量更新 |
| 5 | 锁定机制 | 自定义组件 `PSD2UILock` | Unity 原生机制，序列化到 Prefab，版本控制友好 | 易用性、可靠性 |
| 6 | 增量更新策略 | JSON 配置版本对比 + 锁定标记 | 精确控制更新范围，避免误覆盖 | 用户体验、健壮性 |
| 7 | 命名规范 | 字段名沿用 PSD 图层名（去除特殊字符，转小驼峰） | 保持命名一致性，便于追溯 | 代码可读性 |
| 8 | Layout 模板折叠 | 保留第一个子节点，删除其余 | 减少 Prefab 复杂度，运行时由程序动态生成 | Prefab 大小、性能 |
| 9 | 九宫格应用时机 | 图片导入时通过 AssetPostprocessor | 自动化流程，无需手动干预 | 易用性、自动化 |
| 10 | 错误处理 | 警告而非中断，记录到日志，标记问题节点 | 部分失败不影响整体，程序员可选择性修复 | 健壮性、用户体验 |

## 未决问题

| # | 问题 | 建议解决阶段 |
|---|------|-------------|
| 1 | 项目是否已集成 TextMeshPro，还是使用原生 Text 组件 | research - 需要检查项目当前 UI 组件使用情况 |
| 2 | 项目是否有统一的代码风格配置（.editorconfig, StyleCop 等） | research - 需要检查项目代码规范 |
| 3 | 锚点推导的阈值参数（如"靠近"定义为距离 < 15%）是否需要可配置 | design - 可能需要提供配置文件或 Inspector 面板调整 |
| 4 | 生成的 View 代码是否需要支持多种架构模板（MVC, MVP, MVVM） | explore - 评估不同架构的需求和实现成本 |
| 5 | Prefab 生成路径和命名规则是否需要可配置 | design - 可能需要项目特定的资产组织规则 |
| 6 | Canvas 和 EventSystem 是否需要工具自动创建 | design - 需要确认项目现有场景结构 |

## 约束与假设

**技术约束：**
- Unity 版本 ≥ 2021.3 LTS
- 依赖 FRD #1 的 JSON 配置文件和图片资产
- 项目使用 uGUI（非 UI Toolkit）
- C# 版本 ≥ 8.0（支持可空引用类型）

**开发约束：**
- 插件代码放在 `UnityProject/Assets/Change/Editor/` 下
- 需要创建对应的 `.asmdef` 文件（`Change.Editor.PSD2UI.asmdef`）

**假设：**
- 程序员理解 Unity 编辑器基本操作
- 项目已有基础的 UI 框架（Canvas, EventSystem）
- 生成的 Prefab 会被手动调整和扩展
- 程序员会在生成的代码基础上添加业务逻辑

## 技术选型摘要

**核心依赖：**
- Unity Editor API（AssetDatabase, PrefabUtility, TextureImporter 等）
- Newtonsoft.Json 或 System.Text.Json - JSON 解析
- UnityEngine.UI - uGUI 组件
- TextMeshPro（可选）- 高级文本渲染

**代码生成：**
- C# Roslyn（可选）- 代码语法树生成和格式化
- 或字符串模板 + StringBuilder

## 输出物规范

### Prefab 结构示例

```
ShopWindow (Canvas)
├── Background (Image, Sliced) [锚点: Stretch/Stretch]
├── CloseButton (Button) [锚点: TopRight]
│   ├── Icon (Image)
│   └── Label (TextMeshProUGUI)
├── TitleBar (Image, Sliced) [锚点: HorizontalStretch/Top]
│   └── Title (TextMeshProUGUI)
├── Content (RectTransform) [锚点: Stretch/Stretch, 有边距]
│   └── ItemList (ScrollRect + VerticalLayoutGroup)
│       ├── Viewport (RectMask2D)
│       │   └── Content (RectTransform)
│       │       └── ItemTemplate (GameObject) [模板节点]
│       └── Scrollbar (Scrollbar)
└── ButtonGroup (HorizontalLayoutGroup) [锚点: Bottom/Center]
    ├── ConfirmButton (Button)
    └── CancelButton (Button)
```

### C# View 代码示例

```csharp
// ShopWindowView.Generated.cs (工具自动生成，不要手动编辑)
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Change.Runtime.UI
{
    public partial class ShopWindowView : MonoBehaviour
    {
        [SerializeField] private Button btnClose;
        [SerializeField] private Button btnConfirm;
        [SerializeField] private Button btnCancel;
        [SerializeField] private TextMeshProUGUI txtTitle;
        [SerializeField] private ScrollRect scrollItemList;

        private void Start()
        {
            btnClose.onClick.AddListener(OnCloseClicked);
            btnConfirm.onClick.AddListener(OnConfirmClicked);
            btnCancel.onClick.AddListener(OnCancelClicked);
        }

        partial void OnCloseClicked();
        partial void OnConfirmClicked();
        partial void OnCancelClicked();
    }
}
```

```csharp
// ShopWindowView.cs (程序员手动创建，实现业务逻辑)
namespace Change.Runtime.UI
{
    public partial class ShopWindowView
    {
        private ShopData _shopData;

        public void Initialize(ShopData data)
        {
            _shopData = data;
            txtTitle.text = data.ShopName;
            // ... 其他初始化逻辑
        }

        partial void OnCloseClicked()
        {
            // 实现关闭逻辑
            gameObject.SetActive(false);
        }

        partial void OnConfirmClicked()
        {
            // 实现确认逻辑
            _shopData.Purchase();
        }

        partial void OnCancelClicked()
        {
            // 实现取消逻辑
            gameObject.SetActive(false);
        }
    }
}
```

### PSD2UILock 组件示例

```csharp
// PSD2UILock.cs
using UnityEngine;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// 标记此 GameObject 在 PSD2UI 重新生成时保持锁定，不被覆盖
    /// </summary>
    public class PSD2UILock : MonoBehaviour
    {
        [SerializeField] private bool lockTransform = true;
        [SerializeField] private bool lockComponents = true;
        [SerializeField] private bool lockChildren = false;
        
        [TextArea(3, 5)]
        [SerializeField] private string notes = "锁定原因：手动调整了锚点以适配特殊屏幕";
    }
}
```

详细实现在 design 阶段完善。
