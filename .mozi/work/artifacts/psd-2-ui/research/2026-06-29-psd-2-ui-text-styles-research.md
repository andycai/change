# PSD 文字样式同步与对齐支持 代码调研

> 日期: 2026-06-29
> 范围: PSD 解析器（Node.js）、Unity 生成器（C# Editor）、TMP Shader
> 上游: [PSD 文字样式同步与对齐支持 FRD](../discover/2026-06-29-psd-2-ui-text-styles-frd.md)
> 调研深度: 中等

## Summary

调研发现 ag-psd 库（v30.2.0）提供了完整的 API 支持，可提取所有 6 种文字效果（描边、阴影、内阴影、渐变、发光、斜角）和对齐信息。项目现有的 `TMP_SDF.shader` 包含所有必需的参数，可直接映射 Photoshop 效果。当前代码库中文本样式提取和应用是**零基础**，需要从头实现：解析器未提取任何文本属性，生成器未设置任何 TMP 属性。`PSD2UILock` 组件已实现可直接复用。JSON Schema 扩展点清晰，基于 Zod 库可安全添加 `textStyles` 字段。

## 与 FRD 的映射关系

### 回答的未决问题

| FRD 未决问题 | 调研发现 | 结论 |
|-------------|----------|------|
| #1: ag-psd 库是否完整支持所有 6 种效果的参数提取？ | ag-psd v30.2.0 通过 `LayerEffectsInfo` 接口完整支持：`dropShadow`, `innerShadow`, `outerGlow`, `bevel`, `stroke`, `gradientOverlay` | **完全支持**，无需补充原始 PSD 二进制解析 |
| #2: 项目的 TMP 默认字体是哪个？ | 项目中未找到 TMP Settings 和字体资产 | **需要在 design 阶段确定**：使用 Unity 内置默认字体 or 要求项目先配置 |
| #3: Material 是否需要复用？ | 典型 PSD 包含 10-50 个文本图层，独立 Material 会产生 10-50 个资产 | **可接受**，Unity 项目通常有数百个 Material，建议采用 Decision #7（独立 Material） |
| #4: 内阴影通过 `_UnderlayDilate` 负值实现是否足够？ | `_UnderlayDilate` 参数范围 -1 到 1，负值使 underlay 向内收缩 | **理论可行**，需在 design 阶段测试视觉效果 |
| #5: 斜角效果的参数映射是否完整？ | Shader 包含 `_Bevel`, `_BevelOffset`, `_BevelWidth`, `_BevelRoundness`, `_LightAngle`, `_SpecularColor` 等完整参数 | **可以覆盖 Photoshop 的主要斜角样式**（外斜角、内斜角、浮雕） |
| #6: 字号的 DPI 转换比例是多少？ | 当前代码中未找到字号转换逻辑 | **需在 design 阶段确定公式**（PSD 72 DPI → Unity 屏幕空间，通常需要缩放因子） |

### 支撑的决策

| FRD 决策 | 调研支撑 | 验证结果 |
|---------|----------|----------|
| Decision #1: 支持 6 种效果 | ag-psd 和 TMP_SDF.shader 都完整支持所有 6 种效果的 API 和参数 | **可行** |
| Decision #3: 使用项目现有 TMP_SDF.shader | Shader 包含所有必需参数（描边、阴影、发光、斜角、渐变），无需额外开发 | **可行** |
| Decision #5: 锁定标记（PSD2UILock） | `PSD2UILock.cs` 已实现，包含 `LockComponents`、`Notes` 字段 | **可直接复用** |
| Decision #7: 每个文本图层独立 Material | 资源数量可接受（10-50 个），便于单独调整，符合 Unity 常规实践 | **可行** |
| Decision #8: 在 layers 数组元素中增加 textStyles 字段 | JSON Schema 基于 Zod，扩展点为 `LayerConfigSchema`，影响范围可控 | **可行** |

## Code References

### 核心文件（Node.js 解析器）

| 文件 | 职责 | 与本次需求的关系 |
|------|------|------------------|
| `PSDExporterProject/src/parser/psd-parser.ts` | PSD 文件解析，调用 ag-psd 库 | **需要扩展**：提取 `layer.text` 和 `layer.effects` 属性 |
| `PSDExporterProject/src/parser/layer-tree.ts` | Layer 数据结构定义 | **需要扩展**：添加 `textStyles` 字段到 `Layer` 接口 |
| `PSDExporterProject/src/generator/json-schema.ts` | JSON Schema 定义和验证（Zod） | **需要扩展**：添加 `TextStylesSchema` 和对应的类型 |
| `PSDExporterProject/node_modules/ag-psd/dist/psd.d.ts` | ag-psd 类型定义 | **参考**：`LayerTextData` (第426行)、`LayerEffectsInfo` (第225行) |

