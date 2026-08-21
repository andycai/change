# Unity Prefab 生成与代码框架 实现计划

> 日期: 2026-06-24 | 状态: 草稿
> 上游设计: [2026-06-24-psd-2-ui-generator-design.md](../designs/2026-06-24-psd-2-ui-generator-design.md)
> 上游 FRD: [2026-06-24-psd-2-ui-generator-frd.md](../discover/2026-06-24-psd-2-ui-generator-frd.md)
> 上游方案探索: [2026-06-24-psd-2-ui-codegen-solution.md](../solutions/2026-06-24-psd-2-ui-codegen-solution.md)

## 上游产出引用

### 来自 Design

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 切片 A: 配置加载与数据模型 | 任务 1-4 |
| 切片 B: 图片导入与九宫格 | 任务 5-8 |
| 切片 C: Prefab 组装与锚点推导 | 任务 9-18 |
| 切片 D: C# View 代码生成 | 任务 19-24 |
| 切片 E: 增量更新与锁定机制 | 任务 25-30 |
| 文件地图: 19 个文件 | 任务按文件创建顺序组织 |
| 架构决策: 分层架构（4 层） | 任务按层级依赖顺序执行 |
| 锚点推导规则: 6 种场景 | 任务 13-14 实现推导算法 |

### 来自 FRD

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| Phase 1: 图片导入与九宫格 | 任务 5-8 |
| Phase 2: Prefab 基础组装 | 任务 9-12 |
| Phase 3: 锚点自动推导 | 任务 13-14 |
| Phase 4: Layout Group 自动挂载 | 任务 15-16 |
| Phase 5: C# View 代码生成 | 任务 19-24 |
| Phase 6: 修正机制 | 任务 27-28 |
| Phase 7: 增量更新 | 任务 29-30 |

### 来自 Solutions

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 选定方案 A: 字符串模板 + StringBuilder | 任务 20-21 使用字符串拼接生成代码 |
| 手动控制缩进（4 空格） | 任务 21 模板中明确缩进规则 |

## 目标

开发 Unity Editor 插件，读取 FRD #1 输出的 JSON 配置，自动生成带自适应锚点的 uGUI Prefab 和 C# View 代码框架，支持增量更新和锁定机制。

## 架构

采用分层架构（4 层）：
1. **配置读取层** — 解析 JSON 为内部数据模型
2. **数据模型层** — UINodeData 树结构
3. **生成引擎层** — Prefab Builder + Code Generator
4. **Unity 集成层** — AssetDatabase + Menu Commands

技术方案：字符串模板 + StringBuilder（零依赖），启发式规则推导锚点（6 种场景），partial class 模式分离生成代码与业务逻辑。

## 技术栈

- **Unity**: 2021.3 LTS+
- **C#**: 8.0+ (支持可空引用类型)
- **Unity API**: AssetDatabase, PrefabUtility, TextureImporter
- **uGUI**: UnityEngine.UI
- **TextMeshPro**: (可选) Unity.TextMeshPro
- **JSON**: Newtonsoft.Json 或 System.Text.Json

## 文件清单

