# Unity Prefab 生成与代码框架 架构设计

> 日期: 2026-06-24 | 状态: 草稿
> FRD: [2026-06-24-psd-2-ui-generator-frd.md](../discover/2026-06-24-psd-2-ui-generator-frd.md)
> 上游: [2026-06-24-psd-2-ui-codegen-solution.md](../solutions/2026-06-24-psd-2-ui-codegen-solution.md)

## 上游产出引用

### 来自 FRD

| 引用内容 | 如何使用 |
|---------|----------|
| 决策 #2: 文本组件选择（优先 TextMeshProUGUI） | 切片 C 的 ComponentFactory 检测项目集成情况并选择组件 |
| 决策 #3: 锚点推导策略（启发式规则） | 切片 C 的 AnchorEngine 实现 6 种场景覆盖 |
| 决策 #4: 代码生成模式（partial class） | 切片 D 生成 `.Generated.cs` 和程序员编写文件分离 |
| 决策 #5: 锁定机制（自定义组件） | 切片 E 实现 `PSD2UILock` 组件 |
| 决策 #6: 增量更新策略（JSON 版本对比 + 锁定标记） | 切片 E 的 ChangeDetector 和 IncrementalUpdater |
| 验收条件 Phase 1-7 | 各切片验收标准与 FRD 对齐 |

### 来自 Solutions

| 引用内容 | 如何使用 |
|---------|----------|
| 最终选定方案: 方案 A（字符串模板 + StringBuilder） | 切片 D 使用 ViewCodeTemplate 实现字符串拼接，手动控制格式 |
| 风险: 格式不一致、语法错误延迟发现 | 切片 D 提供格式检查和示例代码验证 |
| 调整说明: 手动控制缩进和换行 | ViewCodeTemplate 使用 4 空格缩进标准 |

## 架构决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 整体架构模式 | 分层架构（4 层） | 职责清晰，与 FRD 的 7 个 Phase 自然对齐，便于增量更新实现 |
| 配置格式 | JSON（依赖 FRD #1 产出） | 上游依赖，无需重新设计 |
| 代码生成方式 | 字符串模板 + StringBuilder | 用户选定，零依赖，实现简单 |
| 锚点推导方式 | 启发式规则（位置和大小） | FRD 决策 #3，不依赖 AI，准确率高 |
| 测试策略 | 单元测试 + 集成测试 | 每个切片配单元测试，切片 E 提供端到端集成测试 |

## 文件地图

| 文件 | 所属切片 | 职责 |
|------|----------|------|
| `ConfigReader.cs` | 切片 A | 读取 JSON 配置文件 |
| `UINodeData.cs` | 切片 A | UI 节点数据模型定义 |
| `ConfigReaderTests.cs` | 切片 A | 配置读取单元测试 |
| `AssetImporter.cs` | 切片 B | 图片导入和九宫格应用 |
| `AssetImportPostprocessor.cs` | 切片 B | 自动化导入流程钩子 |
| `AssetImporterTests.cs` | 切片 B | 图片导入测试 |
| `PrefabBuilder.cs` | 切片 C | Prefab 组装主逻辑 |
| `AnchorEngine.cs` | 切片 C | 锚点自动推导引擎 |
| `ComponentFactory.cs` | 切片 C | 组件创建工厂 |
| `LayoutManager.cs` | 切片 C | Layout Group 挂载和模板折叠 |
| `PrefabBuilderTests.cs` | 切片 C | Prefab 组装测试 |
| `ViewCodeGenerator.cs` | 切片 D | 代码生成主逻辑 |
| `ViewCodeTemplate.cs` | 切片 D | 字符串模板和占位符替换 |
| `NamingUtility.cs` | 切片 D | 命名规范转换 |
| `ViewCodeGeneratorTests.cs` | 切片 D | 代码生成测试 |
| `PSD2UILock.cs` | 切片 E | 锁定标记组件 |
| `ChangeDetector.cs` | 切片 E | JSON 配置版本对比 |
| `IncrementalUpdater.cs` | 切片 E | 增量更新协调器 |
| `UnityMenuCommands.cs` | 切片 E | Unity 菜单命令入口 |

