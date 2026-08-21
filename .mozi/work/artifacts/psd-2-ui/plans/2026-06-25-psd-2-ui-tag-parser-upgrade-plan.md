# 实施计划：psd-2-ui-tag-parser-upgrade

> 日期: 2026-06-29 | 状态: 重做（原计划不完整）
> 上游设计: [PSD 标签解析器升级架构设计](../designs/2026-06-25-psd-2-ui-tag-parser-upgrade-design.md)
> 上游 FRD: [PSD 标签解析器升级功能需求文档](../discover/2026-06-25-psd-2-ui-tag-parser-upgrade-frd.md)

## 现状分析

在重做本计划之前，已对代码库进行了完整核查。**切片 A/B/C 已经完整实现**，无需重复实现。

### 已完成（切片 A/B/C）

| 文件 | 状态 | 说明 |
|------|------|------|
| `PSDExporterProject/config/tag-config.json` | ✅ 完整 | 14 main + 2 textBackend + 4 imageType + 22 role = 42 个标签（含 vbox/hbox/grid） |
| `PSDExporterProject/src/recognizer/tag-parse-result.ts` | ✅ 完整 | TagParseResult 接口定义完整 |
| `PSDExporterProject/src/recognizer/tag-config-loader.ts` | ✅ 完整 | zod 校验，load/validate 方法 |
| `PSDExporterProject/src/recognizer/tag-parser.ts` | ✅ 完整 | 右→左解析算法，ref/refp 前缀，未知标签跳过 |
| `PSDExporterProject/tests/recognizer/tag-parser.test.ts` | ✅ 完整 | 20+ 测试用例覆盖所有场景 |
| `PSDExporterProject/tests/recognizer/tag-config-loader.test.ts` | ✅ 完整 | 配置加载测试 |
| `PSDExporterProject/src/recognizer/component-types.ts` | ✅ 完整 | ComponentType 含 15 个值，ComponentInfo 含 textBackend/imageType/role |
| `PSDExporterProject/src/recognizer/component-recognizer.ts` | ✅ 完整 | 14 个 main tag 映射，TagParseResult→ComponentInfo |
| `PSDExporterProject/src/generator/json-schema.ts` | ✅ 完整 | ComponentInfoSchema 含新字段 |

### 尚未完成（切片 D/E）

| 文件 | 状态 | 说明 |
|------|------|------|
| `PSDExporterProject/tests/integration/tag-parser-integration.test.ts` | ❌ 缺失 | 端到端集成测试不存在 |
| `UnityProject/Assets/Change/Editor/PSD2UI/ComponentInfo.cs` | ❌ 缺失 | C# 数据类不存在 |
| `UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs` | ⚠️ 部分 | 只支持 Image/Text/Button/ScrollRect/InputField，缺少 Dropdown/Toggle/Slider/RawImage/Mask；无 info 参数 |
| `UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/ComponentFactoryTests.cs` | ❌ 缺失 | ComponentFactory 单元测试不存在 |

---

## 上游产出引用

| 引用内容 | 转化方式 |
|---------|----------|
| 切片 D: 集成测试与边界验证 | 任务 1-2 |
| 切片 E: Unity C# 端组件扩展 | 任务 3-6 |
| Design 切片 D 验收标准：FRD 所有验收条件的集成测试 | 任务 1 |
| Design 切片 E 验收标准：ComponentInfo.cs、ComponentFactory 扩展 | 任务 3-5 |
| Design 切片 E 验收标准：ConfigureImageType、CreateTextComponent | 任务 4 |

## 目标

在已完成切片 A/B/C 的基础上，补齐剩余工作：

1. **切片 D**：编写端到端集成测试，验证 TagParser→ComponentRecognizer→ComponentInfo 完整链路
2. **切片 E**：在 Unity C# 端新增 ComponentInfo 数据类，扩展 ComponentFactory 支持新组件类型和扩展属性

## 文件清单

| 文件 | 操作 | 职责 | 所属任务 |
|------|------|------|----------|
| `PSDExporterProject/tests/integration/tag-parser-integration.test.ts` | 创建 | 端到端集成测试 | 任务 1-2 |
| `UnityProject/Assets/Change/Editor/PSD2UI/ComponentInfo.cs` | 创建 | C# ComponentInfo 数据类 | 任务 3 |
| `UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs` | 修改 | 扩展支持新组件类型和 info 参数 | 任务 4 |
| `UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/ComponentFactoryTests.cs` | 创建 | ComponentFactory 单元测试 | 任务 5-6 |

## 任务依赖图

```
任务 1: 端到端集成测试（TypeScript）
任务 2: 运行并修复集成测试
任务 3: ComponentInfo.cs（C#）
任务 4: ComponentFactory.cs 扩展（依赖任务 3）
任务 5: ComponentFactoryTests.cs（依赖任务 4）
任务 6: 运行 Unity 测试并验证
```