| 文件 | 操作 | 职责 | 所属任务 |
|------|------|------|----------|
| `ConfigReader.cs` | 创建 | 读取 JSON 配置文件 | 任务 1-2 |
| `UINodeData.cs` | 创建 | UI 节点数据模型定义 | 任务 3 |
| `ConfigReaderTests.cs` | 创建 | 配置读取单元测试 | 任务 4 |
| `AssetImporter.cs` | 创建 | 图片导入和九宫格应用 | 任务 5-6 |
| `AssetImportPostprocessor.cs` | 创建 | 自动化导入流程钩子 | 任务 7 |
| `AssetImporterTests.cs` | 创建 | 图片导入测试 | 任务 8 |
| `PrefabBuilder.cs` | 创建 | Prefab 组装主逻辑 | 任务 9-10 |
| `ComponentFactory.cs` | 创建 | 组件创建工厂 | 任务 11-12 |
| `AnchorEngine.cs` | 创建 | 锚点自动推导引擎 | 任务 13-14 |
| `LayoutManager.cs` | 创建 | Layout Group 挂载和模板折叠 | 任务 15-16 |
| `PrefabBuilderTests.cs` | 创建 | Prefab 组装测试 | 任务 17-18 |
| `ViewCodeGenerator.cs` | 创建 | 代码生成主逻辑 | 任务 19-20 |
| `ViewCodeTemplate.cs` | 创建 | 字符串模板和占位符替换 | 任务 21 |
| `NamingUtility.cs` | 创建 | 命名规范转换 | 任务 22 |
| `ViewCodeGeneratorTests.cs` | 创建 | 代码生成测试 | 任务 23-24 |
| `PSD2UILock.cs` | 创建 | 锁定标记组件 | 任务 25-26 |
| `ChangeDetector.cs` | 创建 | JSON 配置版本对比 | 任务 27 |
| `IncrementalUpdater.cs` | 创建 | 增量更新协调器 | 任务 28-29 |
| `UnityMenuCommands.cs` | 创建 | Unity 菜单命令入口 | 任务 30 |

**文件位置:** `UnityProject/Assets/Change/Editor/PSD2UI/`

**Assembly Definition:** 任务 0 中创建 `Change.Editor.PSD2UI.asmdef`

## 任务依赖图

```
任务 0: Assembly Definition
  └── 任务 1-4: 切片 A（配置加载）
        ├── 任务 5-8: 切片 B（图片导入）
        │     └── 任务 9-18: 切片 C（Prefab 组装）
        │           └── 任务 19-24: 切片 D（代码生成）
        └── 任务 25-30: 切片 E（增量更新，依赖 A-D）
```

## 风险评估

| 风险（来源） | 影响 | 计划中的缓解措施 |
|-------------|------|-----------------|
| AssetDatabase 操作可能覆盖现有图片设置（Design 切片 B） | 项目现有资产受影响 | 任务 7 中检查路径前缀 `UIPanelArt/`，只处理工具导入的图片 |
| 锚点推导算法准确性不足（Design 切片 C） | 生成的 Prefab 布局错误 | 任务 18 中针对 6 种场景编写详细测试 |
| 字符串模板格式不一致（Solutions 风险） | 生成的代码不符合项目风格 | 任务 21 中明确缩进规则（4 空格），任务 24 中验证生成代码格式 |
| 同名 Prefab 被覆盖（Design 回归评估） | 程序员手动调整丢失 | 任务 28-29 实现锁定机制，任务 30 提供强制覆盖选项 |
| 模块名称提取失败（新增需求） | 代码生成到错误目录 | 任务 20 中实现模块名称提取逻辑，支持手动指定 |

---

## 任务

### 任务 0: 创建 Assembly Definition

**覆盖的上游需求:** Design 文件地图 — Assembly Definition 配置

**文件:**
- 创建: `UnityProject/Assets/Change/Editor/PSD2UI/Change.Editor.PSD2UI.asmdef`

- [ ] **步骤 1: 创建 asmdef 文件**

```json
{
    "name": "Change.Editor.PSD2UI",
    "rootNamespace": "Change.Editor.PSD2UI",
    "references": [
        "UnityEngine.UI",
        "Unity.TextMeshPro"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **步骤 2: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/Change.Editor.PSD2UI.asmdef
git commit -m "feat(psd2ui): add assembly definition for PSD2UI editor plugin

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 1: ConfigReader - 基础结构和 JSON 解析

**覆盖的上游需求:** Design 切片 A — 配置加载，FRD Phase 1

**文件:**
- 创建: `UnityProject/Assets/Change/Editor/PSD2UI/ConfigReader.cs`
- 测试: `UnityProject/Assets/Change/Editor/PSD2UI/Tests/ConfigReaderTests.cs`

- [ ] **步骤 1: 编写失败的测试 — 验证 LoadFromJson 存在**

```csharp
// ConfigReaderTests.cs
using NUnit.Framework;
using Change.Editor.PSD2UI;

