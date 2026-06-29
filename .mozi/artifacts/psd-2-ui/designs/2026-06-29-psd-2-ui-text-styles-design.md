# PSD 文字样式同步与对齐支持 架构设计

> 日期: 2026-06-29 | 状态: 草稿
> FRD: [PSD 文字样式同步与对齐支持 FRD](../discover/2026-06-29-psd-2-ui-text-styles-frd.md)
> 上游: [代码调研报告](../research/2026-06-29-psd-2-ui-text-styles-research.md)

## 上游产出引用

### 来自 FRD

| 引用内容 | 如何使用 |
|---------|----------|
| Decision #1: 支持 6 种效果 | 切片 4 实现完整的效果提取和应用 |
| Decision #3: 使用项目现有 TMP_SDF.shader | 切片 4 的 Shader 参数映射直接使用现有 shader |
| Decision #5: 锁定标记（PSD2UILock） | 切片 3 和 4 检测锁定组件跳过更新 |
| Decision #7: 每个文本图层独立 Material | 切片 4 为每个文本创建独立 Material |
| Decision #8: 在 layers 数组中增加 textStyles 字段 | 切片 1 扩展 JSON Schema |
| 验收条件: 视觉还原度 > 90% | 切片 4 的验收标准 |
| 验收条件: 50+ 文本对象解析 < 10 秒 | 切片 2 和 4 的性能要求 |

### 来自 Research

| 引用内容 | 如何使用 |
|---------|----------|
| ag-psd v30.2.0 完整支持 6 种效果 API | 切片 2 和 4 使用 `LayerTextData` 和 `LayerEffectsInfo` 接口 |
| TMP_SDF.shader 参数完整覆盖 | 切片 4 建立完整的 Shader 参数映射表 |
| 现有模式: 单一职责（asset-exporter.ts） | 切片 2 的 `TextStyleExtractor` 参考此模式 |
| 集成点: `psd-parser.ts` 的 `convertLayer()` | 切片 2 在此方法中调用提取器 |
| 集成点: `ComponentFactory.CreateComponent()` | 切片 3 在此方法中调用应用器 |
| PSD2UILock 已实现可复用 | 切片 3 和 4 直接使用 `LockComponents` 字段 |
| 风险: 颜色空间转换 | 切片 2 使用 color-convert 库统一转换为 RGBA |
| 风险: 字号 DPI 转换未确定 | 切片 2 使用缩放因子 1.0 作为初始值，可配置 |
| 风险: 内阴影视觉效果未验证 | 切片 4 通过 `_UnderlayDilate` 负值实现，需测试 |

## 架构决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 架构方案 | 单体模块设计 | 符合现有代码库模式，职责清晰，易于维护；6 种效果是固定的不需要插件化 |
| 文本样式提取器位置 | `PSDExporterProject/src/parser/text-style-extractor.ts`（新建） | 单一职责，参考 `asset-exporter.ts` 模式 |
| 文本样式应用器位置 | `UnityProject/Assets/Change/Editor/PSD2UI/TextStyleApplier.cs`（新建） | 独立模块，便于单元测试 |
| JSON Schema 扩展策略 | 在 `Layer` 接口中增加可选字段 `textStyles?: TextStyles` | 向后兼容，旧版本忽略该字段 |
| 颜色空间转换 | 使用 `color-convert` 库统一转换为 RGBA | 支持 CMYK/Lab 等多种颜色空间 |
| 字号 DPI 转换公式 | `unityFontSize = psdFontSize * scaleFactor`，初始 scaleFactor = 1.0 | 通过实际测试确定缩放因子，支持可配置 |
| 渐变降级策略 | 斜向/径向 → 垂直/水平，多色 → 双色 | 简化实现，覆盖 80% 常见需求，生成警告日志 |
| Material 命名规则 | `TMP_<LayerName>_<LayerID>_Material.mat` | 避免重名，便于追踪 |
| Material 存储位置 | `Assets/GameRes/Materials/UI/PSD2UI/` | 独立目录，便于管理和清理 |
| 内阴影实现方式 | `_UnderlayDilate` 设为负值（-0.5） | 利用现有 Shader 参数，需视觉验证 |
| TMP 默认字体策略 | 使用 TMP Settings 默认字体，失败时使用 Unity 内置 Arial | 不自动导入字体，避免版权和项目混乱 |