## 风险评估

| 风险 | 缓解措施 |
|------|----------|
| 集成测试依赖 AI 识别路径（需要 mock） | 任务 1 用 null aiIdentifier + enableAI: false 跳过 AI 路径 |
| ComponentFactory 新增 info 参数破坏现有调用 | 参数设为可选（默认 null），向后兼容 |
| Unity 无法运行 C# 测试（CLI 环境） | 任务 6 使用已知有效的 Unity 测试命令格式 |

---

## 任务

### 任务 1: 端到端集成测试

**覆盖的上游需求:** Design 切片 D — 所有集成测试场景

**文件:**
- 创建: `PSDExporterProject/tests/integration/tag-parser-integration.test.ts`

- [ ] **步骤 1: 创建集成测试文件**

```typescript
// PSDExporterProject/tests/integration/tag-parser-integration.test.ts

import * as path from 'path';
import { TagConfigLoader } from '../../src/recognizer/tag-config-loader';
import { TagParser } from '../../src/recognizer/tag-parser';
import { ComponentRecognizer } from '../../src/recognizer/component-recognizer';
import { ComponentInfo } from '../../src/recognizer/component-types';
import { Layer } from '../../src/parser/layer-tree';

const configPath = path.resolve(__dirname, '../../config/tag-config.json');

function makeLayer(name: string): Layer {
  return {
    id: `layer-${name}`,
    name,
    type: 'image',
    bounds: { x: 0, y: 0, width: 100, height: 100 },
    visible: true,
    opacity: 1,
    children: [],
  };
}

function makeRecognizer(): ComponentRecognizer {
  const config = TagConfigLoader.load(configPath);
  const tagParser = new TagParser(config);
  return new ComponentRecognizer(null, {
    enableAI: false,
    aiThreshold: 0.8,
    cvConfidenceMin: 0.7,
  }, tagParser);
}

describe('TagParser Integration: 完整链路测试', () => {
  let recognizer: ComponentRecognizer;

  beforeEach(() => {
    recognizer = makeRecognizer();
  });

  // =========================================================================
  // 场景 1: 多标签叠加
  // =========================================================================

  test('多标签叠加: close.bt.tmp.bg → Button + tmp + bg', async () => {
    const layer = makeLayer('close.bt.tmp.bg');
    const info = await recognizer.recognize(layer);

    expect(info.type).toBe('Button');
    expect(info.textBackend).toBe('tmp');
    expect(info.role).toBe('bg');
    expect(info.confidence).toBe(1.0);
    expect(info.source).toBe('tag');
    expect(info.needsReview).toBe(false);
  });

  test('多标签叠加: icon.img.sliced → Image + sliced', async () => {
    const layer = makeLayer('icon.img.sliced');
    const info = await recognizer.recognize(layer);

    expect(info.type).toBe('Image');
    expect(info.imageType).toBe('sliced');
    expect(info.confidence).toBe(1.0);
    expect(info.source).toBe('tag');
  });

  test('多标签叠加: input.ipt.tmp.placeholder → InputField + tmp + placeholder', async () => {
    const layer = makeLayer('input.ipt.tmp.placeholder');
    const info = await recognizer.recognize(layer);

    expect(info.type).toBe('InputField');
    expect(info.textBackend).toBe('tmp');
    expect(info.role).toBe('placeholder');
    expect(info.source).toBe('tag');
  });

  // =========================================================================
  // 场景 2: 右→左优先级覆盖
  // =========================================================================

  test('右→左优先级: panel.bt.dpd → Dropdown（dpd 覆盖 bt）', async () => {
    const layer = makeLayer('panel.bt.dpd');
    const info = await recognizer.recognize(layer);

    expect(info.type).toBe('Dropdown');
    expect(info.source).toBe('tag');
  });

  test('右→左优先级: icon.sliced.simple → simple（simple 覆盖 sliced）', async () => {
    const layer = makeLayer('icon.sliced.simple');
    const info = await recognizer.recognize(layer);

    // sliced and simple are both imageType — simple wins (rightmost)
    expect(info.imageType).toBe('simple');
    expect(info.source).toBe('tag');
  });

  // =========================================================================
  // 场景 3: ref/refp 前缀保留
  // =========================================================================

  test('ref 前缀: ref icon.img → Image（prefix 在 TagParseResult 中，不影响 ComponentInfo.type）', async () => {
    const layer = makeLayer('ref icon.img');
    const info = await recognizer.recognize(layer);

    expect(info.type).toBe('Image');
    expect(info.source).toBe('tag');
    // ComponentInfo 不存储 prefix，prefix 留在 TagParseResult 供 Unity 生成器消费
  });

  test('refp 前缀: refp panel.bt → Button', async () => {
    const layer = makeLayer('refp panel.bt');
    const info = await recognizer.recognize(layer);

    expect(info.type).toBe('Button');
    expect(info.source).toBe('tag');
  });

  // =========================================================================
  // 场景 4: 未识别标签跳过
  // =========================================================================

  test('未识别标签跳过: name.unknowntag.bt → Button', async () => {
    const layer = makeLayer('name.unknowntag.bt');
    const info = await recognizer.recognize(layer);

    expect(info.type).toBe('Button');
    expect(info.source).toBe('tag');
  });

  // =========================================================================
  // 场景 5: 无标签图层走 AI 兜底（AI 禁用时返回 Unknown）
  // =========================================================================

  test('无标签图层 AI 禁用: background → Unknown（AI 兜底流程）', async () => {
    const layer = makeLayer('background');
    const info = await recognizer.recognize(layer);

    expect(info.type).toBe('Unknown');
    expect(info.confidence).toBe(0);
    expect(info.source).toBe('ai');
    // needsReview = false（因为 enableAI: false）
    expect(info.needsReview).toBe(false);
  });

  test('全是未识别标签: name.xyz.abc → Unknown', async () => {
    const layer = makeLayer('name.xyz.abc');
    const info = await recognizer.recognize(layer);

    expect(info.type).toBe('Unknown');
    expect(info.source).toBe('ai');
  });

  // =========================================================================
  // 场景 6: 新组件类型
  // =========================================================================

  test('新组件类型: dropdown.dpd → Dropdown', async () => {
    const layer = makeLayer('dropdown.dpd');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Dropdown');
  });

  test('新组件类型: toggle.tg → Toggle', async () => {
    const layer = makeLayer('toggle.tg');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Toggle');
  });

  test('新组件类型: slider.sld → Slider', async () => {
    const layer = makeLayer('slider.sld');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Slider');
  });

  test('新组件类型: raw.rimg → RawImage', async () => {
    const layer = makeLayer('raw.rimg');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('RawImage');
  });

  test('新组件类型: mask.msk → Mask', async () => {
    const layer = makeLayer('mask.msk');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Mask');
  });

  test('新组件类型: fill.col → FillColor', async () => {
    const layer = makeLayer('fill.col');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('FillColor');
  });

  // =========================================================================
  // 场景 7: 布局组件
  // =========================================================================

  test('布局组件: list.vbox → VerticalLayoutGroup', async () => {
    const layer = makeLayer('list.vbox');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('VerticalLayoutGroup');
  });

  test('布局组件: row.hbox → HorizontalLayoutGroup', async () => {
    const layer = makeLayer('row.hbox');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('HorizontalLayoutGroup');
  });

  test('布局组件: grid.grid → GridLayoutGroup', async () => {
    const layer = makeLayer('grid.grid');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('GridLayoutGroup');
  });

  // =========================================================================
  // 场景 8: 边界情况
  // =========================================================================

  test('边界: 空字符串 → Unknown', async () => {
    const layer = makeLayer('');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Unknown');
  });

  test('边界: 只有标签无 baseName: bt.tmp → Button + tmp', async () => {
    const layer = makeLayer('bt.tmp');
    const info = await recognizer.recognize(layer);
    expect(info.type).toBe('Button');
    expect(info.textBackend).toBe('tmp');
  });

  // =========================================================================
  // 场景 9: recognizeTree 递归识别
  // =========================================================================

  test('recognizeTree: 父子节点各自识别', async () => {
    const parent: Layer = {
      id: 'parent',
      name: 'panel.bt',
      type: 'group',
      bounds: { x: 0, y: 0, width: 200, height: 200 },
      visible: true,
      opacity: 1,
      children: [
        {
          id: 'child',
          name: 'bg.img.sliced',
          type: 'image',
          bounds: { x: 0, y: 0, width: 200, height: 200 },
          visible: true,
          opacity: 1,
          children: [],
        },
      ],
    };

    const results = await recognizer.recognizeTree(parent);

    expect(results.size).toBe(2);
    expect(results.get('parent')?.type).toBe('Button');
    expect(results.get('child')?.type).toBe('Image');
    expect(results.get('child')?.imageType).toBe('sliced');
  });
});
```