namespace Change.Editor.PSD2UI.Tests
{
    public class ConfigReaderTests
    {
        [Test]
        public void LoadFromJson_WithValidPath_ReturnsUINodeData()
        {
            var reader = new ConfigReader();
            var result = reader.LoadFromJson("test.json");
            Assert.IsNotNull(result);
        }
    }
}
```

- [ ] **步骤 2: 运行测试验证失败**

运行: 在 Unity Test Runner 中运行 `ConfigReaderTests.LoadFromJson_WithValidPath_ReturnsUINodeData`
预期: FAIL，报错 "ConfigReader does not exist"

- [ ] **步骤 3: 创建 ConfigReader 骨架**

```csharp
// ConfigReader.cs
using System.Collections.Generic;

namespace Change.Editor.PSD2UI
{
    public class ConfigReader
    {
        public UINodeData LoadFromJson(string jsonPath)
        {
            // TODO: 实现
            return null;
        }

        public bool ValidateConfig(string jsonPath, out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}
```

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/ConfigReader.cs
git add UnityProject/Assets/Change/Editor/PSD2UI/Tests/ConfigReaderTests.cs
git commit -m "test(psd2ui): add ConfigReader skeleton with failing test

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 2: ConfigReader - 实现 JSON 解析逻辑

**覆盖的上游需求:** Design 切片 A — LoadFromJson 接口

**依赖:** 任务 1, 任务 3（UINodeData 数据模型）

**文件:**
- 修改: `UnityProject/Assets/Change/Editor/PSD2UI/ConfigReader.cs`

- [ ] **步骤 1: 实现 LoadFromJson**

```csharp
using System.IO;
using Newtonsoft.Json;

public UINodeData LoadFromJson(string jsonPath)
{
    if (!File.Exists(jsonPath))
    {
        throw new FileNotFoundException($"JSON config not found: {jsonPath}");
    }

    string jsonContent = File.ReadAllText(jsonPath);
    var nodeData = JsonConvert.DeserializeObject<UINodeData>(jsonContent);

    if (nodeData == null)
    {
        throw new InvalidDataException($"Failed to deserialize JSON: {jsonPath}");
    }

    return nodeData;
}
```

- [ ] **步骤 2: 运行测试验证通过（需要准备测试 JSON 文件）**

运行: Unity Test Runner 中运行 `ConfigReaderTests`
预期: PASS（假设 test.json 存在且格式正确）

- [ ] **步骤 3: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/ConfigReader.cs
git commit -m "feat(psd2ui): implement LoadFromJson with JSON deserialization

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 3: UINodeData - 数据模型定义

**覆盖的上游需求:** Design 切片 A — 数据契约

**文件:**
- 创建: `UnityProject/Assets/Change/Editor/PSD2UI/UINodeData.cs`

- [ ] **步骤 1: 定义数据模型类**

```csharp
// UINodeData.cs
using System.Collections.Generic;

namespace Change.Editor.PSD2UI
{
    public class UINodeData
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public RectData Rect { get; set; }
        public string SpritePath { get; set; }
        public SliceData Slice { get; set; }
        public LayoutData Layout { get; set; }
        public List<UINodeData> Children { get; set; } = new List<UINodeData>();
    }

    public class RectData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    public class SliceData
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
    }