### 核心文件（Unity 生成器）

| 文件 | 职责 | 与本次需求的关系 |
|------|------|------------------|
| `UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs` | UI 组件创建工厂 | **需要扩展**：创建 TMP 组件后设置文本属性 |
| `UnityProject/Assets/Change/Editor/PSD2UI/PrefabBuilder.cs` | Prefab 组装器 | **需要扩展**：调用新的文本样式应用模块 |
| `UnityProject/Assets/GameRes/Shader/UI/TMP_SDF.shader` | TMP Distance Field Shader | **参考**：所有效果参数的定义和语义 |
| `UnityProject/Assets/Change/Runtime/PSD2UI/PSD2UILock.cs` | 锁定标记组件 | **复用**：检测 `LockComponents` 字段跳过样式更新 |

### 关键函数/类

| 符号 | 位置 | 作用 |
|------|------|------|
| `PsdParser.convertLayer()` | `psd-parser.ts:91` | 递归转换 ag-psd Layer 到内部 Layer 结构，**需扩展提取 text 和 effects** |
| `ComponentFactory.CreateComponent()` | `ComponentFactory.cs:20` | 创建 TMP 组件，**需扩展设置文本属性** |
| `LayerTextData` | `ag-psd/dist/psd.d.ts:426` | ag-psd 文本数据接口，包含字号、颜色、字体、对齐等 |
| `LayerEffectsInfo` | `ag-psd/dist/psd.d.ts:225` | ag-psd 效果数据接口，包含 6 种效果的完整参数 |
| `PSD2UILock` | `PSD2UILock.cs:10` | 锁定标记组件，**直接复用** |

## Integration Points

### 内部接口

**Node.js 解析器：**
- 输入：ag-psd 的 `Layer.text` 和 `Layer.effects` 属性
- 输出：扩展后的 JSON 配置，每个文本图层包含 `textStyles` 字段
- 数据模型：需要定义 `TextStylesData` 接口（字号、颜色、字体、对齐、效果数组）

**Unity 生成器：**
- 输入：JSON 配置中的 `textStyles` 字段
- 输出：设置好样式的 TextMeshProUGUI 组件和 Material 资产
- 集成点：
  1. `ComponentFactory.CreateComponent()` 调用新的 `TextStyleApplier` 模块
  2. `PrefabBuilder.CreateNode()` 检测 `PSD2UILock` 组件决定是否应用样式
  3. Material 创建和参数设置通过 Unity Editor API

### 外部依赖

**Node.js 解析器：**
- `ag-psd` v30.2.0：PSD 解析，提取文本和效果数据
- `zod`：JSON Schema 校验
- `color-convert`（建议新增）：颜色格式转换（Photoshop RGB → Unity RGBA）

**Unity 生成器：**
- Unity Editor API：`Material.SetFloat()`, `Material.SetColor()`
- TextMeshPro API：`TextMeshProUGUI.fontSize`, `.color`, `.alignment`, `.fontSharedMaterial`
- 项目现有 Shader：`TMP_SDF.shader` 及其变体

### 调用链

**解析器调用链：**
```
CLI parse 命令
  ↓
PsdParser.parse()
  ↓
PsdParser.convertLayer()（递归）
  ↓ [NEW] 检测 node.text
  ↓ [NEW] 提取 LayerTextData（字号、颜色、字体、对齐）
  ↓ [NEW] 提取 LayerEffectsInfo（6 种效果）
  ↓ [NEW] 转换为 textStyles 数据结构
  ↓
JsonGenerator.generate()
  ↓ [NEW] 包含 textStyles 字段的 JSON
```

**生成器调用链：**
```
Unity 菜单命令
  ↓
PrefabBuilder.BuildPrefab()
  ↓
PrefabBuilder.CreateNode()（递归）
  ↓ [NEW] 检测 layer.type === 'text'
  ↓ [NEW] ComponentFactory.CreateComponent("Text")
  ↓ [NEW] 检测 PSD2UILock 组件
  ↓ [NEW] TextStyleApplier.Apply(tmpComponent, textStyles)
      ↓ 设置基础属性（fontSize, color, alignment）
      ↓ 创建或获取 Material
      ↓ 设置 Shader 参数（_OutlineWidth, _GlowColor, etc.）
```

## Architecture Insights

### 现有模式