**插件代码位置：** `UnityProject/Assets/Change/Editor/PSD2UI/`

**Assembly Definition：** `Change.Editor.PSD2UI.asmdef`
- 依赖：`UnityEngine.UI`, `Unity.TextMeshPro`（可选）
- 平台：Editor Only

**生成资产输出路径：**
- **UI 图片资源：** `UnityProject/Assets/GameRes/UIPanelArt/`
- **Prefab 文件：** `UnityProject/Assets/GameRes/UIPanel/`
- **C# View 代码：** `UnityProject/Assets/HotUpdate/GameLogic/<ModuleName>/Views/Generated/`
  - 例如：`UnityProject/Assets/HotUpdate/GameLogic/Mail/Views/Generated/MailWindowView.Generated.cs`

## 切片分解

### 切片 A: 配置加载与数据模型

**依赖：** 无  
**风险等级：** 低  
**涉及文件：** `ConfigReader.cs`, `UINodeData.cs`, `ConfigReaderTests.cs`

**内容：** 读取 FRD #1 生成的 JSON 配置文件，解析为内部数据模型 `UINodeData` 树结构，为后续切片提供统一的数据访问接口。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `ConfigReader.LoadFromJson` | `UINodeData LoadFromJson(string jsonPath)` | 读取 JSON 文件并反序列化为 UINodeData 树 |
| `ConfigReader.ValidateConfig` | `bool ValidateConfig(string jsonPath, out List<string> errors)` | 验证 JSON 格式和必填字段 |

**数据契约：**

```csharp
public class UINodeData
{
    public string Name { get; set; }              // 节点名称（来自 PSD 图层名）
    public string Type { get; set; }              // 组件类型: "Image", "Text", "Button", etc.
    public RectData Rect { get; set; }            // 矩形数据（位置和大小）
    public string SpritePath { get; set; }        // 图片资产路径（可选）
    public SliceData Slice { get; set; }          // 九宫格数据（可选）
    public LayoutData Layout { get; set; }        // Layout 配置（可选）
    public List<UINodeData> Children { get; set; }// 子节点列表
}

public class RectData
{
    public float X, Y, Width, Height;
}

public class SliceData
{
    public int Left, Top, Right, Bottom;
}

public class LayoutData
{
    public string Type;       // "Vertical", "Horizontal", "Grid"
    public float Spacing;
    public string Alignment;
}
```

**验收标准：**
- [ ] 能正确解析 FRD 中的 `ShopWindow` 示例 JSON
- [ ] 缺少必填字段（Name, Type, Rect）时抛出 `InvalidDataException` 并提示具体字段名
- [ ] Children 为空时返回空列表而非 null
- [ ] 支持嵌套层级深度至少 10 层

**回归风险评估：**
- **影响范围：** 无（新增功能）
- **缓解措施：** 无需缓解

---

### 切片 B: 图片导入与九宫格

**依赖：** 切片 A（需要配置中的图片路径和九宫格数据）  
**风险等级：** 高（AssetDatabase 操作）  
**涉及文件：** `AssetImporter.cs`, `AssetImportPostprocessor.cs`, `AssetImporterTests.cs`

**内容：** 根据 JSON 配置中的图片路径和九宫格数据，自动导入图片资产到 Unity 项目，并通过 `TextureImporter.spriteBorder` 应用九宫格设置。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `AssetImporter.ImportSprites` | `void ImportSprites(List<UINodeData> nodes)` | 遍历节点收集图片路径并导入 |
| `AssetImporter.ApplySliceSettings` | `void ApplySliceSettings(string spritePath, SliceData slice)` | 应用九宫格边界设置 |

**行为契约：**
- **输入：** `UINodeData` 列表（包含 SpritePath 和 Slice）
- **输出：** Unity 项目中的 Sprite 资产，九宫格边界已设置
- **副作用：** 
  - 图片导入到 `Assets/GameRes/UIPanelArt/` 目录
  - `TextureImporter.spriteBorder` 被修改
  - 触发 AssetDatabase 刷新