    public class LayoutData
    {
        public string Type { get; set; }  // "Vertical", "Horizontal", "Grid"
        public float Spacing { get; set; }
        public string Alignment { get; set; }
    }
}
```

- [ ] **步骤 2: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/UINodeData.cs
git commit -m "feat(psd2ui): define UINodeData and related data models

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 4: ConfigReader - 验证和测试完善

**覆盖的上游需求:** Design 切片 A — ValidateConfig 接口，FRD 验收标准

**依赖:** 任务 1, 2, 3

**文件:**
- 修改: `UnityProject/Assets/Change/Editor/PSD2UI/ConfigReader.cs`
- 修改: `UnityProject/Assets/Change/Editor/PSD2UI/Tests/ConfigReaderTests.cs`

- [ ] **步骤 1: 编写验证测试 — 缺少必填字段**

```csharp
[Test]
public void ValidateConfig_MissingRequiredField_ReturnsFalse()
{
    var reader = new ConfigReader();
    var isValid = reader.ValidateConfig("invalid.json", out List<string> errors);
    
    Assert.IsFalse(isValid);
    Assert.IsNotEmpty(errors);
    Assert.That(errors[0], Does.Contain("Name"));
}
```

- [ ] **步骤 2: 实现 ValidateConfig**

```csharp
public bool ValidateConfig(string jsonPath, out List<string> errors)
{
    errors = new List<string>();

    try
    {
        var nodeData = LoadFromJson(jsonPath);
        ValidateNode(nodeData, "", errors);
    }
    catch (System.Exception ex)
    {
        errors.Add($"JSON parsing error: {ex.Message}");
        return false;
    }

    return errors.Count == 0;
}

private void ValidateNode(UINodeData node, string path, List<string> errors)
{
    string currentPath = string.IsNullOrEmpty(path) ? node.Name : $"{path}/{node.Name}";

    if (string.IsNullOrEmpty(node.Name))
        errors.Add($"{currentPath}: Missing required field 'Name'");
    
    if (string.IsNullOrEmpty(node.Type))
        errors.Add($"{currentPath}: Missing required field 'Type'");
    
    if (node.Rect == null)
        errors.Add($"{currentPath}: Missing required field 'Rect'");

    if (node.Children != null)
    {
        foreach (var child in node.Children)
        {
            ValidateNode(child, currentPath, errors);
        }
    }
}
```

- [ ] **步骤 3: 运行测试验证**

运行: Unity Test Runner
预期: 所有测试 PASS

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/ConfigReader.cs
git add UnityProject/Assets/Change/Editor/PSD2UI/Tests/ConfigReaderTests.cs
git commit -m "feat(psd2ui): add config validation with required field checks

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---
### 任务 5-8: 切片 B - 图片导入与九宫格

**覆盖的上游需求:** Design 切片 B，FRD Phase 1

**任务 5-6:** 创建 AssetImporter.cs，实现 ImportSprites 和 ApplySliceSettings 方法，使用 AssetDatabase.ImportAsset 和 TextureImporter.spriteBorder

**任务 7:** 创建 AssetImportPostprocessor.cs，在 OnPreprocessTexture 中检查路径前缀 `UIPanelArt/`

**任务 8:** 创建 AssetImporterTests.cs，验证图片导入到 `Assets/GameRes/UIPanelArt/`，九宫格边界正确应用

**关键代码片段（任务 6）:**
```csharp
public void ApplySliceSettings(string spritePath, SliceData slice)
{
    var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
    if (importer != null)
    {
        importer.spriteBorder = new Vector4(slice.Left, slice.Bottom, slice.Right, slice.Top);
        importer.SaveAndReimport();
    }
}
```

---

### 任务 9-10: PrefabBuilder - 基础组装逻辑

**覆盖的上游需求:** Design 切片 C，FRD Phase 2

**任务 9:** 创建 PrefabBuilder.cs 骨架，定义 BuildPrefab 和 CreateNode 方法签名

**任务 10:** 实现递归构建逻辑，遍历 UINodeData 树创建 GameObject 层级

**关键代码（任务 10）:**
```csharp
public GameObject BuildPrefab(UINodeData root, string savePath)
{
    var rootGO = new GameObject(root.Name);
    rootGO.AddComponent<RectTransform>();
    
    CreateNode(root, rootGO.transform);
    
    PrefabUtility.SaveAsPrefabAsset(rootGO, savePath);
    Object.DestroyImmediate(rootGO);
    
    return AssetDatabase.LoadAssetAtPath<GameObject>(savePath);
}