- [ ] **步骤 2: 检查 Layer 接口定义，确认 makeLayer 构造正确**

```bash
cat /Users/andy/Workspace/github/andycai/fun/PSDExporterProject/src/parser/layer-tree.ts | head -40
```

预期: 看到 Layer 接口定义，确认字段名（id, name, type, bounds, visible, opacity, children）

---

### 任务 2: 运行集成测试

**覆盖的上游需求:** Design 切片 D 验收标准 — 所有场景通过

**文件:**
- 无新文件

- [ ] **步骤 1: 运行集成测试**

```bash
cd /Users/andy/Workspace/github/andycai/fun/PSDExporterProject && npm test -- tests/integration/tag-parser-integration.test.ts 2>&1
```

预期: 所有测试通过，输出类似：
```
PASS tests/integration/tag-parser-integration.test.ts
  TagParser Integration: 完整链路测试
    ✓ 多标签叠加: close.bt.tmp.bg → Button + tmp + bg
    ✓ ...
Tests: 19 passed, 19 total
```

- [ ] **步骤 2: 如果有测试失败，根据错误信息修复**

常见失败原因：
- Layer 接口字段不匹配 → 调整 makeLayer 中的字段
- ComponentRecognizer 构造参数不匹配 → 查看实际构造函数签名并调整
- 路径问题 → 检查 configPath