## 文件地图

### Node.js 解析器

| 文件 | 所属切片 | 职责 |
|------|----------|------|
| `PSDExporterProject/src/parser/layer-tree.ts` | 切片 1 | 扩展 `Layer` 接口，定义 `TextStyles` 类型 |
| `PSDExporterProject/src/generator/json-schema.ts` | 切片 1 | 定义 `TextStylesSchema` 和效果 Schema（Zod） |
| `PSDExporterProject/tests/unit/json-schema.test.ts` | 切片 1 | JSON Schema 验证测试 |
| `PSDExporterProject/src/parser/text-style-extractor.ts` | 切片 2, 4 | 文本样式提取器（新建），包含基础属性和 6 种效果提取 |
| `PSDExporterProject/src/parser/psd-parser.ts` | 切片 2 | 集成 `TextStyleExtractor` 到 `convertLayer()` |
| `PSDExporterProject/tests/unit/text-style-extractor.test.ts` | 切片 2, 4 | 提取器单元测试 |

### Unity 生成器

| 文件 | 所属切片 | 职责 |
|------|----------|------|
| `UnityProject/Assets/Change/Editor/PSD2UI/TextStyleApplier.cs` | 切片 3, 4 | 文本样式应用器（新建），包含基础属性和效果应用 |
| `UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs` | 切片 3 | 集成 `TextStyleApplier`，调用 `ApplyBasicProperties()` |
| `UnityProject/Assets/Change/Editor/PSD2UI/PrefabBuilder.cs` | 切片 3 | 传递 `textStyles` 数据到 `ComponentFactory` |
| `UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/TextStyleApplierTests.cs` | 切片 3, 4 | 应用器单元测试 |

## 切片分解

### 切片 1：JSON Schema 扩展与基础文本属性

**依赖：** 无  
**风险等级：** 低  
**涉及文件：**
- `PSDExporterProject/src/parser/layer-tree.ts`
- `PSDExporterProject/src/generator/json-schema.ts`
- `PSDExporterProject/tests/unit/json-schema.test.ts`

**内容：** 定义 `TextStyles` 接口和相关类型，扩展 JSON Schema 支持文本样式字段。为后续切片提供数据契约基础。

**接口契约：**

```typescript
// layer-tree.ts
export interface TextStyles {
  fontSize: number;
  color: { r: number; g: number; b: number; a: number };
  fontName: string;
  fontStyle: { bold: boolean; italic: boolean };
  alignment: { horizontal: 'left' | 'center' | 'right' | 'justify'; vertical: 'top' | 'middle' | 'bottom' };
  effects?: TextEffect[];
}

export interface TextEffect {
  type: 'stroke' | 'dropShadow' | 'innerShadow' | 'gradient' | 'outerGlow' | 'bevel';
  enabled: boolean;
  [key: string]: any;
}

export interface Layer {
  // ... 现有字段
  textStyles?: TextStyles;
}
```

**数据契约：**
- **输出：** TypeScript 类型定义和 Zod Schema
- **不变量：** 颜色值 [0, 1]，字号 > 0，alignment 枚举值有效

**验收标准：**
- [ ] TypeScript 编译通过，无类型错误
- [ ] Zod 验证器接受合法 JSON（包含所有必需字段）
- [ ] Zod 验证器拒绝非法 JSON（如负字号、超范围颜色）
- [ ] 单元测试覆盖所有验证规则

**回归风险评估：**
- **影响范围：** JSON Schema 定义，`Layer` 接口
- **风险：** 极低，新增可选字段不影响现有解析逻辑
- **缓解措施：** 运行现有 JSON Schema 测试套件确保向后兼容

---

### 切片 2：文本基础属性提取（Node.js）

**依赖：** 切片 1  
**风险等级：** 中  
**涉及文件：**
- `PSDExporterProject/src/parser/text-style-extractor.ts`（新建）
- `PSDExporterProject/src/parser/psd-parser.ts`
- `PSDExporterProject/tests/unit/text-style-extractor.test.ts`（新建）

**内容：** 创建 `TextStyleExtractor` 类，从 ag-psd 的 `LayerTextData` 提取基础属性（字号、颜色、字体、对齐），处理颜色空间转换和字号 DPI 转换。集成到 `convertLayer()` 方法。