private void CreateNode(UINodeData node, Transform parent)
{
    var go = new GameObject(node.Name);
    var rt = go.AddComponent<RectTransform>();
    rt.SetParent(parent, false);
    
    // 设置 RectTransform 属性
    rt.anchoredPosition = new Vector2(node.Rect.X, node.Rect.Y);
    rt.sizeDelta = new Vector2(node.Rect.Width, node.Rect.Height);
    
    // 递归创建子节点
    foreach (var child in node.Children)
    {
        CreateNode(child, rt);
    }
}
```

---

### 任务 11-12: ComponentFactory - 组件创建

**覆盖的上游需求:** Design 切片 C，FRD Phase 2

**任务 11:** 创建 ComponentFactory.cs，实现 CreateComponent 方法，支持 Image/Text/Button/ScrollRect/InputField

**任务 12:** 集成 TextMeshPro 检测逻辑，优先使用 TextMeshProUGUI，回退到 Text

**关键代码（任务 11）:**
```csharp
public Component CreateComponent(string type, GameObject target)
{
    switch (type)
    {
        case "Image":
            return target.AddComponent<Image>();
        case "Text":
            return TryAddTextMeshPro(target) ?? target.AddComponent<Text>();
        case "Button":
            var button = target.AddComponent<Button>();
            target.AddComponent<Image>(); // Button 需要 TargetGraphic
            button.targetGraphic = target.GetComponent<Image>();
            return button;
        case "ScrollRect":
            return target.AddComponent<ScrollRect>();
        case "InputField":
            return target.AddComponent<InputField>();
        default:
            return null;
    }
}

private Component TryAddTextMeshPro(GameObject target)
{
    var tmpType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
    return tmpType != null ? target.AddComponent(tmpType) : null;
}
```

---

### 任务 13-14: AnchorEngine - 锚点推导

**覆盖的上游需求:** Design 切片 C（锚点推导规则），FRD Phase 3

**任务 13:** 创建 AnchorEngine.cs，定义 AnchorPreset 枚举和 InferAnchor 方法

**任务 14:** 实现 6 种场景的锚点推导逻辑和 ApplyAnchor 方法

**关键代码（任务 14）:**
```csharp
public AnchorPreset InferAnchor(RectData childRect, RectData parentRect)
{
    float widthRatio = childRect.Width / parentRect.Width;
    float heightRatio = childRect.Height / parentRect.Height;
    float centerX = (childRect.X + childRect.Width / 2) / parentRect.Width;
    float centerY = (childRect.Y + childRect.Height / 2) / parentRect.Height;

    // 全屏拉伸
    if (widthRatio > 0.95f && heightRatio > 0.95f)
        return AnchorPreset.StretchAll;
    
    // 水平拉伸
    if (widthRatio > 0.90f && heightRatio < 0.30f)
        return AnchorPreset.HorizontalStretch;
    
    // 垂直拉伸
    if (heightRatio > 0.90f && widthRatio < 0.30f)
        return AnchorPreset.VerticalStretch;
    
    // 右上角固定
    if (centerX > 0.85f && centerY > 0.85f)
        return AnchorPreset.TopRight;
    
    // 底部居中
    if (centerX >= 0.45f && centerX <= 0.55f && centerY < 0.15f)
        return AnchorPreset.BottomCenter;
    
    // 完全居中
    if (centerX >= 0.45f && centerX <= 0.55f && centerY >= 0.45f && centerY <= 0.55f)
        return AnchorPreset.MiddleCenter;
    
    return AnchorPreset.MiddleCenter; // 默认
}