- [ ] **步骤 3: 运行完整测试套件（回归检查）**

```bash
cd /Users/andy/Workspace/github/andycai/fun/PSDExporterProject && npm test 2>&1
```

预期: 所有现有测试仍然通过，集成测试也通过

- [ ] **步骤 4: Commit**

```bash
cd /Users/andy/Workspace/github/andycai/fun
git add PSDExporterProject/tests/integration/tag-parser-integration.test.ts
git commit -m "test(integration): add end-to-end tag parser integration tests

- 19 test cases covering all FRD acceptance criteria
- Multi-tag stacking, right-to-left priority, ref/refp prefix
- Unknown tag skipping, AI fallback, new component types
- Layout components (vbox/hbox/grid), edge cases
- recognizeTree recursive recognition

```

---

### 任务 3: ComponentInfo.cs 数据类

**覆盖的上游需求:** Design 切片 E — ComponentInfo C# 数据类

**文件:**
- 创建: `UnityProject/Assets/Change/Editor/PSD2UI/ComponentInfo.cs`

- [ ] **步骤 1: 创建 ComponentInfo.cs**

```csharp
// UnityProject/Assets/Change/Editor/PSD2UI/ComponentInfo.cs

using System;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// 组件识别结果数据类，对应 TypeScript 端导出的 ComponentInfo JSON 结构。
    /// 由 Newtonsoft.Json 自动反序列化，所有可选字段为 null 时不影响默认行为。
    /// </summary>
    [Serializable]
    public class ComponentInfo
    {
        /// <summary>
        /// 组件类型。对应 TypeScript ComponentType。
        /// 可选值: "Button" | "Image" | "RawImage" | "Text" | "ScrollView" | "InputField" |
        ///         "Dropdown" | "Toggle" | "Slider" | "Mask" | "FillColor" |
        ///         "VerticalLayoutGroup" | "HorizontalLayoutGroup" | "GridLayoutGroup" | "Unknown"
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 文本后端（可选）。"tmp" = TextMeshPro，"ugui" = UGUI Text。
        /// 仅当 Type 为 "Text" 或包含文本的组件时有意义。
        /// </summary>
        public string TextBackend { get; set; }

        /// <summary>
        /// 图片类型（可选）。"simple" | "sliced" | "tiled" | "filled"。
        /// 仅当 Type 为 "Image" 时有意义，用于设置 Image.type 属性。
        /// </summary>
        public string ImageType { get; set; }

        /// <summary>
        /// 角色标签（可选）。如 "bg", "press", "placeholder" 等。
        /// 用于后续子组件识别功能（当前版本保留字段，不消费）。
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// 识别置信度，范围 0-1。Tag 识别时为 1.0，AI 识别时为模型置信度。
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// 识别来源。"tag" | "cv" | "ai"。
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 是否需要人工审查。置信度低或 AI 未识别时为 true。
        /// </summary>
        public bool NeedsReview { get; set; }
    }
}
```

- [ ] **步骤 2: 确认文件保存并无语法错误（Unity 编译前快速检查）**

```bash
cat /Users/andy/Workspace/github/andycai/fun/UnityProject/Assets/Change/Editor/PSD2UI/ComponentInfo.cs
```

预期: 输出文件内容，无截断

- [ ] **步骤 3: Commit**

```bash
cd /Users/andy/Workspace/github/andycai/fun
git add UnityProject/Assets/Change/Editor/PSD2UI/ComponentInfo.cs
git commit -m "feat(unity/psd2ui): add ComponentInfo C# data class

- Maps TypeScript ComponentInfo JSON structure to C#
- Supports Type/TextBackend/ImageType/Role/Confidence/Source/NeedsReview
- All optional fields (TextBackend/ImageType/Role) default to null
- Newtonsoft.Json compatible for automatic deserialization

```

---

### 任务 4: ComponentFactory.cs 扩展

**覆盖的上游需求:** Design 切片 E — 扩展 ComponentFactory，支持新组件类型和扩展属性

**依赖:** 任务 3（ComponentInfo.cs 已存在）

**文件:**
- 修改: `UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs`

当前状态（需保留）：支持 Image/Text/Button/ScrollRect/InputField