**验收标准：**
- [ ] 图片正确导入到 `Assets/GameRes/UIPanelArt/` 目录
- [ ] 在 Sprite Editor 中可见九宫格边界线（对应 JSON 中的 Left/Top/Right/Bottom）
- [ ] 不存在的图片路径记录警告日志，不中断流程
- [ ] 重复导入同一图片不会覆盖手动调整的 Import Settings（通过文件哈希对比）

**回归风险评估：**
- **影响范围：** 项目现有图片导入设置可能被覆盖
- **缓解措施：** 
  - 只处理 JSON 配置中指定的图片路径
  - 使用独立的 `UIPanelArt/` 目录，避免与现有资产冲突
  - 在 AssetPostprocessor 中检查路径是否为 `UIPanelArt/`

---

### 切片 C: Prefab 组装与锚点推导

**依赖：** 切片 A（数据模型）、切片 B（图片资产）  
**风险等级：** 高（锚点推导算法准确性）  
**涉及文件：** `PrefabBuilder.cs`, `AnchorEngine.cs`, `ComponentFactory.cs`, `LayoutManager.cs`, `PrefabBuilderTests.cs`

**内容：** 根据 `UINodeData` 树结构组装 Unity Prefab，自动推导锚点设置（覆盖 6 种移动端场景），挂载 Layout 组件并折叠模板。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `PrefabBuilder.BuildPrefab` | `GameObject BuildPrefab(UINodeData root, string savePath)` | 构建 Prefab 并保存 |
| `AnchorEngine.InferAnchor` | `AnchorPreset InferAnchor(RectData childRect, RectData parentRect)` | 推导锚点类型 |
| `AnchorEngine.ApplyAnchor` | `void ApplyAnchor(RectTransform rt, AnchorPreset preset, RectData rect)` | 应用锚点设置 |
| `ComponentFactory.CreateComponent` | `Component CreateComponent(string type, GameObject target)` | 根据类型创建组件 |
| `LayoutManager.AttachLayoutGroup` | `void AttachLayoutGroup(UINodeData node, GameObject target)` | 挂载 LayoutGroup |
| `LayoutManager.CollapseTemplate` | `void CollapseTemplate(Transform parent)` | 折叠重复子节点 |

**数据契约：**

```csharp
public enum AnchorPreset
{
    StretchAll,         // 全屏拉伸（背景）
    TopRight,           // 右上角固定（关闭按钮）
    BottomCenter,       // 底部居中（导航栏）
    HorizontalStretch,  // 水平拉伸（标题栏）
    VerticalStretch,    // 垂直拉伸（侧边栏）
    MiddleCenter        // 完全居中（弹窗）
}
```

**锚点推导规则：**

| 场景 | 检测条件 | 锚点设置 |
|------|---------|---------|
| 全屏拉伸 | 宽高都 > 95% 父节点 | anchorMin=(0,0), anchorMax=(1,1) |
| 右上角固定 | X > 85% 父宽 且 Y > 85% 父高 | anchorMin=(1,1), anchorMax=(1,1), pivot=(1,1) |
| 底部居中 | 水平居中（45%-55%）且 Y < 15% | anchorMin=(0.5,0), anchorMax=(0.5,0), pivot=(0.5,0) |
| 水平拉伸 | 宽 > 90% 父宽 且 高 < 30% 父高 | anchorMin=(0,1), anchorMax=(1,1) |
| 垂直拉伸 | 高 > 90% 父高 且 宽 < 30% 父宽 | anchorMin=(0,0), anchorMax=(0,1) |
| 完全居中 | 水平和垂直都居中（45%-55%） | anchorMin=(0.5,0.5), anchorMax=(0.5,0.5) |