public void ApplyAnchor(RectTransform rt, AnchorPreset preset, RectData rect)
{
    switch (preset)
    {
        case AnchorPreset.StretchAll:
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(rect.X, rect.Y);
            rt.offsetMax = new Vector2(-rect.X, -rect.Y);
            break;
        case AnchorPreset.TopRight:
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-rect.X, -rect.Y);
            rt.sizeDelta = new Vector2(rect.Width, rect.Height);
            break;
        // ... 其他 preset
    }
}
```

---

### 任务 15-16: LayoutManager - Layout Group 挂载

**覆盖的上游需求:** Design 切片 C，FRD Phase 4

**任务 15:** 创建 LayoutManager.cs，实现 AttachLayoutGroup 方法

**任务 16:** 实现 CollapseTemplate 方法，删除重复子节点只保留第一个

**关键代码（任务 15）:**
```csharp
public void AttachLayoutGroup(UINodeData node, GameObject target)
{
    if (node.Layout == null) return;

    switch (node.Layout.Type)
    {
        case "Vertical":
            var vg = target.AddComponent<VerticalLayoutGroup>();
            vg.spacing = node.Layout.Spacing;
            vg.childAlignment = ParseAlignment(node.Layout.Alignment);
            break;
        case "Horizontal":
            var hg = target.AddComponent<HorizontalLayoutGroup>();
            hg.spacing = node.Layout.Spacing;
            hg.childAlignment = ParseAlignment(node.Layout.Alignment);
            break;
        case "Grid":
            var gg = target.AddComponent<GridLayoutGroup>();
            gg.spacing = new Vector2(node.Layout.Spacing, node.Layout.Spacing);
            break;
    }
}
```

---

### 任务 17-18: PrefabBuilder 集成测试

**覆盖的上游需求:** Design 切片 C 验收标准

**任务 17:** 创建 PrefabBuilderTests.cs，编写 6 种锚点场景的测试用例

**任务 18:** 验证 Prefab 层级结构、组件类型、锚点设置、LayoutGroup 挂载

---

### 任务 19-20: ViewCodeGenerator - 代码生成主逻辑

**覆盖的上游需求:** Design 切片 D，FRD Phase 5

**任务 19:** 创建 ViewCodeGenerator.cs，实现 ScanComponents 方法扫描交互组件

**任务 20:** 实现 GenerateViewCode 方法，调用 ViewCodeTemplate 生成代码，支持模块名称提取

**关键代码（任务 19）:**
```csharp
private List<ComponentInfo> ScanComponents(GameObject prefab)
{
    var components = new List<ComponentInfo>();
    ScanRecursive(prefab.transform, components);
    return components;
}