新增内容：
1. 可选参数 `ComponentInfo info = null`
2. 新组件类型：RawImage/Dropdown/Toggle/Slider/Mask（FillColor 无对应 Unity 组件，跳过）
3. `ConfigureImageType()` 私有方法
4. `CreateTextComponent()` 私有方法

- [ ] **步骤 1: 替换 ComponentFactory.cs 完整内容**

```csharp
// UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Factory for creating UI components on GameObjects.
    /// Supports Image, RawImage, Text (TextMeshProUGUI or UGUI), Button,
    /// ScrollRect, InputField (TMP_InputField), Dropdown, Toggle, Slider, Mask.
    /// </summary>
    public class ComponentFactory
    {
        /// <summary>
        /// Creates a UI component of the specified type on the target GameObject.
        /// </summary>
        /// <param name="type">
        /// Component type: "Image" | "RawImage" | "Text" | "Button" | "ScrollRect" |
        /// "InputField" | "Dropdown" | "Toggle" | "Slider" | "Mask" | "FillColor"
        /// </param>
        /// <param name="target">The GameObject to add the component to</param>
        /// <param name="info">
        /// Optional ComponentInfo with extended attributes (ImageType, TextBackend, Role).
        /// Pass null to use default behavior (backward compatible).
        /// </param>
        /// <returns>The created Component, or null if the type is unrecognized</returns>
        public Component CreateComponent(string type, GameObject target, ComponentInfo info = null)
        {
            if (target == null)
            {
                Debug.LogWarning("[PSD2UI] ComponentFactory.CreateComponent: target is null.");
                return null;
            }

            if (string.IsNullOrEmpty(type))
            {
                Debug.LogWarning("[PSD2UI] ComponentFactory.CreateComponent: type is null or empty.");
                return null;
            }

            switch (type)
            {
                case "Image":
                    var image = target.AddComponent<Image>();
                    if (info?.ImageType != null)
                        ConfigureImageType(image, info.ImageType);
                    return image;

                case "RawImage":
                    return target.AddComponent<RawImage>();

                case "Text":
                    return CreateTextComponent(info?.TextBackend, target);

                case "Button":
                    var button = target.AddComponent<Button>();
                    var buttonImage = target.AddComponent<Image>();
                    button.targetGraphic = buttonImage;
                    return button;

                case "ScrollRect":
                case "ScrollView":
                    return target.AddComponent<ScrollRect>();

                case "InputField":
                    return target.AddComponent<TMP_InputField>();

                case "Dropdown":
                    return target.AddComponent<Dropdown>();

                case "Toggle":
                    return target.AddComponent<Toggle>();

                case "Slider":
                    return target.AddComponent<Slider>();

                case "Mask":
                    return target.AddComponent<Mask>();

                case "FillColor":
                    // FillColor 没有对应的 Unity 组件，使用 Image 替代
                    Debug.LogWarning("[PSD2UI] ComponentFactory: FillColor has no direct Unity component. Using Image.");
                    return target.AddComponent<Image>();

                case "VerticalLayoutGroup":
                    return target.AddComponent<VerticalLayoutGroup>();

                case "HorizontalLayoutGroup":
                    return target.AddComponent<HorizontalLayoutGroup>();

                case "GridLayoutGroup":
                    return target.AddComponent<GridLayoutGroup>();

                default:
                    Debug.LogWarning($"[PSD2UI] ComponentFactory: unrecognized component type '{type}'.");
                    return null;
            }
        }

        /// <summary>
        /// Configures the Image.type property based on the imageType string.
        /// </summary>
        /// <param name="image">The Image component to configure</param>
        /// <param name="imageType">"simple" | "sliced" | "tiled" | "filled"</param>
        private void ConfigureImageType(Image image, string imageType)
        {
            switch (imageType)
            {
                case "simple":
                    image.type = Image.Type.Simple;
                    break;
                case "sliced":
                    image.type = Image.Type.Sliced;
                    break;
                case "tiled":
                    image.type = Image.Type.Tiled;
                    break;
                case "filled":
                    image.type = Image.Type.Filled;
                    break;
                default:
                    Debug.LogWarning($"[PSD2UI] ComponentFactory: unknown imageType '{imageType}', using Simple.");
                    image.type = Image.Type.Simple;
                    break;
            }
        }

        /// <summary>
        /// Creates a text component based on the textBackend preference.
        /// </summary>
        /// <param name="textBackend">"ugui" for UGUI Text, anything else (or null) for TextMeshProUGUI</param>
        /// <param name="target">The GameObject to add the component to</param>
        /// <returns>TextMeshProUGUI or UnityEngine.UI.Text component</returns>
        private Component CreateTextComponent(string textBackend, GameObject target)
        {
            if (textBackend == "ugui")
                return target.AddComponent<UnityEngine.UI.Text>();
            else
                return target.AddComponent<TextMeshProUGUI>(); // Default: TMP
        }
    }
}
```

- [ ] **步骤 2: 确认文件写入正确**