**解析器模式：**
- **单一职责**：`psd-parser.ts` 负责解析，`json-generator.ts` 负责输出，`asset-exporter.ts` 负责图片导出
- **递归遍历**：`convertLayer()` 递归处理图层树，每个节点独立转换
- **类型安全**：使用 TypeScript 和 Zod Schema 确保数据结构正确性

**生成器模式：**
- **工厂模式**：`ComponentFactory` 根据类型字符串创建组件
- **Builder 模式**：`PrefabBuilder` 递归组装 GameObject 树
- **锁定机制**：通过 `PSD2UILock` 组件标记跳过更新

**建议复用：**
- 文本样式提取模块可以参考 `asset-exporter.ts` 的结构（单一职责、错误处理）
- Material 创建可以参考现有的资产管理模式（命名规则、存储位置）

### 先例参考

**类似功能实现：**
1. **图片资产导出**（`asset-exporter.ts`）：
   - 检测 `layer.type === 'image'` → 提取 `node.canvas` → 导出 PNG
   - **可参考模式**：检测 `layer.type === 'text'` → 提取 `node.text` 和 `node.effects` → 转换为 textStyles

2. **组件识别**（`component-recognizer.ts`）：
   - 解析标签 → AI 识别 → 输出 ComponentInfo
   - **可参考模式**：文本样式解析可以作为独立模块（`text-style-extractor.ts`），返回 `TextStylesData`

3. **增量更新**（`IncrementalUpdater.cs`）：
   - 检测 `PSD2UILock` → 跳过更新
   - **直接复用**：文本样式应用时检测锁定组件

**相关 commit：**
- `87f2e95`：资产导出 CLI 选项集成（参考命令行接口扩展）
- `580b7f4`：组件类型扩展（参考如何添加新的组件类型）
- `8f89826`：增量更新中的锁定机制（参考锁定检测逻辑）

### 风险与建议

| 风险 | 影响 | 建议 |
|------|------|------|
| **ag-psd 颜色空间转换**：Photoshop 可能使用 CMYK、Lab 等颜色空间，Unity TMP 只支持 RGBA | 颜色显示不准确 | 使用 `color-convert` 库统一转换为 RGBA，记录警告日志 |
| **字号 DPI 转换未确定**：PSD 72 DPI 到 Unity 屏幕空间的缩放因子不明确 | 文字大小与设计稿不一致 | design 阶段通过实际测试确定转换公式，可能需要可配置参数 |
| **Material 数量管理**：每个文本独立 Material 可能导致资源过多 | 打包体积增加、资源加载变慢 | 初期采用独立 Material，后续优化时可添加 Material 复用逻辑（相同参数组合共享） |
| **内阴影视觉效果**：通过 `_UnderlayDilate` 负值实现内阴影未经验证 | 视觉还原度不达标 | design 阶段创建测试用例验证，如果效果不佳需要考虑自定义 Shader 变体 |
| **TMP 字体缺失**：项目未配置 TMP Settings 和字体资产 | 生成的文本组件无法显示 | design 阶段明确字体管理策略：(1) 使用 Unity 内置默认字体 (2) 要求项目先配置 TMP |
| **Bevel 参数语义差异**：Photoshop 和 TMP Shader 的斜角参数可能不完全对应 | 斜角效果还原度低 | design 阶段建立完整的参数映射表，对比视觉效果，记录不支持的样式 |
| **JSON Schema 版本兼容性**：添加 `textStyles` 字段可能破坏旧版本解析器 | 旧版本工具无法解析新 JSON | 采用可选字段（`textStyles?: TextStylesData`），旧版本忽略该字段，新版本检测并应用 |

## 调研问题与答案

### 现状调研（Node.js 解析器）

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| Q1 | PSD 解析器的文本图层处理在哪些文件？ | `grep` + 目录结构查看 | `psd-parser.ts`（主解析器）、`layer-tree.ts`（数据结构定义）。当前仅识别类型为 `'text'`，未提取任何属性 |
| Q2 | 当前解析器已经提取了哪些文本属性？ | `grep` 搜索 fontSize/fontName/alignment | **完全没有**提取文本属性，`Layer` 接口中无任何文本相关字段 |
| Q3 | ag-psd 库的版本和文本效果 API 覆盖度如何？ | 查看 package.json + 类型定义文件 | **ag-psd v30.2.0**，完整支持所有 6 种效果：`LayerTextData`（字号、颜色、字体、对齐）、`LayerEffectsInfo`（描边、阴影、内阴影、渐变、发光、斜角） |