**接口契约：**

```typescript
export class TextStyleExtractor {
  extract(textData: LayerTextData): TextStyles;
  private convertColor(color: Color): { r: number; g: number; b: number; a: number };
  private convertFontSize(psdFontSize: number): number;
  private convertAlignment(justification: Justification, shapeType: 'point' | 'box'): { horizontal: string; vertical: string };
}
```

**数据契约：**
- **输入：** ag-psd 的 `LayerTextData`（包含 `style`, `paragraphStyle`, `text` 等字段）
- **输出：** 标准化的 `TextStyles` 对象
- **转换规则：**
  - 颜色：CMYK/Lab/RGB → RGBA [0, 1]（使用 color-convert 库）
  - 字号：`psdFontSize * 1.0`（初始缩放因子，可配置）
  - 对齐：`justification` → horizontal；`shapeType` → vertical 推断

**行为契约：**
- `textData` 为 null → 返回默认样式（12pt, 黑色, 左对齐, Arial）
- 颜色空间不支持 → 记录警告，使用黑色 (0,0,0,1)
- 字体名称缺失 → 使用 "Arial"

**验收标准：**
- [ ] 单元测试覆盖所有颜色空间（RGBA, CMYK, Lab）
- [ ] 单元测试覆盖 9 种对齐组合（3 horizontal × 3 vertical）
- [ ] 生成的 JSON 符合 `TextStylesSchema` 验证
- [ ] 警告日志正确记录不支持的颜色空间
- [ ] 集成测试：解析包含文本的 PSD → JSON 包含 textStyles 字段

**回归风险评估：**
- **影响范围：** `psd-parser.ts` 的 `convertLayer()` 方法
- **风险：** 中，新增代码路径可能影响解析性能
- **缓解措施：** 仅在 `layer.type === 'text'` 时执行；运行现有解析器测试套件；性能测试对比

---

### 切片 3：文本基础属性应用（Unity）

**依赖：** 切片 2  
**风险等级：** 中  
**涉及文件：**
- `UnityProject/Assets/Change/Editor/PSD2UI/TextStyleApplier.cs`（新建）
- `UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs`
- `UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/TextStyleApplierTests.cs`（新建）

**内容：** 创建 `TextStyleApplier` 类，读取 JSON 的 textStyles 基础属性，设置 TMP 组件（fontSize, color, alignment, fontStyle）。实现字体匹配和锁定检测逻辑。

**接口契约：**

```csharp
public class TextStyleApplier
{
    public void ApplyBasicProperties(TextMeshProUGUI tmpComponent, TextStylesData textStyles, string layerName);
    private TMP_FontAsset FindFont(string fontName);
    private TextAlignmentOptions MapAlignment(string horizontal, string vertical);
    private bool IsLocked(GameObject gameObject);
}
```

**数据契约：**
- **输入：** JSON 反序列化后的 `TextStylesData` 结构
- **输出：** 配置好的 `TextMeshProUGUI` 组件
- **映射规则：**
  - fontSize → `tmpComponent.fontSize`
  - color → `tmpComponent.color`
  - fontStyle.bold → `tmpComponent.fontStyle = FontStyles.Bold`
  - alignment → `TextAlignmentOptions` 枚举（9 种组合）

**行为契约：**
- 字体匹配失败 → 使用 TMP Settings 默认字体，记录警告
- 检测到 `PSD2UILock.LockComponents = true` → 跳过所有设置，记录日志
- `textStyles` 为 null → 使用 TMP 默认值（不报错）

**验收标准：**
- [ ] 单元测试验证 9 种对齐映射（TopLeft, TopCenter, ..., BottomRight）
- [ ] 单元测试验证字体匹配逻辑（精确匹配、默认回退）
- [ ] 单元测试验证锁定组件被跳过
- [ ] 集成测试：生成的 Prefab 中 TMP 组件属性与 JSON 一致
- [ ] 端到端测试：解析 PSD → Unity 中文本显示正确字号、颜色、对齐

**回归风险评估：**
- **影响范围：** `ComponentFactory.CreateComponent()` 方法
- **风险：** 中，修改组件创建流程
- **缓解措施：** `textStyles` 参数为可选，现有调用不传参则行为不变；运行现有 PrefabBuilder 测试套件