```bash
grep -n "case \"Dropdown\"" /Users/andy/Workspace/github/andycai/fun/UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs
grep -n "ConfigureImageType" /Users/andy/Workspace/github/andycai/fun/UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs
grep -n "CreateTextComponent" /Users/andy/Workspace/github/andycai/fun/UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs
```

预期: 各 grep 命令均找到对应行，行号合理

- [ ] **步骤 3: Commit**

```bash
cd /Users/andy/Workspace/github/andycai/fun
git add UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs
git commit -m "feat(unity/psd2ui): extend ComponentFactory with new component types

- Add optional ComponentInfo parameter (backward compatible, defaults to null)
- Add support for: RawImage, Dropdown, Toggle, Slider, Mask, FillColor (→Image)
- Add VerticalLayoutGroup, HorizontalLayoutGroup, GridLayoutGroup
- Add ScrollView as alias for ScrollRect
- Add ConfigureImageType(): sets Image.type from imageType string
- Add CreateTextComponent(): selects TMP or UGUI Text based on textBackend

```

---

### 任务 5: ComponentFactoryTests.cs 单元测试

**覆盖的上游需求:** Design 切片 E 验收标准 — 单元测试覆盖新旧调用方式

**依赖:** 任务 4（ComponentFactory.cs 已更新）

**文件:**
- 创建: `UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/ComponentFactoryTests.cs`

注意：Unity 单元测试需要 `[UnityTest]` 或 `[Test]` 属性，且测试类需要在 EditMode 测试集中运行。ComponentFactory 在测试中需要真实的 `GameObject`，Unity 编辑器测试支持这种操作。

- [ ] **步骤 1: 创建 ComponentFactoryTests.cs**