### 现状调研（Unity 生成器）

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| Q4 | Unity 生成器的 TMP 组件创建在哪些文件？ | `grep` 搜索 TextMeshPro | `ComponentFactory.cs`（第40行），创建 `TextMeshProUGUI` 组件 |
| Q5 | 当前生成器已经设置了哪些 TMP 属性？ | 阅读 ComponentFactory 和 PrefabBuilder 代码 | **完全没有**设置 TMP 属性，只创建组件未配置任何属性 |
| Q6 | 项目的 TMP 默认字体是什么？ | 查找 TMP Settings 和字体资产 | **未找到**，项目可能尚未完整配置 TMP，需在 design 阶段确定字体策略 |

### 模式调研

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| Q7 | 项目中是否有类似的"样式应用"逻辑可以参考？ | `grep` 搜索 Material 创建和参数设置 | 找到 `asset-exporter.ts`（图片导出）和 `component-recognizer.ts`（组件识别）模式，可参考单一职责和错误处理方式 |
| Q8 | 项目中 Material 的命名和管理规则是什么？ | 查看现有 Material 资产 | 未找到现有 Material 资产参考，建议命名规则：`TMP_<LayerName>_<LayerID>_Material` |

### 依赖调研

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| Q9 | TMP_SDF.shader 的所有参数及其语义是什么？ | 阅读 shader 文件，提取属性定义 | **完整支持所有 6 种效果**：描边（`_OutlineWidth`, `_OutlineColor`）、阴影（`_UnderlayColor`, `_UnderlayOffsetX/Y`）、发光（`_GlowColor`, `_GlowOuter`）、斜角（`_Bevel`, `_BevelWidth`）、渐变（`_GradientScale`） |
| Q10 | 内阴影通过 `_UnderlayDilate` 负值实现是否可行？ | 分析 shader 代码中 `_UnderlayDilate` 使用逻辑 | **理论可行**，参数范围 -1 到 1，负值使 underlay 向内收缩，需 design 阶段测试 |
| Q11 | 斜角效果的参数能覆盖哪些 Photoshop 样式？ | 对比 shader 参数和 Photoshop Bevel 选项 | **可以覆盖主要样式**：外斜角、内斜角、浮雕，包含深度、宽度、圆度、光照角度、高光颜色等完整参数 |
| Q12 | 字号如何在 PSD 和 Unity 之间转换？ | 搜索现有代码中的字号处理逻辑 | **未找到**现有转换逻辑，需 design 阶段确定公式（PSD 72 DPI → Unity 屏幕空间） |

### 风险调研

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| Q13 | 每个文本图层独立 Material 会产生多少资源？ | 分析典型 PSD 的文本图层数量 | 典型 PSD 包含 10-50 个文本图层，产生 10-50 个 Material 资产，**数量可接受**（Unity 项目通常有数百个 Material） |
| Q14 | 锁定机制（PSD2UILock）是否已实现？ | 搜索 PSD2UILock 组件代码 | **已实现**，位于 `PSD2UILock.cs`，包含 `LockComponents`、`LockTransform`、`Notes` 字段，**可直接复用** |
| Q15 | 现有 JSON Schema 的扩展点在哪？ | 查看 JSON Schema 定义和验证逻辑 | 定义在 `json-schema.ts`，使用 Zod 库，扩展点为 `LayerConfigSchema`，添加可选字段 `textStyles: TextStylesSchema.optional()`，影响范围可控 |

## 实施建议

基于调研发现，建议下一步进入 `/mz-design` 阶段，重点设计以下模块：

1. **文本样式提取器**（Node.js）：
   - 从 ag-psd 的 `LayerTextData` 和 `LayerEffectsInfo` 提取数据
   - 颜色空间转换（CMYK/Lab → RGBA）
   - 渐变降级策略（斜向/径向 → 垂直/水平）
   - 警告日志生成

2. **JSON Schema 扩展**：
   - 定义 `TextStylesSchema`（字号、颜色、字体、对齐、效果数组）
   - 定义 6 种效果的 Schema（EffectStroke, EffectShadow, etc.）
   - 版本兼容性策略（可选字段）

3. **文本样式应用器**（Unity C#）：
   - TMP 组件属性设置（fontSize, color, alignment, fontStyle）
   - 字体匹配逻辑（精确匹配 + 默认回退）
   - Material 创建和参数映射
   - Shader 参数设置（6 种效果的完整映射表）
   - 锁定检测和跳过逻辑

4. **字号转换公式**：
   - 通过实际测试确定 PSD → Unity 的缩放因子
   - 考虑可配置参数（不同项目可能有不同的设计稿分辨率）

5. **测试策略**：
   - 创建包含所有 6 种效果的测试 PSD
   - 验证内阴影的视觉效果（`_UnderlayDilate` 负值）
   - 验证 Bevel 参数映射的准确性
   - 测试字体匹配和回退逻辑