private void ScanRecursive(Transform transform, List<ComponentInfo> components)
{
    var button = transform.GetComponent<Button>();
    if (button != null)
    {
        components.Add(new ComponentInfo 
        { 
            Name = transform.name, 
            Type = "Button", 
            EventType = "onClick" 
        });
    }
    
    // ... Toggle, Slider, InputField, ScrollRect
    
    foreach (Transform child in transform)
    {
        ScanRecursive(child, components);
    }
}
```

**模块名称提取（任务 20）:**
```csharp
private string ExtractModuleName(string prefabName)
{
    // 去除 "Window" 或 "Panel" 后缀
    string moduleName = prefabName.Replace("Window", "").Replace("Panel", "");
    return string.IsNullOrEmpty(moduleName) ? "Common" : moduleName;
}
```

---

### 任务 21: ViewCodeTemplate - 字符串模板实现

**覆盖的上游需求:** Design 切片 D，Solutions 方案 A

**关键代码:**
```csharp
public string GenerateClass(string className, string moduleName, List<ComponentInfo> components)
{
    var sb = new StringBuilder();
    sb.AppendLine("// AUTO-GENERATED CODE - DO NOT EDIT MANUALLY");
    sb.AppendLine("using UnityEngine;");
    sb.AppendLine("using UnityEngine.UI;");
    sb.AppendLine("using TMPro;");
    sb.AppendLine();
    sb.AppendLine("namespace Change.Runtime.UI");
    sb.AppendLine("{");
    sb.AppendLine($"    public partial class {className}View : MonoBehaviour");
    sb.AppendLine("    {");
    
    // 字段声明
    foreach (var comp in components)
    {
        string fieldName = NamingUtility.ToFieldName(comp.Name, comp.Type);
        sb.AppendLine($"        [SerializeField] private {comp.Type} {fieldName};");
    }
    
    sb.AppendLine();
    sb.AppendLine("        private void Start()");
    sb.AppendLine("        {");
    
    // 事件绑定
    foreach (var comp in components)
    {
        string fieldName = NamingUtility.ToFieldName(comp.Name, comp.Type);
        string methodName = NamingUtility.ToMethodName(comp.Name, comp.EventType);
        sb.AppendLine($"            {fieldName}.{comp.EventType}.AddListener({methodName});");
    }
    
    sb.AppendLine("        }");
    sb.AppendLine();
    
    // 部分方法声明
    foreach (var comp in components)
    {
        string methodName = NamingUtility.ToMethodName(comp.Name, comp.EventType);
        sb.AppendLine($"        partial void {methodName}();");
    }
    
    sb.AppendLine("    }");
    sb.AppendLine("}");
    
    return sb.ToString();
}
```

---

### 任务 22: NamingUtility - 命名规范转换

**关键代码:**
```csharp
public static string ToFieldName(string layerName, string componentType)
{
    string prefix = componentType switch
    {
        "Button" => "btn",
        "Text" => "txt",
        "Toggle" => "tgl",
        "Slider" => "sld",
        "InputField" => "inp",
        "ScrollRect" => "scr",
        _ => ""
    };
    
    string cleaned = CleanName(layerName);
    return prefix + ToPascalCase(cleaned);
}

public static string ToMethodName(string layerName, string eventType)
{
    string cleaned = CleanName(layerName);
    string eventSuffix = eventType switch
    {
        "onClick" => "Clicked",
        "onValueChanged" => "ValueChanged",
        "onEndEdit" => "EndEdit",
        _ => ""
    };
    
    return "On" + ToPascalCase(cleaned) + eventSuffix;
}

private static string CleanName(string name)
{
    // 去除特殊字符，保留字母数字
    return System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9]", "");
}

private static string ToPascalCase(string name)
{
    if (string.IsNullOrEmpty(name)) return name;
    return char.ToUpper(name[0]) + name.Substring(1);
}
```

---

### 任务 23-24: ViewCodeGenerator 测试

**覆盖的上游需求:** Design 切片 D 验收标准

**任务 23:** 创建 ViewCodeGeneratorTests.cs，验证字段声明、事件绑定、部分方法生成

**任务 24:** 验证生成代码格式（4 空格缩进），命名规范正确

---

### 任务 25-26: PSD2UILock - 锁定标记组件

**覆盖的上游需求:** Design 切片 E，FRD Phase 6

**任务 25:** 创建 PSD2UILock.cs，定义锁定标记组件

**任务 26:** 添加 Inspector 面板自定义绘制（可选）

**代码（任务 25）:**
```csharp
public class PSD2UILock : MonoBehaviour
{
    public bool LockTransform = true;
    public bool LockComponents = true;
    public bool LockChildren = false;
    
    [TextArea(3, 5)]
    public string Notes = "锁定原因：";
}
```

---

### 任务 27: ChangeDetector - 配置版本对比

**覆盖的上游需求:** Design 切片 E

**关键代码:**
```csharp
public ChangeReport CompareConfigs(UINodeData oldConfig, UINodeData newConfig)
{
    var report = new ChangeReport();
    CompareNodes(oldConfig, newConfig, "", report);
    return report;
}