**验收标准：**
- [ ] 生成的 Prefab 层级与 JSON 配置完全一致
- [ ] 全屏背景节点锚点为 StretchAll
- [ ] 右上角关闭按钮锚点为 TopRight
- [ ] 底部导航栏锚点为 BottomCenter
- [ ] 标题栏锚点为 HorizontalStretch
- [ ] LayoutGroup 挂载正确（Vertical/Horizontal/Grid）
- [ ] 模板节点只保留第一个子节点，其余删除

**回归风险评估：**
- **影响范围：** 如果 `UIPanel/` 目录中已有同名 Prefab，会被覆盖
- **缓解措施：** 
  - 生成前检查 Prefab 是否存在
  - 提示用户是否覆盖，提供"强制覆盖"选项
  - 备份旧 Prefab 到 `UIPanel/.backup/` 目录

---

### 切片 D: C# View 代码生成

**依赖：** 切片 C（需要扫描 Prefab 中的交互组件）  
**风险等级：** 中（字符串模板格式控制）  
**涉及文件：** `ViewCodeGenerator.cs`, `ViewCodeTemplate.cs`, `NamingUtility.cs`, `ViewCodeGeneratorTests.cs`

**内容：** 扫描生成的 Prefab，为所有交互组件生成 C# View 代码框架，使用 partial class 模式分离生成代码和业务逻辑。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `ViewCodeGenerator.GenerateViewCode` | `void GenerateViewCode(GameObject prefab, string moduleName, string className, string savePath)` | 生成 View 代码文件，根据模块名称确定输出路径 |
| `ViewCodeTemplate.GenerateClass` | `string GenerateClass(string className, List<ComponentInfo> components)` | 生成完整类代码字符串 |
| `NamingUtility.ToFieldName` | `string ToFieldName(string layerName, string componentType)` | 转换为字段名（小驼峰） |
| `NamingUtility.ToMethodName` | `string ToMethodName(string layerName, string eventType)` | 转换为方法名（大驼峰） |

**数据契约：**

```csharp
public class ComponentInfo
{
    public string Name;         // 组件名称（来自 GameObject.name）
    public string Type;         // 组件类型: "Button", "Toggle", "Slider", "InputField", "ScrollRect"
    public string EventType;    // 事件类型: "onClick", "onValueChanged", "onEndEdit"
}
```

**行为契约：**
- **输入：** Prefab GameObject，模块名称（如 "Mail"），类名，保存路径
- **输出：** `.Generated.cs` 文件到 `Assets/HotUpdate/GameLogic/<ModuleName>/Views/Generated/` 目录
- **副作用：** 
  - 自动创建模块目录和 `Views/Generated/` 子目录（如果不存在）
  - 如果 `.Generated.cs` 已存在，覆盖更新
  - 不修改程序员编写的 `<ClassName>View.cs` 文件（位于 `Views/` 目录）

**字符串模板示例：**

```csharp
// {ClassName}View.Generated.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Change.Runtime.UI
{
    public partial class {ClassName}View : MonoBehaviour
    {
        // 字段声明
        [SerializeField] private Button {fieldName1};
        [SerializeField] private Toggle {fieldName2};
        
        private void Start()
        {
            // 事件绑定
            {fieldName1}.onClick.AddListener(On{MethodName1}Clicked);
            {fieldName2}.onValueChanged.AddListener(On{MethodName2}ValueChanged);
        }
        
        // 部分方法声明
        partial void On{MethodName1}Clicked();
        partial void On{MethodName2}ValueChanged(bool value);
    }
}
```

**命名规则：**
- 字段名：`btn` + PascalCase（Button）、`txt` + PascalCase（Text）、`tgl` + PascalCase（Toggle）
- 方法名：`On` + PascalCase + 事件类型
- 去除特殊字符：PSD 图层名中的空格、下划线、中文转为空或英文

**验收标准：**
- [ ] 代码正确生成到 `Assets/HotUpdate/GameLogic/<ModuleName>/Views/Generated/` 目录
- [ ] 自动创建不存在的模块目录结构
- [ ] 生成的代码包含所有交互组件的字段声明
- [ ] 部分方法命名符合 C# 规范（PascalCase）
- [ ] 事件绑定在 Start 方法中正确生成
- [ ] 代码缩进使用 4 空格，符合项目风格
- [ ] 使用 `SerializeField` 属性，字段为 private