```csharp
// UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/ComponentFactoryTests.cs

using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Change.Editor.PSD2UI;

namespace Change.Editor.PSD2UI.Tests
{
    /// <summary>
    /// EditMode tests for ComponentFactory.
    /// Tests both the original API (no info param) and the extended API (with ComponentInfo).
    /// </summary>
    public class ComponentFactoryTests
    {
        private ComponentFactory factory;
        private GameObject testGo;

        [SetUp]
        public void SetUp()
        {
            factory = new ComponentFactory();
            testGo = new GameObject("TestGO");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(testGo);
        }

        // =====================================================================
        // 向后兼容：无 info 参数（原有 API）
        // =====================================================================

        [Test]
        public void CreateComponent_Image_NoInfo_CreatesImage()
        {
            var component = factory.CreateComponent("Image", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Image>(component);
        }

        [Test]
        public void CreateComponent_Text_NoInfo_CreatesTMP()
        {
            var component = factory.CreateComponent("Text", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<TextMeshProUGUI>(component);
        }

        [Test]
        public void CreateComponent_Button_NoInfo_CreatesButton()
        {
            var component = factory.CreateComponent("Button", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Button>(component);
        }

        [Test]
        public void CreateComponent_ScrollRect_NoInfo_CreatesScrollRect()
        {
            var component = factory.CreateComponent("ScrollRect", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<ScrollRect>(component);
        }

        [Test]
        public void CreateComponent_InputField_NoInfo_CreatesTMPInputField()
        {
            var component = factory.CreateComponent("InputField", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<TMP_InputField>(component);
        }

        // =====================================================================
        // 新组件类型
        // =====================================================================

        [Test]
        public void CreateComponent_RawImage_CreatesRawImage()
        {
            var component = factory.CreateComponent("RawImage", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<RawImage>(component);
        }

        [Test]
        public void CreateComponent_Dropdown_CreatesDropdown()
        {
            var component = factory.CreateComponent("Dropdown", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Dropdown>(component);
        }

        [Test]
        public void CreateComponent_Toggle_CreatesToggle()
        {
            var component = factory.CreateComponent("Toggle", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Toggle>(component);
        }

        [Test]
        public void CreateComponent_Slider_CreatesSlider()
        {
            var component = factory.CreateComponent("Slider", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Slider>(component);
        }

        [Test]
        public void CreateComponent_Mask_CreatesMask()
        {
            var component = factory.CreateComponent("Mask", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Mask>(component);
        }

        [Test]
        public void CreateComponent_FillColor_FallsBackToImage()
        {
            var component = factory.CreateComponent("FillColor", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<Image>(component);
        }

        [Test]
        public void CreateComponent_ScrollView_CreatesScrollRect()
        {
            var component = factory.CreateComponent("ScrollView", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<ScrollRect>(component);
        }

        [Test]
        public void CreateComponent_VerticalLayoutGroup_CreatesVLG()
        {
            var component = factory.CreateComponent("VerticalLayoutGroup", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<VerticalLayoutGroup>(component);
        }

        [Test]
        public void CreateComponent_HorizontalLayoutGroup_CreatesHLG()
        {
            var component = factory.CreateComponent("HorizontalLayoutGroup", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<HorizontalLayoutGroup>(component);
        }

        [Test]
        public void CreateComponent_GridLayoutGroup_CreatesGLG()
        {
            var component = factory.CreateComponent("GridLayoutGroup", testGo);
            Assert.IsNotNull(component);
            Assert.IsInstanceOf<GridLayoutGroup>(component);
        }

        // =====================================================================
        // 扩展属性：ImageType
        // =====================================================================

        [Test]
        public void CreateComponent_Image_WithSlicedImageType_SetsImageTypeSliced()
        {
            var info = new ComponentInfo { Type = "Image", ImageType = "sliced" };
            var component = factory.CreateComponent("Image", testGo, info);

            Assert.IsNotNull(component);
            var image = testGo.GetComponent<Image>();
            Assert.IsNotNull(image);
            Assert.AreEqual(Image.Type.Sliced, image.type);
        }

        [Test]
        public void CreateComponent_Image_WithSimpleImageType_SetsImageTypeSimple()
        {
            var info = new ComponentInfo { Type = "Image", ImageType = "simple" };
            var component = factory.CreateComponent("Image", testGo, info);

            var image = testGo.GetComponent<Image>();
            Assert.AreEqual(Image.Type.Simple, image.type);
        }

        [Test]
        public void CreateComponent_Image_WithTiledImageType_SetsImageTypeTiled()
        {
            var info = new ComponentInfo { Type = "Image", ImageType = "tiled" };
            var component = factory.CreateComponent("Image", testGo, info);

            var image = testGo.GetComponent<Image>();
            Assert.AreEqual(Image.Type.Tiled, image.type);
        }

        [Test]
        public void CreateComponent_Image_WithFilledImageType_SetsImageTypeFilled()
        {
            var info = new ComponentInfo { Type = "Image", ImageType = "filled" };
            var component = factory.CreateComponent("Image", testGo, info);

            var image = testGo.GetComponent<Image>();
            Assert.AreEqual(Image.Type.Filled, image.type);
        }

        [Test]
        public void CreateComponent_Image_WithNullInfo_UsesDefaultImageType()
        {
            var component = factory.CreateComponent("Image", testGo, null);

            var image = testGo.GetComponent<Image>();
            Assert.IsNotNull(image);
            // Default Image.type is Simple
            Assert.AreEqual(Image.Type.Simple, image.type);
        }

        // =====================================================================
        // 扩展属性：TextBackend
        // =====================================================================

        [Test]
        public void CreateComponent_Text_WithTMPBackend_CreatesTMPComponent()
        {
            var info = new ComponentInfo { Type = "Text", TextBackend = "tmp" };
            var component = factory.CreateComponent("Text", testGo, info);

            Assert.IsNotNull(component);
            Assert.IsInstanceOf<TextMeshProUGUI>(component);
        }

        [Test]
        public void CreateComponent_Text_WithUGUIBackend_CreatesUGUIText()
        {
            var info = new ComponentInfo { Type = "Text", TextBackend = "ugui" };
            var component = factory.CreateComponent("Text", testGo, info);

            Assert.IsNotNull(component);
            Assert.IsInstanceOf<UnityEngine.UI.Text>(component);
        }

        [Test]
        public void CreateComponent_Text_WithNullBackend_DefaultsToTMP()
        {
            var info = new ComponentInfo { Type = "Text", TextBackend = null };
            var component = factory.CreateComponent("Text", testGo, info);

            Assert.IsNotNull(component);
            Assert.IsInstanceOf<TextMeshProUGUI>(component);
        }

        // =====================================================================
        // 边界情况
        // =====================================================================

        [Test]
        public void CreateComponent_NullTarget_ReturnsNull()
        {
            var component = factory.CreateComponent("Image", null);
            Assert.IsNull(component);
        }

        [Test]
        public void CreateComponent_NullType_ReturnsNull()
        {
            var component = factory.CreateComponent(null, testGo);
            Assert.IsNull(component);
        }

        [Test]
        public void CreateComponent_UnknownType_ReturnsNull()
        {
            var component = factory.CreateComponent("UnknownComponent", testGo);
            Assert.IsNull(component);
        }
    }
}
```

- [ ] **步骤 2: 确认文件写入正确**

```bash
grep -c "\[Test\]" /Users/andy/Workspace/github/andycai/fun/UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/ComponentFactoryTests.cs
```

预期: 输出 `28`（28 个测试方法）

- [ ] **步骤 3: Commit**

```bash
cd /Users/andy/Workspace/github/andycai/fun
git add UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/ComponentFactoryTests.cs
git commit -m "test(unity/psd2ui): add ComponentFactory unit tests

- 28 test cases covering original and extended API
- Backward compatibility: Image/Text/Button/ScrollRect/InputField without info
- New component types: RawImage/Dropdown/Toggle/Slider/Mask/FillColor
- Layout groups: VerticalLayoutGroup/HorizontalLayoutGroup/GridLayoutGroup
- ImageType configuration: simple/sliced/tiled/filled
- TextBackend selection: tmp (TMP) vs ugui
- Edge cases: null target, null type, unknown type

```