---

### 切片 4：6 种效果提取与应用

**依赖：** 切片 3  
**风险等级：** 高  
**涉及文件：**
- `PSDExporterProject/src/parser/text-style-extractor.ts`（扩展）
- `PSDExporterProject/src/generator/json-schema.ts`（扩展）
- `UnityProject/Assets/Change/Editor/PSD2UI/TextStyleApplier.cs`（扩展）
- `UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/TextStyleApplierTests.cs`（扩展）

**内容：** 扩展 `TextStyleExtractor` 提取 6 种效果参数，实现渐变降级策略。扩展 `TextStyleApplier` 创建 Material 并设置 Shader 参数，实现多效果叠加。生成更新报告。

**接口契约（Node.js）：**

```typescript
export class TextStyleExtractor {
  extractEffects(effects: LayerEffectsInfo): { effects: TextEffect[]; warnings: string[] };
  private degradeGradient(gradient: LayerEffectGradientOverlay): { degraded: GradientEffect; warning?: string };
}
```

**接口契约（Unity）：**

```csharp
public class TextStyleApplier
{
    public void ApplyEffects(TextMeshProUGUI tmpComponent, TextEffectData[] effects, string layerName, string layerId);
    private Material CreateMaterial(string layerName, string layerId);
    private void ApplyStroke(Material mat, StrokeEffectData stroke);
    private void ApplyDropShadow(Material mat, ShadowEffectData shadow);
    private void ApplyInnerShadow(Material mat, ShadowEffectData innerShadow);
    private void ApplyGradient(Material mat, TextMeshProUGUI tmp, GradientEffectData gradient);
    private void ApplyOuterGlow(Material mat, GlowEffectData glow);
    private void ApplyBevel(Material mat, BevelEffectData bevel);
}
```

**数据契约：**

**Shader 参数映射表：**

| PSD 效果 | JSON 字段 | Shader 参数 | 默认值 |
|---------|----------|------------|--------|
| 描边 | stroke.width, stroke.color, stroke.position | `_OutlineWidth`, `_OutlineColor`, `_OutlineSoftness` | width=1.0, color=black, softness=0 |
| 阴影 | dropShadow.offsetX/Y, color, blur | `_UnderlayColor`, `_UnderlayOffsetX/Y`, `_UnderlaySoftness` | offset=2/-2, blur=4, color=black@0.5 |
| 内阴影 | innerShadow.offsetX/Y, color | `_UnderlayDilate`=-0.5, `_UnderlayColor`, `_UnderlayOffsetX/Y` | dilate=-0.5, offset=2/-2 |
| 发光 | outerGlow.color, size, spread | `_GlowColor`, `_GlowOffset`, `_GlowOuter`, `_GlowPower` | size=5, spread=0, power=0.75 |
| 渐变 | gradient.angle, colors[] | `_GradientScale`, TMP `colorGradient` | angle=90, colors=2 |
| 斜角 | bevel.size, depth, angle, highlightColor, shadowColor | `_Bevel`, `_BevelWidth`, `_LightAngle`, `_SpecularColor` | size=5, depth=1, angle=120° |

**行为契约：**

**Node.js：**
- 效果未启用（`enabled = false`）→ 不添加到 effects 数组
- 渐变降级规则：
  - 斜向（angle ≠ 0/90/180/270）→ angle > 45 ? 90 : 0，生成警告
  - 径向 → 固定 90°，生成警告
  - 多色（colorStops.length > 2）→ 取首尾颜色，生成警告

**Unity：**
- Material 创建：命名 `TMP_<LayerName>_<LayerID>_Material.mat`，保存到 `Assets/GameRes/Materials/UI/PSD2UI/`
- 多效果叠加：依次设置 Shader 参数，不覆盖
- 内阴影实现：`_UnderlayDilate = -0.5`，同时设置偏移和颜色

**验收标准：**

**Node.js：**
- [ ] 单元测试验证 6 种效果的参数提取
- [ ] 单元测试验证渐变降级规则（斜向 45°→90°、径向→90°、三色→双色）
- [ ] 集成测试：JSON 包含 effects 数组和 warnings 数组
- [ ] 性能测试：50 个文本图层（含效果）< 10 秒

