# PSD 文字样式同步与对齐支持 功能需求文档

> 日期: 2026-06-29 | 状态: 草稿
> 父功能: PSD 转 UGUI 自动化工具链 ([README](./README.md))
> 依赖: [PSD 解析与智能分析管线](./2026-06-24-psd-2-ui-parser-frd.md) (FRD #1)、[Unity Prefab 生成与代码框架](./2026-06-24-psd-2-ui-generator-frd.md) (FRD #3)

## 概述

增强 PSD 解析器和 Unity 生成器，支持从 Photoshop 文本图层提取 6 种视觉效果（描边、阴影、内阴影、渐变、发光、斜角）和文本对齐信息，并自动映射到 Unity TextMeshPro 组件和项目现有的 `TMP_SDF.shader` 参数，显著提升 UI 文字的视觉还原度。同时完善迭代维护机制，通过锁定标记保护手动调整。

## 用户故事

| 角色 | 场景 | 目标 |
|------|------|------|
| 程序员 | 拿到美术提供的 PSD，文本有描边、阴影、发光效果 | 运行工具后，Unity 中的 TMP 组件自动应用这些效果，视觉还原度 > 90%，无需手动逐个调整 |
| 程序员 | 在 Unity 中手动调整了某个标题的描边颜色以适配夜间模式 | 给该文本挂载 `PSD2UILock` 组件，美术更新 PSD 后重新生成时，手动调整保持不变 |
| 程序员 | PSD 中的文本使用了复杂的斜向渐变 | 工具生成警告日志，自动降级为垂直渐变，程序员可以选择在 Unity 中手动切换到自定义 Shader |
| 美术 | 在 PSD 中调整了按钮文字的对齐方式（从左对齐改为居中） | 程序员重新生成后，Unity 中的按钮文字自动变为居中对齐 |
| 程序员 | 查看解析日志，发现某个文本效果参数异常 | 日志清晰指出问题节点、效果类型、参数值、降级策略，快速定位并手动修正 |

## 功能边界

**范围内：**
- PSD 文本图层效果解析（Node.js 解析器扩展）：
  - 描边（Stroke）：颜色、宽度、位置（外描边/内描边/居中）
  - 阴影（Drop Shadow）：颜色、偏移 (x, y)、模糊半径、透明度
  - 内阴影（Inner Shadow）：颜色、偏移、模糊半径、透明度
  - 渐变（Gradient Overlay）：类型（线性/径向）、起止颜色、角度（仅支持垂直/水平，其他降级）
  - 发光（Outer Glow）：颜色、扩散半径、透明度
  - 斜角（Bevel & Emboss）：样式、深度、大小、角度、高光颜色、阴影颜色
- 文本基础属性解析：字号、颜色、字体名称、粗体、斜体
- 文本对齐解析：段落对齐（左/中/右/两端）、垂直对齐（上/中/下）
- JSON Schema 扩展：在现有 JSON 配置中增加 `textStyles` 字段，包含所有效果参数
- Unity TMP 样式应用（Unity 生成器扩展）：
  - 映射到项目现有 `TMP_SDF.shader` 参数（`_OutlineWidth`, `_OutlineColor`, `_GlowColor`, `_UnderlayOffsetX` 等）
  - 映射到 TMP 组件的对齐属性（9 种组合：TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight）
  - 自动配置 Material 以支持多效果叠加（描边+阴影+发光同时启用）
- 字体匹配：优先精确匹配字体名，失败时回退到默认字体并生成警告
- 降级策略：
  - 复杂渐变（斜向、径向、多色停点 > 2）→ 降级为最接近的垂直/水平渐变，生成警告
  - 不支持的效果参数（如图案叠加）→ 忽略并生成警告
- 迭代维护机制：
  - 检测 `PSD2UILock` 组件，锁定的文本对象完全跳过样式更新
  - 未锁定的文本对象完全覆盖为 PSD 最新样式
  - 生成更新报告：记录更新/跳过的对象数量、效果类型、警告信息
- 命令行接口扩展：`psd-exporter parse <input.psd> --with-text-styles`

**范围外：**
- 不支持 Photoshop 的特殊文字效果（图案叠加 Pattern Overlay、光泽 Satin、缎面等）
- 不支持文字动画（仅静态样式）
- 不支持自定义字体自动导入到 Unity（假设字体已在项目中）
- 不支持文字变形（弧形、波浪、透视等 Warp 效果）
- 不处理富文本标签（`<b>`, `<color>`, `<size>` 等，仅解析图层级样式）
- 不支持 UI Toolkit（仅 uGUI + TextMeshPro）
- 不实现资源复用（ref/refp 功能）
- 不实现图片对半、图片效果（镜像、旋转等）
- 不提供样式库管理（不创建预设 Material 库）
- 不支持运行时动态修改样式（仅编辑器工具）

## 验收条件

### Phase 1：PSD 文本样式解析（Node.js）

**基础属性解析：**
- [ ] 正确提取文本图层的字号、颜色（RGBA）、字体名称
- [ ] 正确提取粗体、斜体状态
- [ ] 正确提取段落对齐（左/中/右/两端）和垂直对齐（上/中/下）

**6 种效果解析：**
- [ ] 描边（Stroke）：提取颜色、宽度、位置（外/内/居中），写入 JSON
- [ ] 阴影（Drop Shadow）：提取颜色、偏移 (x, y)、模糊半径、透明度
- [ ] 内阴影（Inner Shadow）：提取颜色、偏移、模糊半径、透明度
- [ ] 渐变（Gradient）：提取类型、起止颜色、角度，判断是否为垂直/水平
- [ ] 发光（Outer Glow）：提取颜色、扩散半径、透明度
- [ ] 斜角（Bevel）：提取样式、深度、大小、角度、高光/阴影颜色

**降级与警告：**
- [ ] 复杂渐变（斜向/径向）自动降级为最接近的垂直/水平方向，生成警告日志
- [ ] 多色停点 > 2 的渐变降级为双色渐变（取首尾颜色），生成警告
- [ ] 不支持的效果（图案叠加等）忽略并生成警告
- [ ] JSON 中包含 `warnings` 字段，记录所有降级和忽略的信息

**JSON Schema 验证：**
- [ ] `textStyles` 字段符合预定义的 JSON Schema
- [ ] 包含所有必需字段：`fontSize`, `color`, `fontName`, `alignment`, `effects`
- [ ] `effects` 数组包含启用的效果及其参数

### Phase 2：Unity TMP 样式应用

**对齐应用：**
- [ ] 根据 JSON 配置正确设置 TMP 组件的 `alignment` 属性（9 种组合）
- [ ] 两端对齐（Justified）映射到 TMP 的对应模式

**Shader 参数映射：**
- [ ] 描边 → 设置 `_OutlineWidth`, `_OutlineColor`, `_OutlineSoftness`
- [ ] 阴影 → 设置 `_UnderlayColor`, `_UnderlayOffsetX`, `_UnderlayOffsetY`, `_UnderlaySoftness`
- [ ] 内阴影 → 设置 `_UnderlayDilate` 为负值（实现内阴影效果）
- [ ] 发光 → 设置 `_GlowColor`, `_GlowOffset`, `_GlowInner`, `_GlowOuter`, `_GlowPower`
- [ ] 渐变 → 设置 `_GradientScale` 和 TMP 的 `colorGradient` 属性
- [ ] 斜角 → 设置 `_Bevel`, `_BevelOffset`, `_BevelWidth`, `_BevelRoundness`, `_LightAngle`, `_SpecularColor`

**Material 配置：**
- [ ] 自动选择或创建合适的 Material（使用项目的 `TMP_SDF.shader`）
- [ ] 多效果叠加时，Material 参数正确组合（不互相覆盖），例如：描边+阴影同时可见、描边+发光同时生效
- [ ] Material 命名规则：`TMP_<LayerName>_<LayerID>_Material`，避免重名冲突

**字体匹配：**
- [ ] 优先精确匹配 JSON 中的 `fontName` 到 Unity 项目中的 TMP 字体资产
- [ ] 匹配失败时，回退到项目默认 TMP 字体（检查 `TMP Settings`）
- [ ] 生成警告日志：记录未找到的字体名称、使用的回退字体

**基础属性应用：**
- [ ] 正确设置 `fontSize`（转换公式在 design 阶段确定，参考未决问题 #6）
- [ ] 正确设置 `color`（RGBA 颜色）
- [ ] 正确设置 `fontStyle`（粗体、斜体）

### Phase 3：迭代维护机制

**锁定检测：**
- [ ] 扫描 Prefab 中的所有 TextMeshProUGUI 组件，检测是否挂载 `PSD2UILock` 组件
- [ ] `PSD2UILock.lockComponents = true` 时，跳过该文本对象的所有样式更新
- [ ] 生成日志：记录哪些对象被锁定、锁定原因（读取 `PSD2UILock.notes` 字段）

**增量更新：**
- [ ] 对比新旧 JSON 配置，识别文本样式的变化（字号、颜色、效果参数）
- [ ] 只更新变化的样式属性，未变化的保持不变（减少不必要的序列化变更）
- [ ] 完全覆盖模式：未锁定的对象，所有样式属性完全同步为 PSD 最新值

**更新报告：**
- [ ] 生成 Markdown 格式的更新报告：`PSD2UI_UpdateReport_<Timestamp>.md`
- [ ] 包含以下内容：
  - 更新的文本对象数量、名称、变化的属性
  - 跳过的锁定对象数量、名称、锁定原因
  - 警告信息（字体未找到、渐变降级、效果不支持等）
  - 统计信息（总文本数、更新成功数、跳过数、警告数）

### Phase 4：端到端测试

**视觉还原测试：**
- [ ] 使用包含 6 种效果的真实 PSD 测试（每种效果至少 2 个样本）
- [ ] Unity 中的 TMP 组件视觉还原度 > 90%（通过截图对比验证）
- [ ] 多效果叠加测试：描边+阴影、描边+发光、描边+阴影+发光等组合

**降级策略测试：**
- [ ] 斜向渐变（45°）→ 降级为垂直/水平渐变，生成警告
- [ ] 径向渐变 → 降级为垂直渐变，生成警告
- [ ] 三色渐变 → 降级为双色渐变（首尾颜色），生成警告

**迭代维护测试：**
- [ ] 生成 Prefab 后，手动修改某个文本的描边颜色，挂载 `PSD2UILock`
- [ ] 修改 PSD 中该文本的描边宽度，重新生成
- [ ] 验证：Unity 中的描边颜色保持手动修改值，描边宽度未更新
- [ ] 验证：更新报告中记录该对象被跳过

**性能测试：**
- [ ] 包含 50+ 文本对象（含多种效果）的 PSD 解析时间 < 10 秒
- [ ] Unity 生成器应用样式时间 < 5 秒
- [ ] 生成的 Material 数量合理（尽量复用相同参数组合的 Material）

**边界情况测试：**
- [ ] 文本图层未应用任何效果 → 仅设置基础属性（字号、颜色、对齐）
- [ ] 文本图层字体在 Unity 中不存在 → 使用默认字体，生成警告
- [ ] 文本图层效果参数极端值（如描边宽度 = 0）→ 正确处理，不报错

## Decisions

| # | 决策点 | 选择 | 理由 | 影响范围 |
|---|--------|------|------|----------|
| 1 | 文字效果支持范围 | 6 种效果：描边、阴影、内阴影、渐变、发光、斜角 | 覆盖 80% 常见需求，项目已有完整 TMP Shader 支持 | 解析器、生成器、测试 |
| 2 | 渐变支持策略 | 仅支持垂直/水平线性渐变，其他降级 | 简化实现，复杂渐变使用频率低，可手动调整 | 解析器降级逻辑、警告生成 |
| 3 | Shader 选择 | 使用项目现有 `TMP_SDF.shader` 及其变体 | 已验证稳定，支持所有 6 种效果，无需额外开发 | 生成器 Material 配置 |
| 4 | 文本对齐映射 | 完全映射 9 种组合（水平 × 垂直） | PSD 文本框已包含完整对齐信息，TMP 原生支持 | 解析器、生成器 |
| 5 | 迭代维护机制 | 锁定标记（复用 `PSD2UILock`） | 程序员明确控制保护范围，不会误覆盖 | 生成器增量更新逻辑 |
| 6 | 字体匹配策略 | 精确匹配优先，失败回退到默认字体 | 不自动导入字体（避免版权和项目混乱），程序员手动管理字体库 | 生成器字体查找逻辑 |
| 7 | Material 管理 | 每个文本图层生成独立 Material，命名规则：`TMP_<LayerName>_<LayerID>_Material` | 避免参数冲突和重名，便于程序员单独调整 | 生成器 Material 创建逻辑 |
| 8 | JSON Schema 扩展 | 在现有 `layers` 数组元素中增加 `textStyles` 字段 | 保持数据结构一致性，便于生成器遍历 | 解析器输出、生成器输入 |
| 9 | 降级警告记录 | JSON 中增加 `warnings` 数组，Unity 生成日志文件 | 双重记录，便于调试和审查 | 解析器、生成器、测试 |
| 10 | 更新报告格式 | Markdown 格式，包含统计信息和详细列表 | 易读易查，可纳入版本控制 | 生成器更新报告生成逻辑 |

## 未决问题

| # | 问题 | 建议解决阶段 |
|---|------|-------------|
| 1 | ag-psd 库是否完整支持所有 6 种效果的参数提取？特别是 Bevel & Emboss 的细节参数 | research - 需要验证 ag-psd API 覆盖度，可能需要补充原始 PSD 二进制解析 |
| 2 | 项目的 TMP 默认字体是哪个？是否有字体命名映射表（如 PSD 中的"Arial"映射到 Unity 中的"Arial-TMP"） | research - 需要检查项目 TMP Settings 和现有字体资产 |
| 3 | Material 是否需要复用？相同参数组合的文本是否共享 Material 以减少资源数量 | design - 权衡资源管理复杂度和性能优化 |
| 4 | 内阴影通过 `_UnderlayDilate` 负值实现是否足够？是否需要额外的 Shader 变体 | explore - 需要测试视觉效果是否满足需求 |
| 5 | 斜角效果的参数映射是否完整？`_Bevel` 系列参数是否能覆盖 Photoshop 的所有斜角样式（外斜角、内斜角、浮雕等） | explore - 需要对比 Photoshop 和 TMP Shader 的参数语义 |
| 6 | 字号的 DPI 转换比例是多少？PSD 的 72 DPI 如何映射到 Unity 的屏幕空间字号 | design - 需要确定转换公式，可能需要可配置 |

## 约束与假设

**技术约束：**
- Node.js 版本 ≥ 18.x（解析器运行环境）
- Unity 版本 ≥ 2021.3 LTS
- TextMeshPro 已集成到项目（版本 ≥ 3.0）
- 项目使用 uGUI（非 UI Toolkit）
- 依赖 ag-psd 库版本 ≥ 14.x（支持图层效果解析）

**开发约束：**
- 解析器代码在 `PSDExporterProject/src/` 下扩展
- Unity 插件代码在 `UnityProject/Assets/Change/Editor/PSD2UI/` 下扩展
- 需要更新现有的 JSON Schema 定义和验证规则

**规范约束：**
- 美术提供的 PSD 必须先通过 PSD2UIForm 脚本优化导出
- 文本必须使用 Photoshop 文本图层（非栅格化）
- 推荐美术遵循有限效果集（最多一层描边 + 一个阴影/内阴影 + 一个发光/渐变）

**假设：**
- 项目已有 TMP 字体资产，程序员负责字体库管理
- 项目的 `TMP_SDF.shader` 参数命名和语义与标准 TMP Shader 一致
- 程序员理解锁定机制的使用场景（夜间模式适配、特殊屏幕适配等）
- 美术更新 PSD 时，不会大幅修改文本图层的命名和层级结构（否则增量更新可能失效）
- 生成的 Material 资产会被纳入版本控制

## 技术选型摘要

**解析器扩展（Node.js）：**
- ag-psd - PSD 图层效果解析
- zod - JSON Schema 校验（扩展现有 Schema）
- color-convert - 颜色格式转换（Photoshop 颜色空间 → Unity RGBA）

**生成器扩展（Unity C#）：**
- Unity Editor API - Material 创建和参数设置
- TextMeshPro API - 组件属性设置
- Newtonsoft.Json 或 System.Text.Json - JSON 解析

**测试工具：**
- Jest - 解析器单元测试
- Unity Test Framework - 生成器单元测试和集成测试
- ImageMagick 或类似工具（可选）- 视觉还原度自动对比测试

## 输出物规范

### JSON Schema 扩展示例

```json
{
  "layers": [
    {
      "id": "layer_005",
      "name": "txt_title",
      "type": "text",
      "component": {
        "type": "Text",
        "confidence": 1.0,
        "source": "tag"
      },
      "bounds": { "x": 100, "y": 50, "width": 300, "height": 60 },
      "textStyles": {
        "fontSize": 36,
        "color": { "r": 1.0, "g": 1.0, "b": 1.0, "a": 1.0 },
        "fontName": "Arial",
        "fontStyle": {
          "bold": true,
          "italic": false
        },
        "alignment": {
          "horizontal": "center",
          "vertical": "middle"
        },
        "effects": [
          {
            "type": "stroke",
            "enabled": true,
            "color": { "r": 0.0, "g": 0.0, "b": 0.0, "a": 1.0 },
            "width": 2.0,
            "position": "outside"
          },
          {
            "type": "dropShadow",
            "enabled": true,
            "color": { "r": 0.0, "g": 0.0, "b": 0.0, "a": 0.5 },
            "offsetX": 2.0,
            "offsetY": -2.0,
            "blur": 4.0
          },
          {
            "type": "gradient",
            "enabled": true,
            "gradientType": "linear",
            "angle": 90,
            "colors": [
              { "r": 1.0, "g": 1.0, "b": 0.0, "a": 1.0, "position": 0.0 },
              { "r": 1.0, "g": 0.5, "b": 0.0, "a": 1.0, "position": 1.0 }
            ],
            "degraded": false
          }
        ]
      },
      "warnings": []
    }
  ]
}
```

### Unity Material 参数设置示例

```csharp
// TextStyleApplier.cs (伪代码)
public void ApplyTextStyles(TextMeshProUGUI tmpComponent, TextStylesData styles)
{
    // 基础属性
    tmpComponent.fontSize = styles.fontSize;
    tmpComponent.color = styles.color;
    tmpComponent.fontStyle = styles.fontStyle.bold ? FontStyles.Bold : FontStyles.Normal;
    tmpComponent.alignment = MapAlignment(styles.alignment);

    // 创建或获取 Material
    var material = GetOrCreateMaterial(tmpComponent, styles);

    // 应用效果参数
    foreach (var effect in styles.effects)
    {
        switch (effect.type)
        {
            case "stroke":
                material.SetFloat("_OutlineWidth", effect.width);
                material.SetColor("_OutlineColor", effect.color);
                break;
            case "dropShadow":
                material.SetColor("_UnderlayColor", effect.color);
                material.SetFloat("_UnderlayOffsetX", effect.offsetX);
                material.SetFloat("_UnderlayOffsetY", effect.offsetY);
                material.SetFloat("_UnderlaySoftness", effect.blur);
                break;
            case "glow":
                material.SetColor("_GlowColor", effect.color);
                material.SetFloat("_GlowOuter", effect.radius);
                break;
            // ... 其他效果
        }
    }

    tmpComponent.fontSharedMaterial = material;
}
```

### 更新报告示例

```markdown
# PSD2UI 更新报告

**生成时间:** 2026-06-29 14:30:00
**PSD 文件:** ShopWindow.psd
**总文本对象数:** 15

## 更新统计

- ✅ 更新成功: 12 个
- 🔒 跳过（锁定）: 2 个
- ⚠️ 警告: 3 个

## 更新详情

### 更新成功的对象

1. **txt_title** (layer_005)
   - 字号: 32 → 36
   - 描边宽度: 1.5 → 2.0

2. **txt_button_confirm** (layer_012)
   - 对齐: 左对齐 → 居中对齐
   - 颜色: #FFFFFF → #FFCC00

... （省略其他）

### 跳过的锁定对象

1. **txt_vip_badge** (layer_008)
   - 锁定原因: 手动调整了描边颜色以适配夜间模式
   - 跳过的更新: 描边颜色、发光半径

2. **txt_level** (layer_013)
   - 锁定原因: 锁定
   - 跳过的更新: 字号、渐变

### 警告信息

1. **txt_gradient_title** (layer_007)
   - ⚠️ 渐变降级: 斜向渐变（45°）降级为垂直渐变

2. **txt_custom_font** (layer_009)
   - ⚠️ 字体未找到: "CustomFont-Bold" 不存在，使用默认字体 "Arial-TMP"

3. **txt_multi_gradient** (layer_011)
   - ⚠️ 渐变简化: 三色渐变降级为双色渐变（#FF0000 → #0000FF）
```

详细实现在 design 和 plan 阶段完善。