---

### 任务 6: Unity 测试执行与验证

**覆盖的上游需求:** Design 切片 E 验收标准 — 测试通过

**文件:**
- 无新文件

- [ ] **步骤 1: 查找 Unity 可执行文件路径**

```bash
find /Applications -name "Unity" -type f 2>/dev/null | head -5
ls /Applications/Unity/Hub/Editor/ 2>/dev/null || echo "找不到 Unity Hub 路径"
```

预期: 找到 Unity 可执行文件路径，记录版本号

- [ ] **步骤 2: 运行 PSD2UI EditMode 测试**

用找到的路径替换 `<UNITY_PATH>`：

```bash
<UNITY_PATH> \
  -batchmode \
  -nographics \
  -projectPath /Users/andy/Workspace/github/andycai/fun/UnityProject \
  -runTests \
  -testPlatform EditMode \
  -testFilter "Change.Editor.PSD2UI.Tests.ComponentFactoryTests" \
  -testResults /tmp/ComponentFactoryTests-results.xml \
  -logFile /tmp/unity-componentfactory-test.log \
  -quit
```

预期: 退出码 0，测试通过

- [ ] **步骤 3: 查看测试结果**

```bash
cat /tmp/unity-componentfactory-test.log | grep -E "(PASS|FAIL|Error|test)" | tail -20
```

或者解析 XML：

```bash
cat /tmp/ComponentFactoryTests-results.xml | grep -E 'result="(Passed|Failed)"' | wc -l
```

- [ ] **步骤 4: 如果测试失败，检查编译错误**

Unity 编译错误通常在 log 中以 `error CS` 开头：

```bash
grep "error CS" /tmp/unity-componentfactory-test.log
```

常见修复：
- `Mask` 组件需要 `using UnityEngine.UI;`（已包含）
- `Dropdown` 在 Unity 2020+ 可能需要 `UnityEngine.UI.Dropdown`（非 TMP）确认版本
- `TMP_InputField` 需要 TextMeshPro 包已安装（查看 `UnityProject/Packages/manifest.json`）

- [ ] **步骤 5: 最终 Commit（如有修复）**

```bash
cd /Users/andy/Workspace/github/andycai/fun
git add -A
git commit -m "fix(unity/psd2ui): fix compilation errors in ComponentFactory/Tests

[描述具体修复内容]

```

---

## 计划自检

### 上游覆盖度检查

| 设计文档需求 | 对应任务 | 覆盖状态 |
|------------|----------|---------|
| 切片 D: 7 个集成测试场景 | 任务 1-2 | ✅ 覆盖（19 个测试用例） |
| 切片 D: FRD 核心解析功能验收条件 | 任务 1 多标签/优先级/跳过 | ✅ |
| 切片 D: FRD 前缀处理验收条件 | 任务 1 ref/refp 测试 | ✅ |
| 切片 D: FRD 边界情况验收条件 | 任务 1 边界测试 | ✅ |
| 切片 D: 回归测试（现有测试套件） | 任务 2 步骤 3 | ✅ |
| 切片 E: ComponentInfo.cs 数据类 | 任务 3 | ✅ |
| 切片 E: ComponentFactory 支持 Dropdown/Toggle/Slider/RawImage/Mask | 任务 4 | ✅ |
| 切片 E: ConfigureImageType() | 任务 4 | ✅ |
| 切片 E: CreateTextComponent() textBackend 选择 | 任务 4 | ✅ |
| 切片 E: 向后兼容（无 info 参数） | 任务 4（可选参数默认 null） | ✅ |
| 切片 E: ComponentFactory 单元测试 | 任务 5-6 | ✅ |

### 占位符扫描

- 无"TODO"、"待定"、"后续实现"
- 每个步骤均有具体代码块或命令
- 无模糊描述（"添加适当的测试"等）

### 类型一致性

- `ComponentInfo` 在 C# 端：`Type/TextBackend/ImageType/Role`（string 类型）
- `ComponentFactory.CreateComponent(string type, GameObject target, ComponentInfo info = null)` 签名跨任务 4/5/6 一致
- `Image.Type.Sliced`（Unity enum 值）在任务 4 和任务 5 中一致
- `TextMeshProUGUI`（TMP 组件名）在任务 4 和任务 5 中一致

---

## 执行方式

计划审查确认后，建议使用 `mz-implement` 按任务顺序内联执行：

```
任务 1 → 任务 2 → 任务 3 → 任务 4 → 任务 5 → 任务 6
```

任务 1-2（TypeScript 端）可先独立完成并验证，再进行 Unity C# 端（任务 3-6）。