**Unity：**
- [ ] 单元测试验证 Material 创建和命名规则
- [ ] 单元测试验证 6 种效果的 Shader 参数映射
- [ ] 单元测试验证多效果叠加（描边+阴影、描边+发光、描边+阴影+发光）
- [ ] 视觉验证测试：生成包含 6 种效果的测试 PSD → Unity 中视觉还原度 > 90%
- [ ] 内阴影视觉验证：`_UnderlayDilate` 负值效果符合预期
- [ ] Bevel 参数验证：对比 Photoshop 和 Unity 渲染效果

**回归风险评估：**
- **影响范围：** `TextStyleExtractor`、`TextStyleApplier`、Material 资产管理
- **风险：** 高，新增大量 Material 资产，可能影响项目资源管理和打包体积
- **缓解措施：**
  - Material 保存到独立目录 `Assets/GameRes/Materials/UI/PSD2UI/`
  - 在 plan 阶段添加 Material 清理工具任务（删除未使用的 Material）
  - 文档说明 Material 管理策略（命名规则、存储位置、清理方式）
  - 性能测试对比（生成前后的打包体积、加载时间）

## 切片依赖图

```
切片 1（JSON Schema）
    ↓
切片 2（Node.js 基础属性提取）
    ↓
切片 3（Unity 基础属性应用）
    ↓
切片 4（6 种效果提取与应用）← 高风险，需视觉验证
```

**实施顺序：** 严格按 1 → 2 → 3 → 4 顺序执行，每个切片完成并验收后再开始下一个。

## 关键接口

### 切片 1 暴露给切片 2

```typescript
// TypeScript 类型定义
export interface TextStyles {
  fontSize: number;
  color: { r: number; g: number; b: number; a: number };
  fontName: string;
  fontStyle: { bold: boolean; italic: boolean };
  alignment: { horizontal: string; vertical: string };
  effects?: TextEffect[];
}
```

### 切片 2 暴露给切片 3

```json
// JSON 输出格式
{
  "layers": [{
    "id": "layer_005",
    "name": "txt_title",
    "type": "text",
    "textStyles": {
      "fontSize": 36,
      "color": { "r": 1.0, "g": 1.0, "b": 1.0, "a": 1.0 },
      "fontName": "Arial",
      "fontStyle": { "bold": true, "italic": false },
      "alignment": { "horizontal": "center", "vertical": "middle" },
      "effects": []
    }
  }]
}
```

### 切片 3 暴露给切片 4

```csharp
// Unity C# 接口
public void ApplyBasicProperties(
    TextMeshProUGUI tmpComponent,
    TextStylesData textStyles,
    string layerName
);
```

## 回归风险评估

| 影响范围 | 风险等级 | 缓解措施 |
|---------|---------|----------|
| JSON Schema 定义 | 低 | 新增可选字段，运行现有 Schema 测试 |
| PSD 解析性能 | 中 | 仅文本图层执行提取，性能测试对比 |
| Unity 组件创建流程 | 中 | textStyles 参数可选，运行现有 PrefabBuilder 测试 |
| Material 资产数量 | 高 | 独立目录管理，提供清理工具，性能测试 |
| TMP 字体依赖 | 中 | 默认回退机制，警告日志，文档说明 |
| Shader 参数冲突 | 中 | 多效果叠加测试，确保参数不互相覆盖 |
| 打包体积 | 中 | Material 复用策略（后续优化），清理未使用资产 |

## 待确定事项（在 plan 阶段解决）

1. **字号 DPI 缩放因子具体值**：通过实际测试 PSD 和 Unity 对比确定（初始值 1.0）
2. **TMP 默认字体具体资产**：检查项目 TMP Settings，确定默认字体路径
3. **内阴影 `_UnderlayDilate` 具体值**：通过视觉测试确定最佳负值（初始值 -0.5）
4. **Material 清理工具实现细节**：在 plan 阶段设计清理逻辑（扫描 Prefab 引用，删除未使用 Material）

## 下一步

设计文档完成后，调用 `/mz-plan` 生成详细实现计划，包括：
- 每个切片的分步实现任务（2-5 分钟粒度）
- 单元测试和集成测试任务
- 性能测试和视觉验证任务
- Material 清理工具实现任务