private void CompareNodes(UINodeData oldNode, UINodeData newNode, string path, ChangeReport report)
{
    // 对比节点属性
    if (oldNode.Rect.Width != newNode.Rect.Width || oldNode.Rect.Height != newNode.Rect.Height)
    {
        report.Modified.Add(newNode);
    }
    
    // 对比子节点
    var oldChildren = oldNode.Children.ToDictionary(c => c.Name);
    var newChildren = newNode.Children.ToDictionary(c => c.Name);
    
    foreach (var key in newChildren.Keys)
    {
        if (!oldChildren.ContainsKey(key))
            report.Added.Add(newChildren[key]);
    }
    
    foreach (var key in oldChildren.Keys)
    {
        if (!newChildren.ContainsKey(key))
            report.Removed.Add(oldChildren[key]);
    }
}
```

---

### 任务 28-29: IncrementalUpdater - 增量更新协调器

**覆盖的上游需求:** Design 切片 E，FRD Phase 7

**任务 28:** 创建 IncrementalUpdater.cs，实现 Update 方法

**任务 29:** 实现锁定检查逻辑，跳过锁定的 GameObject

**关键代码（任务 29）:**
```csharp
public void Update(GameObject prefab, ChangeReport changes, bool respectLocks)
{
    // 删除节点
    foreach (var removed in changes.Removed)
    {
        var go = FindGameObject(prefab.transform, removed.Name);
        if (go != null && (!respectLocks || !IsLocked(go)))
        {
            Object.DestroyImmediate(go);
        }
    }
    
    // 修改节点
    foreach (var modified in changes.Modified)
    {
        var go = FindGameObject(prefab.transform, modified.Name);
        if (go != null && (!respectLocks || !IsLocked(go)))
        {
            UpdateGameObject(go, modified);
        }
    }
    
    // 新增节点
    foreach (var added in changes.Added)
    {
        CreateGameObject(prefab.transform, added);
    }
}

private bool IsLocked(GameObject go)
{
    return go.GetComponent<PSD2UILock>() != null;
}
```

---

### 任务 30: UnityMenuCommands - 菜单命令入口

**覆盖的上游需求:** Design 切片 E，FRD 验收条件

**关键代码:**
```csharp
public class UnityMenuCommands
{
    [MenuItem("Assets/PSD2UGUI/Generate Prefab from Config")]
    public static void GeneratePrefabFromConfig()
    {
        string configPath = EditorUtility.OpenFilePanel("Select JSON Config", "", "json");
        if (string.IsNullOrEmpty(configPath)) return;
        
        var reader = new ConfigReader();
        var nodeData = reader.LoadFromJson(configPath);
        
        var importer = new AssetImporter();
        importer.ImportSprites(new List<UINodeData> { nodeData });
        
        string prefabPath = $"Assets/GameRes/UIPanel/{nodeData.Name}.prefab";
        var builder = new PrefabBuilder();
        var prefab = builder.BuildPrefab(nodeData, prefabPath);
        
        string moduleName = ExtractModuleName(nodeData.Name);
        string codePath = $"Assets/HotUpdate/GameLogic/{moduleName}/Views/Generated/{nodeData.Name}View.Generated.cs";
        var generator = new ViewCodeGenerator();
        generator.GenerateViewCode(prefab, nodeData.Name, codePath);
        
        Debug.Log($"Generated Prefab: {prefabPath}, Code: {codePath}");
    }
    
    [MenuItem("Assets/PSD2UGUI/Force Regenerate (Ignore Locks)")]
    public static void ForceRegenerate()
    {
        // ... 与上面类似，但 respectLocks = false
    }
}
```

---

## 完成标准

所有任务完成后，验证以下内容：

- [ ] 19 个文件全部创建，单元测试全部通过
- [ ] 使用 FRD 中的 ShopWindow 示例 JSON 运行菜单命令
- [ ] 生成的 Prefab 层级正确，锚点符合 6 种场景
- [ ] 生成的 C# 代码格式正确（4 空格缩进），编译通过
- [ ] 手动调整 Prefab 后挂载 PSD2UILock，重新生成时保留调整
- [ ] 代码提交历史清晰，每个任务一次提交