**回归风险评估：**
- **影响范围：** 如果 `.Generated.cs` 已存在且被程序员手动修改，会被覆盖
- **缓解措施：** 
  - 在文件头添加警告注释："此文件自动生成，请勿手动编辑"
  - 提供"预览代码"功能，生成前展示将要覆盖的内容

---

### 切片 E: 增量更新与锁定机制

**依赖：** 切片 A-D（所有前置功能）  
**风险等级：** 中（变化检测和锁定保留逻辑）  
**涉及文件：** `PSD2UILock.cs`, `ChangeDetector.cs`, `IncrementalUpdater.cs`, `UnityMenuCommands.cs`

**内容：** 实现增量更新机制，支持手动锁定调整，重新生成时只更新变化部分，提供 Unity 菜单命令入口。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `ChangeDetector.CompareConfigs` | `ChangeReport CompareConfigs(UINodeData oldConfig, UINodeData newConfig)` | 对比新旧配置 |
| `IncrementalUpdater.Update` | `void Update(GameObject prefab, ChangeReport changes, bool respectLocks)` | 增量更新 Prefab |
| `UnityMenuCommands.GeneratePrefabFromConfig` | `void GeneratePrefabFromConfig()` | Unity 菜单命令入口 |

**数据契约：**

```csharp
public class PSD2UILock : MonoBehaviour
{
    public bool LockTransform = true;     // 锁定 Transform 属性
    public bool LockComponents = true;    // 锁定组件配置
    public bool LockChildren = false;     // 锁定子节点结构
    [TextArea(3, 5)]
    public string Notes;                  // 锁定原因备注
}

public class ChangeReport
{
    public List<UINodeData> Added;        // 新增节点
    public List<UINodeData> Removed;      // 删除节点
    public List<UINodeData> Modified;     // 修改节点
}
```

**行为契约：**
- **输入：** 新旧 JSON 配置，现有 Prefab，是否尊重锁定
- **输出：** 更新后的 Prefab（保留锁定调整）
- **副作用：** 
  - Prefab 资产被修改
  - 生成更新日志到 `Logs/PSD2UI_Update_<timestamp>.log`

**变化检测逻辑：**
- 按节点名称和路径匹配新旧节点
- 对比 Rect、Type、SpritePath、Layout 等属性
- 生成 ChangeReport（Added/Removed/Modified）

**增量更新逻辑：**
1. 遍历 `changes.Removed`，删除对应 GameObject（除非锁定）
2. 遍历 `changes.Modified`，更新属性（除非锁定）
3. 遍历 `changes.Added`，创建新 GameObject
4. 生成更新日志，记录本次更新和保留的内容

**验收标准：**
- [ ] 手动调整锚点后挂载 `PSD2UILock`，重新生成时保留调整
- [ ] JSON 中删除节点后，Prefab 中对应节点被删除（除非锁定）
- [ ] JSON 中新增节点后，Prefab 中正确添加新节点
- [ ] 更新日志清晰显示保留和更新的内容
- [ ] "强制覆盖"选项忽略所有锁定，完全重新生成
- [ ] Unity 菜单命令可正常调用：`Assets/PSD2UGUI/Generate Prefab from Config`

**回归风险评估：**
- **影响范围：** 程序员手动添加的非锁定调整可能被覆盖
- **缓解措施：** 
  - 生成前提示用户查看更新日志
  - 提供"预览变化"对话框，显示将要更新和保留的内容
  - 在 Scene 视图中高亮显示将要修改的 GameObject

---

## 切片依赖图

```
切片 A: 配置加载
  ├── 切片 B: 图片导入（高风险）
  │     └── 切片 C: Prefab 组装（高风险）
  │           └── 切片 D: 代码生成
  └── 切片 E: 增量更新（依赖 A, B, C, D）
```

**实施顺序：** A → B → C → D → E

**并行机会：** 无（所有切片有明确的依赖链）

---

## 关键接口

### 切片 A 暴露给切片 B/C

```csharp
public class UINodeData
{
    public string Name { get; set; }
    public string Type { get; set; }
    public RectData Rect { get; set; }
    public string SpritePath { get; set; }
    public SliceData Slice { get; set; }
    public LayoutData Layout { get; set; }
    public List<UINodeData> Children { get; set; }
}
```

### 切片 B 暴露给切片 C

```csharp
// AssetImporter 确保图片资产已导入，切片 C 可直接引用 Sprite
public void ImportSprites(List<UINodeData> nodes);
```

### 切片 C 暴露给切片 D

```csharp
// PrefabBuilder 生成 Prefab，切片 D 扫描交互组件
public GameObject BuildPrefab(UINodeData root, string savePath);
```

### 切片 A-D 暴露给切片 E

```csharp
// 增量更新需要调用前置切片的所有接口
ConfigReader.LoadFromJson(oldJsonPath);  // 读取旧配置
AssetImporter.ImportSprites(newNodes);   // 导入新图片
PrefabBuilder.BuildPrefab(newRoot);      // 重建部分 Prefab
ViewCodeGenerator.GenerateViewCode();    // 重新生成代码
```

---

## 回归风险评估

| 影响范围 | 风险等级 | 缓解措施 |
|---------|---------|----------|
| 项目现有图片导入设置 | 中 | 只处理 PSD2UI 标记的图片，添加前缀避免冲突 |
| 同名 Prefab 被覆盖 | 高 | 生成前提示，提供备份，支持锁定机制 |
| `.Generated.cs` 被覆盖 | 低 | 设计为可覆盖文件，业务逻辑在独立文件中 |
| Unity Editor 性能 | 低 | AssetDatabase 操作使用批处理，避免频繁刷新 |
| 现有 UI 框架兼容性 | 低 | 生成的 Prefab 和代码使用标准 uGUI API，无特殊依赖 |

---

## 实施建议

1. **Phase 1：基础设施（切片 A）**
   - 实现 JSON 配置读取和数据模型
   - 编写单元测试验证数据解析
   - 预计 1 天

2. **Phase 2：资产管理（切片 B）**
   - 实现图片导入和九宫格应用
   - 测试 AssetDatabase 操作
   - 预计 1-2 天

3. **Phase 3：核心生成（切片 C）**
   - 实现 Prefab 组装和锚点推导
   - 重点测试 6 种锚点场景
   - 预计 2-3 天

4. **Phase 4：代码生成（切片 D）**
   - 实现字符串模板和代码生成
   - 验证生成代码的格式和正确性
   - 预计 1-2 天

5. **Phase 5：增量更新（切片 E）**
   - 实现变化检测和锁定机制
   - 集成所有前置功能
   - 端到端测试
   - 预计 2 天

**总预计工时：** 7-10 天

---

## 补充说明：模块化代码生成

**模块名称识别：**
- 从 JSON 配置文件路径或 Prefab 名称推断模块名称
- 例如：`MailWindow.json` → 模块名称 = "Mail"
- 支持用户在 Unity 菜单命令中手动指定模块名称

**目录结构示例：**
```
Assets/HotUpdate/GameLogic/
├── Mail/
│   └── Views/
│       ├── Generated/
│       │   └── MailWindowView.Generated.cs  (工具生成)
│       └── MailWindowView.cs                (程序员编写)
└── Task/
    └── Views/
        ├── Generated/
        │   └── TaskWindowView.Generated.cs  (工具生成)
        └── TaskWindowView.cs                (程序员编写)
```

**模块名称提取逻辑：**
1. 优先使用 Unity 菜单命令中用户手动输入的模块名称
2. 如果未指定，从 Prefab 名称提取（去除 "Window"/"Panel" 后缀）
3. 如果仍无法确定，使用默认值 "Common"
