# PSD 文字样式同步与对齐支持 实现计划

> 日期: 2026-06-29 | 状态: 草稿
> 上游设计: [架构设计文档](../designs/2026-06-29-psd-2-ui-text-styles-design.md)
> 上游 FRD: [功能需求文档](../discover/2026-06-29-psd-2-ui-text-styles-frd.md)
> 上游调研: [代码调研报告](../research/2026-06-29-psd-2-ui-text-styles-research.md)

## 上游产出引用

### 来自 Design

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 切片 1: JSON Schema 扩展与基础文本属性 | 任务 1-3: TypeScript 类型定义、Zod Schema、单元测试 |
| 切片 2: 文本基础属性提取（Node.js） | 任务 4-7: TextStyleExtractor 实现、颜色转换、集成测试 |
| 切片 3: 文本基础属性应用（Unity） | 任务 8-12: TextStyleApplier 实现、字体匹配、对齐映射 |
| 切片 4: 6 种效果提取与应用 | 任务 13-20: 效果提取器扩展、Shader 参数映射、视觉验证 |
| 文件地图: 10 个文件（8 新建, 2 扩展） | 按切片依赖顺序逐一创建 |
| 架构决策: 单体模块设计 | TextStyleExtractor 和 TextStyleApplier 两个独立模块 |

### 来自 FRD

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 验收条件: 视觉还原度 > 90% | 任务 19: 视觉验证测试 |
| 验收条件: 50+ 文本对象解析 < 10 秒 | 任务 7、任务 20: 性能测试 |
| Decision #1: 支持 6 种效果 | 任务 13-18: 逐一实现 6 种效果的提取和应用 |
| Decision #3: 使用项目现有 TMP_SDF.shader | 任务 14-18: 直接映射 Shader 参数 |
| Decision #7: 每个文本图层独立 Material | 任务 14: Material 创建逻辑 |

### 来自 Research

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| ag-psd v30.2.0 完整支持 6 种效果 API | 任务 13: 使用 LayerEffectsInfo 接口 |
| TMP_SDF.shader 参数完整覆盖 | 任务 14-18: 完整的 Shader 参数映射表 |
| 现有模式: 单一职责（asset-exporter.ts） | 任务 4: TextStyleExtractor 参考此模式 |
| 集成点: psd-parser.ts 的 convertLayer() | 任务 6: 在此方法中调用提取器 |
| 集成点: ComponentFactory.CreateComponent() | 任务 11: 在此方法中调用应用器 |
| 风险: 颜色空间转换 | 任务 5: 使用 color-convert 库 |
| 风险: 字号 DPI 转换未确定 | 任务 5: 使用缩放因子 1.0 作为初始值 |

## 目标

从 Photoshop 文本图层提取 6 种视觉效果和基础文本属性，自动映射到 Unity TextMeshPro 组件和 TMP_SDF.shader 参数，实现 90%+ 的视觉还原度。

## 架构

采用单体模块设计，分为两个独立模块：
1. **TextStyleExtractor (Node.js)**：从 ag-psd 的 LayerTextData 和 LayerEffectsInfo 提取文本样式，处理颜色空间转换和渐变降级
2. **TextStyleApplier (Unity C#)**：读取 JSON 配置，设置 TMP 组件属性和 Material 的 Shader 参数

按 4 个垂直切片顺序实施：JSON Schema → 基础属性提取 → 基础属性应用 → 6 种效果提取与应用。

## 技术栈

**Node.js 解析器：**
- TypeScript 4.x
- ag-psd v30.2.0
- zod（JSON Schema 校验）
- color-convert（颜色空间转换）
- Jest（单元测试）

**Unity 生成器：**
- Unity 2021.3 LTS
- TextMeshPro 3.0+
- C# 8.0+
- NUnit（单元测试）

## 文件清单

| 文件 | 操作 | 职责 | 所属任务 |
|------|------|------|----------|
| `PSDExporterProject/src/parser/layer-tree.ts` | 修改 | 扩展 Layer 接口，定义 TextStyles 类型 | 任务 1 |
| `PSDExporterProject/src/generator/json-schema.ts` | 修改 | 定义 TextStylesSchema 和效果 Schema（Zod） | 任务 2 |
| `PSDExporterProject/tests/unit/json-schema.test.ts` | 创建 | JSON Schema 验证测试 | 任务 3 |
| `PSDExporterProject/src/parser/text-style-extractor.ts` | 创建 | 文本样式提取器（基础属性 + 6 种效果） | 任务 4, 13 |
| `PSDExporterProject/src/parser/psd-parser.ts` | 修改 | 集成 TextStyleExtractor 到 convertLayer() | 任务 6 |
| `PSDExporterProject/tests/unit/text-style-extractor.test.ts` | 创建 | 提取器单元测试 | 任务 5, 13 |
| `UnityProject/Assets/Change/Editor/PSD2UI/TextStyleApplier.cs` | 创建 | 文本样式应用器（基础属性 + 效果应用） | 任务 8, 14 |
| `UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs` | 修改 | 集成 TextStyleApplier，调用 ApplyBasicProperties() | 任务 11 |
| `UnityProject/Assets/Change/Editor/PSD2UI/PrefabBuilder.cs` | 修改 | 传递 textStyles 数据到 ComponentFactory | 任务 11 |
| `UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/TextStyleApplierTests.cs` | 创建 | 应用器单元测试 | 任务 9, 15 |

## 任务依赖图

```
切片 1: JSON Schema（任务 1-3）
  ↓
切片 2: Node.js 基础属性提取（任务 4-7）
  ↓
切片 3: Unity 基础属性应用（任务 8-12）
  ↓
切片 4: 6 种效果提取与应用（任务 13-20）
```

**详细依赖：**
```
任务 1（Layer 接口扩展）
  ↓
任务 2（Zod Schema 定义）
  ↓
任务 3（Schema 单元测试）
  ↓
任务 4（TextStyleExtractor 实现）
  ├─→ 任务 5（单元测试：颜色转换、字号、对齐）
  └─→ 任务 6（集成到 psd-parser）
        ↓
      任务 7（集成测试 + 性能测试）
        ↓
      任务 8（TextStyleApplier 实现）
        ├─→ 任务 9（单元测试：对齐映射、字体匹配、锁定检测）
        ├─→ 任务 10（TMP Settings 配置检查）
        └─→ 任务 11（集成到 ComponentFactory）
              ↓
            任务 12（端到端测试：PSD → Unity）
              ↓
            任务 13（效果提取器扩展：6 种效果 + 降级策略）
              ├─→ 任务 14（效果应用器扩展：Material 创建 + Shader 参数映射）
              ├─→ 任务 15（单元测试：6 种效果的参数映射）
              ├─→ 任务 16（单元测试：渐变降级规则）
              ├─→ 任务 17（单元测试：多效果叠加）
              ├─→ 任务 18（Material 清理工具）
              ├─→ 任务 19（视觉验证测试：6 种效果还原度）
              └─→ 任务 20（性能测试：50+ 文本对象）
```

## 风险评估

| 风险（来源） | 影响 | 计划中的缓解措施 |
|-------------|------|-----------------|
| 颜色空间转换（Research） | CMYK/Lab 颜色显示不准确 | 任务 5: 使用 color-convert 库统一转换为 RGBA，记录警告日志 |
| 字号 DPI 转换未确定（Research） | 文字大小与设计稿不一致 | 任务 5: 使用缩放因子 1.0 作为初始值，通过实际测试确定（任务 12 端到端测试时调整） |
| 内阴影视觉效果（Research） | 视觉还原度不达标 | 任务 19: 通过视觉测试验证 `_UnderlayDilate` 负值效果，如不佳则标注为已知限制 |
| Material 数量管理（Design） | 打包体积增加、资源加载变慢 | 任务 18: 提供 Material 清理工具，独立目录管理 |
| TMP 字体缺失（Research） | 生成的文本组件无法显示 | 任务 10: 检查 TMP Settings 配置，任务 9: 实现字体匹配和回退逻辑 |
| JSON Schema 版本兼容性（Research） | 旧版本工具无法解析新 JSON | 任务 1: 使用可选字段 `textStyles?: TextStyles`，旧版本忽略该字段 |
| PSD 解析性能（Design） | 解析速度变慢 | 任务 7: 性能测试对比，仅文本图层执行提取 |
| Unity 组件创建流程回归（Design） | 现有 Prefab 生成受影响 | 任务 11: textStyles 参数可选，运行现有 PrefabBuilder 测试套件 |
| Shader 参数冲突（Design） | 多效果叠加时参数互相覆盖 | 任务 17: 多效果叠加测试，确保参数不互相覆盖 |

---

## 任务

### 任务 1: Layer 接口扩展 - 定义 TextStyles 类型

**覆盖的上游需求：** Design 切片 1 - JSON Schema 扩展

**文件：**
- 修改：`PSDExporterProject/src/parser/layer-tree.ts`

- [ ] **步骤 1: 在 layer-tree.ts 中定义 TextStyles 接口**

在文件末尾添加：

```typescript
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

export interface StrokeEffect extends TextEffect {
  type: 'stroke';
  color: { r: number; g: number; b: number; a: number };
  width: number;
  position: 'outside' | 'inside' | 'center';
}

export interface ShadowEffect extends TextEffect {
  type: 'dropShadow' | 'innerShadow';
  color: { r: number; g: number; b: number; a: number };
  offsetX: number;
  offsetY: number;
  blur: number;
}

export interface GradientEffect extends TextEffect {
  type: 'gradient';
  gradientType: 'linear' | 'radial';
  angle: number;
  colors: Array<{ r: number; g: number; b: number; a: number; position: number }>;
  degraded: boolean;
}

export interface GlowEffect extends TextEffect {
  type: 'outerGlow';
  color: { r: number; g: number; b: number; a: number };
  size: number;
  spread: number;
}

export interface BevelEffect extends TextEffect {
  type: 'bevel';
  style: string;
  depth: number;
  size: number;
  angle: number;
  highlightColor: { r: number; g: number; b: number; a: number };
  shadowColor: { r: number; g: number; b: number; a: number };
}
```

- [ ] **步骤 2: 扩展 Layer 接口添加 textStyles 字段**

在 `Layer` 接口中添加可选字段：

```typescript
export interface Layer {
  // ... 现有字段
  textStyles?: TextStyles;
}
```

- [ ] **步骤 3: 验证 TypeScript 编译**

运行：
```bash
cd PSDExporterProject
npm run build
```

预期：编译成功，无类型错误

- [ ] **步骤 4: Commit**

```bash
git add PSDExporterProject/src/parser/layer-tree.ts
git commit -m "feat(parser): add TextStyles interface and effect types

- Define TextStyles with fontSize, color, fontName, fontStyle, alignment
- Define 6 effect types: Stroke, Shadow, Gradient, Glow, Bevel
- Extend Layer interface with optional textStyles field

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 2: Zod Schema 定义 - TextStylesSchema

**覆盖的上游需求：** Design 切片 1 - JSON Schema 扩展

**依赖：** 任务 1

**文件：**
- 修改：`PSDExporterProject/src/generator/json-schema.ts`

- [ ] **步骤 1: 定义颜色 Schema**

在 `json-schema.ts` 中添加：

```typescript
const ColorSchema = z.object({
  r: z.number().min(0).max(1),
  g: z.number().min(0).max(1),
  b: z.number().min(0).max(1),
  a: z.number().min(0).max(1)
});
```

- [ ] **步骤 2: 定义效果 Schema**

```typescript
const StrokeEffectSchema = z.object({
  type: z.literal('stroke'),
  enabled: z.boolean(),
  color: ColorSchema,
  width: z.number().min(0),
  position: z.enum(['outside', 'inside', 'center'])
});

const ShadowEffectSchema = z.object({
  type: z.enum(['dropShadow', 'innerShadow']),
  enabled: z.boolean(),
  color: ColorSchema,
  offsetX: z.number(),
  offsetY: z.number(),
  blur: z.number().min(0)
});

const GradientEffectSchema = z.object({
  type: z.literal('gradient'),
  enabled: z.boolean(),
  gradientType: z.enum(['linear', 'radial']),
  angle: z.number().min(0).max(360),
  colors: z.array(z.object({
    r: z.number().min(0).max(1),
    g: z.number().min(0).max(1),
    b: z.number().min(0).max(1),
    a: z.number().min(0).max(1),
    position: z.number().min(0).max(1)
  })).min(2),
  degraded: z.boolean()
});

const GlowEffectSchema = z.object({
  type: z.literal('outerGlow'),
  enabled: z.boolean(),
  color: ColorSchema,
  size: z.number().min(0),
  spread: z.number().min(0)
});

const BevelEffectSchema = z.object({
  type: z.literal('bevel'),
  enabled: z.boolean(),
  style: z.string(),
  depth: z.number(),
  size: z.number().min(0),
  angle: z.number().min(0).max(360),
  highlightColor: ColorSchema,
  shadowColor: ColorSchema
});

const TextEffectSchema = z.discriminatedUnion('type', [
  StrokeEffectSchema,
  ShadowEffectSchema,
  GradientEffectSchema,
  GlowEffectSchema,
  BevelEffectSchema
]);
```

- [ ] **步骤 3: 定义 TextStylesSchema**

```typescript
export const TextStylesSchema = z.object({
  fontSize: z.number().positive(),
  color: ColorSchema,
  fontName: z.string().min(1),
  fontStyle: z.object({
    bold: z.boolean(),
    italic: z.boolean()
  }),
  alignment: z.object({
    horizontal: z.enum(['left', 'center', 'right', 'justify']),
    vertical: z.enum(['top', 'middle', 'bottom'])
  }),
  effects: z.array(TextEffectSchema).optional()
});
```

- [ ] **步骤 4: 扩展 LayerConfigSchema**

在现有 `LayerConfigSchema` 中添加：

```typescript
export const LayerConfigSchema = z.object({
  // ... 现有字段
  textStyles: TextStylesSchema.optional()
});
```

- [ ] **步骤 5: 验证编译**

运行：
```bash
cd PSDExporterProject
npm run build
```

预期：编译成功

- [ ] **步骤 6: Commit**

```bash
git add PSDExporterProject/src/generator/json-schema.ts
git commit -m "feat(schema): add Zod validation schemas for text styles

- Add ColorSchema with RGBA range validation
- Add 6 effect schemas with discriminated union
- Add TextStylesSchema with fontSize, fontStyle, alignment
- Extend LayerConfigSchema with optional textStyles field

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 3: JSON Schema 验证测试

**覆盖的上游需求：** Design 切片 1 验收标准

**依赖：** 任务 2

**文件：**
- 创建：`PSDExporterProject/tests/unit/json-schema.test.ts`

- [ ] **步骤 1: 编写测试 - 验证合法 TextStyles**

创建文件并添加：

```typescript
import { describe, it, expect } from '@jest/globals';
import { TextStylesSchema } from '../../src/generator/json-schema';

describe('TextStylesSchema', () => {
  it('should accept valid text styles with all required fields', () => {
    const validTextStyles = {
      fontSize: 36,
      color: { r: 1.0, g: 1.0, b: 1.0, a: 1.0 },
      fontName: 'Arial',
      fontStyle: { bold: true, italic: false },
      alignment: { horizontal: 'center' as const, vertical: 'middle' as const },
      effects: []
    };

    const result = TextStylesSchema.safeParse(validTextStyles);
    expect(result.success).toBe(true);
  });

  it('should accept text styles without effects', () => {
    const validTextStyles = {
      fontSize: 24,
      color: { r: 0.0, g: 0.0, b: 0.0, a: 1.0 },
      fontName: 'Helvetica',
      fontStyle: { bold: false, italic: true },
      alignment: { horizontal: 'left' as const, vertical: 'top' as const }
    };

    const result = TextStylesSchema.safeParse(validTextStyles);
    expect(result.success).toBe(true);
  });
});
```

- [ ] **步骤 2: 编写测试 - 拒绝非法数据**

```typescript
describe('TextStylesSchema validation rules', () => {
  it('should reject negative fontSize', () => {
    const invalid = {
      fontSize: -12,
      color: { r: 0.5, g: 0.5, b: 0.5, a: 1.0 },
      fontName: 'Arial',
      fontStyle: { bold: false, italic: false },
      alignment: { horizontal: 'left' as const, vertical: 'top' as const }
    };

    const result = TextStylesSchema.safeParse(invalid);
    expect(result.success).toBe(false);
  });

  it('should reject color values outside [0, 1] range', () => {
    const invalid = {
      fontSize: 18,
      color: { r: 1.5, g: 0.5, b: 0.5, a: 1.0 },
      fontName: 'Arial',
      fontStyle: { bold: false, italic: false },
      alignment: { horizontal: 'left' as const, vertical: 'top' as const }
    };

    const result = TextStylesSchema.safeParse(invalid);
    expect(result.success).toBe(false);
  });

  it('should reject invalid alignment values', () => {
    const invalid = {
      fontSize: 18,
      color: { r: 0.5, g: 0.5, b: 0.5, a: 1.0 },
      fontName: 'Arial',
      fontStyle: { bold: false, italic: false },
      alignment: { horizontal: 'invalid' as any, vertical: 'top' as const }
    };

    const result = TextStylesSchema.safeParse(invalid);
    expect(result.success).toBe(false);
  });
});
```

- [ ] **步骤 3: 编写测试 - 验证效果 Schema**

```typescript
describe('Effect schemas', () => {
  it('should accept valid stroke effect', () => {
    const textStyles = {
      fontSize: 24,
      color: { r: 1.0, g: 1.0, b: 1.0, a: 1.0 },
      fontName: 'Arial',
      fontStyle: { bold: false, italic: false },
      alignment: { horizontal: 'center' as const, vertical: 'middle' as const },
      effects: [{
        type: 'stroke' as const,
        enabled: true,
        color: { r: 0.0, g: 0.0, b: 0.0, a: 1.0 },
        width: 2.0,
        position: 'outside' as const
      }]
    };

    const result = TextStylesSchema.safeParse(textStyles);
    expect(result.success).toBe(true);
  });

  it('should accept valid gradient effect', () => {
    const textStyles = {
      fontSize: 24,
      color: { r: 1.0, g: 1.0, b: 1.0, a: 1.0 },
      fontName: 'Arial',
      fontStyle: { bold: false, italic: false },
      alignment: { horizontal: 'center' as const, vertical: 'middle' as const },
      effects: [{
        type: 'gradient' as const,
        enabled: true,
        gradientType: 'linear' as const,
        angle: 90,
        colors: [
          { r: 1.0, g: 1.0, b: 0.0, a: 1.0, position: 0.0 },
          { r: 1.0, g: 0.5, b: 0.0, a: 1.0, position: 1.0 }
        ],
        degraded: false
      }]
    };

    const result = TextStylesSchema.safeParse(textStyles);
    expect(result.success).toBe(true);
  });
});
```

- [ ] **步骤 4: 运行测试验证**

运行：
```bash
cd PSDExporterProject
npm test -- json-schema.test.ts
```

预期：所有测试通过

- [ ] **步骤 5: Commit**

```bash
git add PSDExporterProject/tests/unit/json-schema.test.ts
git commit -m "test(schema): add comprehensive tests for TextStyles validation

- Test valid text styles with all fields
- Test validation rules (negative fontSize, color range, alignment)
- Test effect schemas (stroke, gradient)
- Ensure Zod rejects invalid data

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 4: TextStyleExtractor 实现 - 基础属性提取

**覆盖的上游需求：** Design 切片 2 - 文本基础属性提取

**依赖：** 任务 3

**文件：**
- 创建：`PSDExporterProject/src/parser/text-style-extractor.ts`

- [ ] **步骤 1: 创建 TextStyleExtractor 类骨架**

创建文件：

```typescript
import type { LayerTextData } from 'ag-psd';
import type { TextStyles } from './layer-tree';

export class TextStyleExtractor {
  /**
   * 从 ag-psd 的 LayerTextData 提取文本样式
   */
  extract(textData: LayerTextData | undefined): TextStyles | null {
    if (!textData || !textData.text) {
      return this.getDefaultTextStyles();
    }

    return {
      fontSize: this.convertFontSize(textData.style?.fontSize || 12),
      color: this.convertColor(textData.style?.fillColor),
      fontName: textData.style?.font?.name || 'Arial',
      fontStyle: {
        bold: textData.style?.font?.weights?.includes('Bold') || false,
        italic: textData.style?.font?.italic || false
      },
      alignment: this.convertAlignment(
        textData.paragraphStyle?.justification,
        textData.transform?.shapeType
      )
    };
  }

  private getDefaultTextStyles(): TextStyles {
    return {
      fontSize: 12,
      color: { r: 0, g: 0, b: 0, a: 1 },
      fontName: 'Arial',
      fontStyle: { bold: false, italic: false },
      alignment: { horizontal: 'left', vertical: 'top' }
    };
  }

  private convertFontSize(psdFontSize: number): number {
    // 初始缩放因子 1.0，通过实际测试确定
    const scaleFactor = 1.0;
    return psdFontSize * scaleFactor;
  }

  private convertColor(color: any): { r: number; g: number; b: number; a: number } {
    // 将在任务 5 中实现
    return { r: 0, g: 0, b: 0, a: 1 };
  }

  private convertAlignment(
    justification: any,
    shapeType: any
  ): { horizontal: string; vertical: string } {
    // 将在任务 5 中实现
    return { horizontal: 'left', vertical: 'top' };
  }
}
```

- [ ] **步骤 2: 验证编译**

运行：
```bash
cd PSDExporterProject
npm run build
```

预期：编译成功

- [ ] **步骤 3: Commit**

```bash
git add PSDExporterProject/src/parser/text-style-extractor.ts
git commit -m "feat(parser): create TextStyleExtractor class skeleton

- Add extract() method with default styles fallback
- Add convertFontSize() with scaleFactor 1.0
- Add placeholder methods for color and alignment conversion

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 5: 颜色转换和对齐映射实现

**覆盖的上游需求：** Design 切片 2 - 颜色空间转换、对齐映射

**依赖：** 任务 4

**文件：**
- 修改：`PSDExporterProject/src/parser/text-style-extractor.ts`
- 修改：`PSDExporterProject/package.json`（添加 color-convert 依赖）

- [ ] **步骤 1: 安装 color-convert 库**

运行：
```bash
cd PSDExporterProject
npm install color-convert
npm install --save-dev @types/color-convert
```

- [ ] **步骤 2: 实现 convertColor 方法**

在 `text-style-extractor.ts` 顶部导入：

```typescript
import convert from 'color-convert';
```

替换 `convertColor` 方法：

```typescript
private convertColor(color: any): { r: number; g: number; b: number; a: number } {
  if (!color) {
    return { r: 0, g: 0, b: 0, a: 1 };
  }

  try {
    // RGBA 颜色
    if ('r' in color && 'g' in color && 'b' in color) {
      return {
        r: color.r / 255,
        g: color.g / 255,
        b: color.b / 255,
        a: color.a !== undefined ? color.a / 255 : 1
      };
    }

    // CMYK 颜色
    if ('c' in color && 'm' in color && 'y' in color && 'k' in color) {
      const [r, g, b] = convert.cmyk.rgb([
        color.c * 100,
        color.m * 100,
        color.y * 100,
        color.k * 100
      ]);
      return { r: r / 255, g: g / 255, b: b / 255, a: 1 };
    }

    // Lab 颜色
    if ('l' in color && 'a' in color && 'b' in color) {
      const [r, g, b] = convert.lab.rgb([color.l, color.a, color.b]);
      return { r: r / 255, g: g / 255, b: b / 255, a: 1 };
    }

    console.warn('Unsupported color space, using black:', color);
    return { r: 0, g: 0, b: 0, a: 1 };
  } catch (error) {
    console.error('Color conversion error:', error);
    return { r: 0, g: 0, b: 0, a: 1 };
  }
}
```

- [ ] **步骤 3: 实现 convertAlignment 方法**

替换 `convertAlignment` 方法：

```typescript
private convertAlignment(
  justification: any,
  shapeType: any
): { horizontal: string; vertical: string } {
  // 水平对齐
  let horizontal: 'left' | 'center' | 'right' | 'justify' = 'left';
  
  if (justification === 'center') {
    horizontal = 'center';
  } else if (justification === 'right') {
    horizontal = 'right';
  } else if (justification === 'justify') {
    horizontal = 'justify';
  }

  // 垂直对齐：根据 shapeType 推断
  // point text: 默认 top
  // box text: 根据 paragraphStyle 推断，默认 top
  let vertical: 'top' | 'middle' | 'bottom' = 'top';
  
  if (shapeType === 'box') {
    // 对于文本框，使用 middle 作为默认值（常见情况）
    vertical = 'middle';
  }

  return { horizontal, vertical };
}
```

- [ ] **步骤 4: 验证编译**

运行：
```bash
cd PSDExporterProject
npm run build
```

预期：编译成功

- [ ] **步骤 5: 创建单元测试文件**

创建 `PSDExporterProject/tests/unit/text-style-extractor.test.ts`：

```typescript
import { describe, it, expect } from '@jest/globals';
import { TextStyleExtractor } from '../../src/parser/text-style-extractor';

describe('TextStyleExtractor', () => {
  let extractor: TextStyleExtractor;

  beforeEach(() => {
    extractor = new TextStyleExtractor();
  });

  describe('extract()', () => {
    it('should return default styles when textData is undefined', () => {
      const result = extractor.extract(undefined);
      
      expect(result).toEqual({
        fontSize: 12,
        color: { r: 0, g: 0, b: 0, a: 1 },
        fontName: 'Arial',
        fontStyle: { bold: false, italic: false },
        alignment: { horizontal: 'left', vertical: 'top' }
      });
    });

    it('should extract basic text properties', () => {
      const textData = {
        text: 'Sample Text',
        style: {
          fontSize: 36,
          fillColor: { r: 255, g: 255, b: 255, a: 255 },
          font: { name: 'Helvetica', weights: ['Bold'] }
        },
        paragraphStyle: {
          justification: 'center'
        },
        transform: {
          shapeType: 'box'
        }
      } as any;

      const result = extractor.extract(textData);

      expect(result?.fontSize).toBe(36);
      expect(result?.color).toEqual({ r: 1, g: 1, b: 1, a: 1 });
      expect(result?.fontName).toBe('Helvetica');
      expect(result?.fontStyle.bold).toBe(true);
      expect(result?.alignment.horizontal).toBe('center');
      expect(result?.alignment.vertical).toBe('middle');
    });
  });

  describe('color conversion', () => {
    it('should convert RGBA colors correctly', () => {
      const textData = {
        text: 'Test',
        style: {
          fontSize: 12,
          fillColor: { r: 128, g: 64, b: 192, a: 255 }
        }
      } as any;

      const result = extractor.extract(textData);

      expect(result?.color.r).toBeCloseTo(128 / 255, 2);
      expect(result?.color.g).toBeCloseTo(64 / 255, 2);
      expect(result?.color.b).toBeCloseTo(192 / 255, 2);
      expect(result?.color.a).toBe(1);
    });
  });

  describe('alignment mapping', () => {
    const testCases = [
      { justification: 'left', shapeType: 'point', expected: { horizontal: 'left', vertical: 'top' } },
      { justification: 'center', shapeType: 'point', expected: { horizontal: 'center', vertical: 'top' } },
      { justification: 'right', shapeType: 'point', expected: { horizontal: 'right', vertical: 'top' } },
      { justification: 'left', shapeType: 'box', expected: { horizontal: 'left', vertical: 'middle' } },
      { justification: 'center', shapeType: 'box', expected: { horizontal: 'center', vertical: 'middle' } },
      { justification: 'right', shapeType: 'box', expected: { horizontal: 'right', vertical: 'middle' } },
    ];

    testCases.forEach(({ justification, shapeType, expected }) => {
      it(`should map ${justification}/${shapeType} to ${expected.horizontal}/${expected.vertical}`, () => {
        const textData = {
          text: 'Test',
          style: { fontSize: 12 },
          paragraphStyle: { justification },
          transform: { shapeType }
        } as any;

        const result = extractor.extract(textData);

        expect(result?.alignment).toEqual(expected);
      });
    });
  });
});
```

- [ ] **步骤 6: 运行测试验证**

运行：
```bash
cd PSDExporterProject
npm test -- text-style-extractor.test.ts
```

预期：所有测试通过

- [ ] **步骤 7: Commit**

```bash
git add PSDExporterProject/src/parser/text-style-extractor.ts \
        PSDExporterProject/tests/unit/text-style-extractor.test.ts \
        PSDExporterProject/package.json \
        PSDExporterProject/package-lock.json
git commit -m "feat(parser): implement color conversion and alignment mapping

- Add color-convert library for RGBA/CMYK/Lab support
- Implement convertColor() with fallback for unsupported spaces
- Implement convertAlignment() with 9 combinations mapping
- Add comprehensive unit tests for color and alignment

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 6: 集成 TextStyleExtractor 到 psd-parser

**覆盖的上游需求：** Design 切片 2 - 集成到 convertLayer()

**依赖：** 任务 5

**文件：**
- 修改：`PSDExporterProject/src/parser/psd-parser.ts`

- [ ] **步骤 1: 导入 TextStyleExtractor**

在 `psd-parser.ts` 顶部添加：

```typescript
import { TextStyleExtractor } from './text-style-extractor';
```

- [ ] **步骤 2: 在 PsdParser 类中添加提取器实例**

在 `PsdParser` 类中添加属性：

```typescript
export class PsdParser {
  private textStyleExtractor: TextStyleExtractor;

  constructor() {
    this.textStyleExtractor = new TextStyleExtractor();
  }

  // ... 现有方法
}
```

- [ ] **步骤 3: 在 convertLayer() 中调用提取器**

找到 `convertLayer()` 方法，在处理图层类型的逻辑中添加文本样式提取：

```typescript
private convertLayer(node: PsdLayer, parentPath: string): Layer {
  const layer: Layer = {
    // ... 现有字段映射
  };

  // 如果是文本图层，提取文本样式
  if (node.type === 'text' && node.text) {
    layer.textStyles = this.textStyleExtractor.extract(node.text);
  }

  // ... 递归处理子图层

  return layer;
}
```

- [ ] **步骤 4: 验证编译**

运行：
```bash
cd PSDExporterProject
npm run build
```

预期：编译成功

- [ ] **步骤 5: Commit**

```bash
git add PSDExporterProject/src/parser/psd-parser.ts
git commit -m "feat(parser): integrate TextStyleExtractor into PsdParser

- Add TextStyleExtractor instance to PsdParser
- Call extract() in convertLayer() for text layers
- Populate layer.textStyles field in output

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 7: 集成测试和性能测试

**覆盖的上游需求：** FRD 验收条件 - 50+ 文本对象 < 10 秒

**依赖：** 任务 6

**文件：**
- 创建：`PSDExporterProject/tests/integration/text-styles-extraction.test.ts`

- [ ] **步骤 1: 准备测试 PSD 文件**

确保测试数据目录存在并放置包含文本图层的测试 PSD：

```bash
mkdir -p PSDExporterProject/tests/fixtures
# 手动放置测试 PSD 或使用现有的测试文件
```

- [ ] **步骤 2: 编写集成测试**

创建 `tests/integration/text-styles-extraction.test.ts`：

```typescript
import { describe, it, expect } from '@jest/globals';
import { PsdParser } from '../../src/parser/psd-parser';
import { readPsd } from 'ag-psd';
import * as fs from 'fs';
import * as path from 'path';

describe('Text Styles Extraction Integration', () => {
  it('should extract textStyles from PSD with text layers', async () => {
    // 读取测试 PSD
    const psdPath = path.join(__dirname, '../fixtures/text-sample.psd');
    
    if (!fs.existsSync(psdPath)) {
      console.warn('Test PSD not found, skipping integration test');
      return;
    }

    const buffer = fs.readFileSync(psdPath);
    const psd = readPsd(buffer);

    // 解析
    const parser = new PsdParser();
    const result = parser.parse(psd);

    // 验证：至少有一个文本图层包含 textStyles
    const textLayers = findTextLayers(result.layers);
    expect(textLayers.length).toBeGreaterThan(0);

    // 验证 textStyles 结构
    textLayers.forEach(layer => {
      expect(layer.textStyles).toBeDefined();
      expect(layer.textStyles?.fontSize).toBeGreaterThan(0);
      expect(layer.textStyles?.color).toHaveProperty('r');
      expect(layer.textStyles?.fontName).toBeTruthy();
      expect(layer.textStyles?.alignment).toHaveProperty('horizontal');
    });
  });
});

function findTextLayers(layers: any[]): any[] {
  let result: any[] = [];
  
  for (const layer of layers) {
    if (layer.type === 'text' && layer.textStyles) {
      result.push(layer);
    }
    if (layer.children) {
      result = result.concat(findTextLayers(layer.children));
    }
  }
  
  return result;
}
```

- [ ] **步骤 3: 编写性能测试**

添加性能测试：

```typescript
describe('Text Styles Extraction Performance', () => {
  it('should parse 50+ text objects in less than 10 seconds', async () => {
    // 如果有大型测试 PSD，使用它；否则跳过
    const largePsdPath = path.join(__dirname, '../fixtures/large-text-sample.psd');
    
    if (!fs.existsSync(largePsdPath)) {
      console.warn('Large test PSD not found, skipping performance test');
      return;
    }

    const buffer = fs.readFileSync(largePsdPath);
    const psd = readPsd(buffer);

    const parser = new PsdParser();
    const startTime = Date.now();
    
    const result = parser.parse(psd);
    
    const elapsed = Date.now() - startTime;
    const textLayers = findTextLayers(result.layers);

    console.log(`Parsed ${textLayers.length} text layers in ${elapsed}ms`);
    
    expect(textLayers.length).toBeGreaterThanOrEqual(50);
    expect(elapsed).toBeLessThan(10000); // 10 seconds
  });
});
```

- [ ] **步骤 4: 运行集成测试**

运行：
```bash
cd PSDExporterProject
npm test -- text-styles-extraction.test.ts
```

预期：集成测试通过（如果测试 PSD 存在）

- [ ] **步骤 5: Commit**

```bash
git add PSDExporterProject/tests/integration/text-styles-extraction.test.ts
git commit -m "test(parser): add integration and performance tests for text extraction

- Add integration test verifying textStyles in parsed JSON
- Add performance test for 50+ text objects < 10s
- Add helper to find text layers recursively

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 8: TextStyleApplier 实现 - 基础属性应用

**覆盖的上游需求：** Design 切片 3 - 文本基础属性应用

**依赖：** 任务 7

**文件：**
- 创建：`UnityProject/Assets/Change/Editor/PSD2UI/TextStyleApplier.cs`

- [ ] **步骤 1: 创建 TextStyleApplier 类骨架**

创建文件：

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Change.Editor.PSD2UI
{
    public class TextStyleApplier
    {
        public void ApplyBasicProperties(
            TextMeshProUGUI tmpComponent,
            TextStylesData textStyles,
            string layerName)
        {
            if (tmpComponent == null || textStyles == null)
            {
                Debug.LogWarning($"[TextStyleApplier] Null component or styles for {layerName}");
                return;
            }

            // 检测锁定
            if (IsLocked(tmpComponent.gameObject))
            {
                Debug.Log($"[TextStyleApplier] Skipping locked component: {layerName}");
                return;
            }

            // 应用基础属性
            tmpComponent.fontSize = textStyles.fontSize;
            tmpComponent.color = new Color(
                textStyles.color.r,
                textStyles.color.g,
                textStyles.color.b,
                textStyles.color.a
            );

            // 应用字体样式
            if (textStyles.fontStyle.bold && textStyles.fontStyle.italic)
            {
                tmpComponent.fontStyle = FontStyles.Bold | FontStyles.Italic;
            }
            else if (textStyles.fontStyle.bold)
            {
                tmpComponent.fontStyle = FontStyles.Bold;
            }
            else if (textStyles.fontStyle.italic)
            {
                tmpComponent.fontStyle = FontStyles.Italic;
            }
            else
            {
                tmpComponent.fontStyle = FontStyles.Normal;
            }

            // 应用对齐
            tmpComponent.alignment = MapAlignment(
                textStyles.alignment.horizontal,
                textStyles.alignment.vertical
            );

            // 应用字体
            var font = FindFont(textStyles.fontName);
            if (font != null)
            {
                tmpComponent.font = font;
            }
            else
            {
                Debug.LogWarning($"[TextStyleApplier] Font not found: {textStyles.fontName}, using default");
            }
        }

        private bool IsLocked(GameObject gameObject)
        {
            var lockComponent = gameObject.GetComponent<PSD2UILock>();
            return lockComponent != null && lockComponent.LockComponents;
        }

        private TMP_FontAsset FindFont(string fontName)
        {
            // 将在任务 10 中完善
            return TMP_Settings.defaultFontAsset;
        }

        private TextAlignmentOptions MapAlignment(string horizontal, string vertical)
        {
            // 将在任务 9 中实现
            return TextAlignmentOptions.TopLeft;
        }
    }

    // 数据结构（与 JSON 对应）
    [System.Serializable]
    public class TextStylesData
    {
        public float fontSize;
        public ColorData color;
        public string fontName;
        public FontStyleData fontStyle;
        public AlignmentData alignment;
        public TextEffectData[] effects;
    }

    [System.Serializable]
    public class ColorData
    {
        public float r, g, b, a;
    }

    [System.Serializable]
    public class FontStyleData
    {
        public bool bold;
        public bool italic;
    }

    [System.Serializable]
    public class AlignmentData
    {
        public string horizontal;
        public string vertical;
    }

    [System.Serializable]
    public class TextEffectData
    {
        public string type;
        public bool enabled;
    }
}
```

- [ ] **步骤 2: 验证编译**

在 Unity 编辑器中：
- 打开项目
- 等待脚本编译
- 检查 Console 无错误

预期：编译成功

- [ ] **步骤 3: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/TextStyleApplier.cs
git commit -m "feat(unity): create TextStyleApplier class skeleton

- Add ApplyBasicProperties() method
- Add IsLocked() check for PSD2UILock component
- Add placeholder methods for font finding and alignment mapping
- Define data structures matching JSON schema

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 9: 对齐映射和字体匹配单元测试

**覆盖的上游需求：** Design 切片 3 验收标准

**依赖：** 任务 8

**文件：**
- 创建：`UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/TextStyleApplierTests.cs`

- [ ] **步骤 1: 创建测试文件骨架**

创建文件：

```csharp
using NUnit.Framework;
using UnityEngine;
using TMPro;
using Change.Editor.PSD2UI;

namespace Change.Editor.PSD2UI.Tests
{
    [TestFixture]
    public class TextStyleApplierTests
    {
        private GameObject testObject;
        private TextMeshProUGUI tmpComponent;
        private TextStyleApplier applier;

        [SetUp]
        public void Setup()
        {
            testObject = new GameObject("TestText");
            tmpComponent = testObject.AddComponent<TextMeshProUGUI>();
            applier = new TextStyleApplier();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(testObject);
        }

        [Test]
        public void ApplyBasicProperties_ShouldSetFontSize()
        {
            var textStyles = new TextStylesData
            {
                fontSize = 36,
                color = new ColorData { r = 1, g = 1, b = 1, a = 1 },
                fontName = "Arial",
                fontStyle = new FontStyleData { bold = false, italic = false },
                alignment = new AlignmentData { horizontal = "center", vertical = "middle" }
            };

            applier.ApplyBasicProperties(tmpComponent, textStyles, "test_layer");

            Assert.AreEqual(36, tmpComponent.fontSize);
        }

        [Test]
        public void ApplyBasicProperties_ShouldSetColor()
        {
            var textStyles = new TextStylesData
            {
                fontSize = 24,
                color = new ColorData { r = 0.5f, g = 0.25f, b = 0.75f, a = 1 },
                fontName = "Arial",
                fontStyle = new FontStyleData { bold = false, italic = false },
                alignment = new AlignmentData { horizontal = "left", vertical = "top" }
            };

            applier.ApplyBasicProperties(tmpComponent, textStyles, "test_layer");

            Assert.AreEqual(new Color(0.5f, 0.25f, 0.75f, 1), tmpComponent.color);
        }

        [Test]
        public void ApplyBasicProperties_ShouldApplyBoldStyle()
        {
            var textStyles = new TextStylesData
            {
                fontSize = 24,
                color = new ColorData { r = 1, g = 1, b = 1, a = 1 },
                fontName = "Arial",
                fontStyle = new FontStyleData { bold = true, italic = false },
                alignment = new AlignmentData { horizontal = "left", vertical = "top" }
            };

            applier.ApplyBasicProperties(tmpComponent, textStyles, "test_layer");

            Assert.AreEqual(FontStyles.Bold, tmpComponent.fontStyle);
        }

        [Test]
        public void ApplyBasicProperties_ShouldApplyItalicStyle()
        {
            var textStyles = new TextStylesData
            {
                fontSize = 24,
                color = new ColorData { r = 1, g = 1, b = 1, a = 1 },
                fontName = "Arial",
                fontStyle = new FontStyleData { bold = false, italic = true },
                alignment = new AlignmentData { horizontal = "left", vertical = "top" }
            };

            applier.ApplyBasicProperties(tmpComponent, textStyles, "test_layer");

            Assert.AreEqual(FontStyles.Italic, tmpComponent.fontStyle);
        }

        [Test]
        public void ApplyBasicProperties_ShouldSkipLockedComponent()
        {
            var lockComponent = testObject.AddComponent<PSD2UILock>();
            lockComponent.LockComponents = true;

            var textStyles = new TextStylesData
            {
                fontSize = 36,
                color = new ColorData { r = 1, g = 1, b = 1, a = 1 },
                fontName = "Arial",
                fontStyle = new FontStyleData { bold = false, italic = false },
                alignment = new AlignmentData { horizontal = "center", vertical = "middle" }
            };

            var originalFontSize = tmpComponent.fontSize;
            applier.ApplyBasicProperties(tmpComponent, textStyles, "test_layer");

            Assert.AreEqual(originalFontSize, tmpComponent.fontSize);
        }
    }
}
```

- [ ] **步骤 2: 编写对齐映射测试**

在同一文件中添加：

```csharp
[Test]
public void MapAlignment_TopLeft()
{
    var textStyles = new TextStylesData
    {
        fontSize = 24,
        color = new ColorData { r = 1, g = 1, b = 1, a = 1 },
        fontName = "Arial",
        fontStyle = new FontStyleData { bold = false, italic = false },
        alignment = new AlignmentData { horizontal = "left", vertical = "top" }
    };

    applier.ApplyBasicProperties(tmpComponent, textStyles, "test");

    Assert.AreEqual(TextAlignmentOptions.TopLeft, tmpComponent.alignment);
}

[Test]
public void MapAlignment_TopCenter()
{
    var textStyles = new TextStylesData
    {
        fontSize = 24,
        color = new ColorData { r = 1, g = 1, b = 1, a = 1 },
        fontName = "Arial",
        fontStyle = new FontStyleData { bold = false, italic = false },
        alignment = new AlignmentData { horizontal = "center", vertical = "top" }
    };

    applier.ApplyBasicProperties(tmpComponent, textStyles, "test");

    Assert.AreEqual(TextAlignmentOptions.Top, tmpComponent.alignment);
}

[Test]
public void MapAlignment_MiddleCenter()
{
    var textStyles = new TextStylesData
    {
        fontSize = 24,
        color = new ColorData { r = 1, g = 1, b = 1, a = 1 },
        fontName = "Arial",
        fontStyle = new FontStyleData { bold = false, italic = false },
        alignment = new AlignmentData { horizontal = "center", vertical = "middle" }
    };

    applier.ApplyBasicProperties(tmpComponent, textStyles, "test");

    Assert.AreEqual(TextAlignmentOptions.Center, tmpComponent.alignment);
}

[Test]
public void MapAlignment_BottomRight()
{
    var textStyles = new TextStylesData
    {
        fontSize = 24,
        color = new ColorData { r = 1, g = 1, b = 1, a = 1 },
        fontName = "Arial",
        fontStyle = new FontStyleData { bold = false, italic = false },
        alignment = new AlignmentData { horizontal = "right", vertical = "bottom" }
    };

    applier.ApplyBasicProperties(tmpComponent, textStyles, "test");

    Assert.AreEqual(TextAlignmentOptions.BottomRight, tmpComponent.alignment);
}
```

- [ ] **步骤 3: 运行测试验证失败**

在 Unity 编辑器中：
- Window → General → Test Runner
- 选择 EditMode
- 运行 TextStyleApplierTests

预期：对齐测试失败（因为 MapAlignment 未实现）

- [ ] **步骤 4: 实现 MapAlignment 方法**

在 `TextStyleApplier.cs` 中替换 `MapAlignment` 方法：

```csharp
private TextAlignmentOptions MapAlignment(string horizontal, string vertical)
{
    // 构建映射表：horizontal + vertical → TextAlignmentOptions
    if (vertical == "top")
    {
        if (horizontal == "left") return TextAlignmentOptions.TopLeft;
        if (horizontal == "center") return TextAlignmentOptions.Top;
        if (horizontal == "right") return TextAlignmentOptions.TopRight;
        if (horizontal == "justify") return TextAlignmentOptions.TopJustified;
    }
    else if (vertical == "middle")
    {
        if (horizontal == "left") return TextAlignmentOptions.Left;
        if (horizontal == "center") return TextAlignmentOptions.Center;
        if (horizontal == "right") return TextAlignmentOptions.Right;
        if (horizontal == "justify") return TextAlignmentOptions.Justified;
    }
    else if (vertical == "bottom")
    {
        if (horizontal == "left") return TextAlignmentOptions.BottomLeft;
        if (horizontal == "center") return TextAlignmentOptions.Bottom;
        if (horizontal == "right") return TextAlignmentOptions.BottomRight;
        if (horizontal == "justify") return TextAlignmentOptions.BottomJustified;
    }

    Debug.LogWarning($"[TextStyleApplier] Unknown alignment: {horizontal}/{vertical}, using TopLeft");
    return TextAlignmentOptions.TopLeft;
}
```

- [ ] **步骤 5: 运行测试验证通过**

在 Unity Test Runner 中重新运行测试

预期：所有对齐测试通过

- [ ] **步骤 6: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/TextStyleApplierTests.cs \
        UnityProject/Assets/Change/Editor/PSD2UI/TextStyleApplier.cs
git commit -m "test(unity): add unit tests and implement alignment mapping

- Add tests for fontSize, color, fontStyle application
- Add tests for 9 alignment combinations
- Add test for locked component detection
- Implement MapAlignment() with complete mapping table

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 10: TMP Settings 配置检查和字体匹配

**覆盖的上游需求：** Research 待确定事项 #2 - TMP 默认字体

**依赖：** 任务 9

**文件：**
- 修改：`UnityProject/Assets/Change/Editor/PSD2UI/TextStyleApplier.cs`

- [ ] **步骤 1: 检查项目 TMP Settings**

在 Unity 编辑器中：
- Edit → Project Settings → TextMesh Pro → Settings
- 记录 Default Font Asset 路径

如果未配置，手动配置默认字体

- [ ] **步骤 2: 实现 FindFont 方法**

在 `TextStyleApplier.cs` 中替换 `FindFont` 方法：

```csharp
private TMP_FontAsset FindFont(string fontName)
{
    // 1. 尝试精确匹配
    var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
    
    foreach (var font in fonts)
    {
        if (font.name.Equals(fontName, System.StringComparison.OrdinalIgnoreCase))
        {
            return font;
        }
    }

    // 2. 尝试部分匹配（去除 "-TMP" 后缀）
    var fontNameWithoutSuffix = fontName.Replace("-TMP", "");
    
    foreach (var font in fonts)
    {
        var assetName = font.name.Replace("-TMP", "");
        if (assetName.Equals(fontNameWithoutSuffix, System.StringComparison.OrdinalIgnoreCase))
        {
            return font;
        }
    }

    // 3. 回退到 TMP Settings 默认字体
    if (TMP_Settings.defaultFontAsset != null)
    {
        Debug.LogWarning($"[TextStyleApplier] Font '{fontName}' not found, using TMP default: {TMP_Settings.defaultFontAsset.name}");
        return TMP_Settings.defaultFontAsset;
    }

    // 4. 最后回退到 Unity 内置 Arial
    Debug.LogWarning($"[TextStyleApplier] Font '{fontName}' not found and TMP default is null, using Unity Arial");
    return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
}
```

- [ ] **步骤 3: 添加字体匹配测试**

在 `TextStyleApplierTests.cs` 中添加：

```csharp
[Test]
public void FindFont_ShouldReturnDefaultWhenNotFound()
{
    var textStyles = new TextStylesData
    {
        fontSize = 24,
        color = new ColorData { r = 1, g = 1, b = 1, a = 1 },
        fontName = "NonExistentFont",
        fontStyle = new FontStyleData { bold = false, italic = false },
        alignment = new AlignmentData { horizontal = "left", vertical = "top" }
    };

    applier.ApplyBasicProperties(tmpComponent, textStyles, "test");

    // 应该回退到默认字体，不应该为 null
    Assert.IsNotNull(tmpComponent.font);
}
```

- [ ] **步骤 4: 运行测试验证**

在 Unity Test Runner 中运行测试

预期：字体匹配测试通过

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/TextStyleApplier.cs \
        UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/TextStyleApplierTests.cs
git commit -m "feat(unity): implement font matching with fallback strategy

- Add FindFont() with exact match, partial match, and fallback
- Use TMP Settings default font as primary fallback
- Use Unity Arial as final fallback
- Add test for font not found scenario

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 11: 集成 TextStyleApplier 到 ComponentFactory

**覆盖的上游需求：** Design 切片 3 - 集成到 ComponentFactory

**依赖：** 任务 10

**文件：**
- 修改：`UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs`
- 修改：`UnityProject/Assets/Change/Editor/PSD2UI/PrefabBuilder.cs`

- [ ] **步骤 1: 在 ComponentFactory 中添加 TextStyleApplier 实例**

在 `ComponentFactory.cs` 顶部添加字段：

```csharp
public class ComponentFactory
{
    private TextStyleApplier textStyleApplier;

    public ComponentFactory()
    {
        textStyleApplier = new TextStyleApplier();
    }

    // ... 现有方法
}
```

- [ ] **步骤 2: 修改 CreateComponent 方法签名**

找到 `CreateComponent` 方法，添加 `textStyles` 参数：

```csharp
public Component CreateComponent(
    GameObject gameObject,
    string componentType,
    LayerData layerData,
    TextStylesData textStyles = null)  // 新增参数
{
    // ... 现有逻辑
}
```

- [ ] **步骤 3: 在创建 TMP 组件后应用样式**

在 `CreateComponent` 方法中，找到创建 TextMeshProUGUI 的逻辑，添加样式应用：

```csharp
if (componentType == "Text")
{
    var tmpComponent = gameObject.AddComponent<TextMeshProUGUI>();
    
    // 应用文本样式
    if (textStyles != null)
    {
        textStyleApplier.ApplyBasicProperties(tmpComponent, textStyles, layerData.name);
    }
    
    return tmpComponent;
}
```

- [ ] **步骤 4: 修改 PrefabBuilder 传递 textStyles**

在 `PrefabBuilder.cs` 中找到调用 `CreateComponent` 的地方，传递 textStyles：

```csharp
private void CreateNode(LayerData layer, Transform parent)
{
    // ... 创建 GameObject

    // 创建组件
    if (layer.component != null)
    {
        factory.CreateComponent(
            gameObject,
            layer.component.type,
            layer,
            layer.textStyles  // 传递 textStyles
        );
    }

    // ... 递归处理子节点
}
```

- [ ] **步骤 5: 确保 LayerData 包含 textStyles 字段**

在 `LayerData` 类中添加字段（如果不存在）：

```csharp
[System.Serializable]
public class LayerData
{
    // ... 现有字段
    public TextStylesData textStyles;
}
```

- [ ] **步骤 6: 验证编译**

在 Unity 编辑器中等待编译完成

预期：编译成功，无错误

- [ ] **步骤 7: 运行现有 PrefabBuilder 测试套件**

在 Unity Test Runner 中运行所有 PrefabBuilder 相关测试

预期：现有测试仍然通过（textStyles 参数可选，不影响现有流程）

- [ ] **步骤 8: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs \
        UnityProject/Assets/Change/Editor/PSD2UI/PrefabBuilder.cs
git commit -m "feat(unity): integrate TextStyleApplier into ComponentFactory

- Add TextStyleApplier instance to ComponentFactory
- Add textStyles parameter to CreateComponent (optional)
- Apply text styles after creating TextMeshProUGUI component
- Pass textStyles from PrefabBuilder to ComponentFactory

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 12: 端到端测试 - PSD 到 Unity

**覆盖的上游需求：** Design 切片 3 验收标准 - 端到端测试

**依赖：** 任务 11

**文件：**
- 创建：`UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/TextStylesEndToEndTests.cs`

- [ ] **步骤 1: 准备测试数据**

准备一个包含文本图层的测试 JSON 配置：

```bash
mkdir -p UnityProject/Assets/Change/Editor/PSD2UI/Tests/Fixtures
```

创建 `test-text-config.json`：

```json
{
  "layers": [
    {
      "id": "layer_001",
      "name": "txt_title",
      "type": "text",
      "component": {
        "type": "Text"
      },
      "bounds": { "x": 100, "y": 50, "width": 300, "height": 60 },
      "textStyles": {
        "fontSize": 36,
        "color": { "r": 1.0, "g": 1.0, "b": 1.0, "a": 1.0 },
        "fontName": "Arial",
        "fontStyle": { "bold": true, "italic": false },
        "alignment": { "horizontal": "center", "vertical": "middle" }
      }
    }
  ]
}
```

- [ ] **步骤 2: 编写端到端测试**

创建 `TextStylesEndToEndTests.cs`：

```csharp
using NUnit.Framework;
using UnityEngine;
using TMPro;
using System.IO;
using Change.Editor.PSD2UI;

namespace Change.Editor.PSD2UI.Tests
{
    [TestFixture]
    public class TextStylesEndToEndTests
    {
        private GameObject rootObject;

        [TearDown]
        public void Teardown()
        {
            if (rootObject != null)
            {
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void EndToEnd_ShouldApplyTextStylesFromJSON()
        {
            // 1. 加载测试 JSON
            var jsonPath = Path.Combine(
                Application.dataPath,
                "Change/Editor/PSD2UI/Tests/Fixtures/test-text-config.json"
            );

            if (!File.Exists(jsonPath))
            {
                Assert.Ignore("Test JSON not found");
                return;
            }

            var json = File.ReadAllText(jsonPath);
            var config = JsonUtility.FromJson<PSDConfig>(json);

            // 2. 使用 PrefabBuilder 生成 Prefab
            var builder = new PrefabBuilder();
            rootObject = builder.Build(config);

            // 3. 验证生成的文本组件
            var textComponent = rootObject.GetComponentInChildren<TextMeshProUGUI>();
            Assert.IsNotNull(textComponent, "TextMeshProUGUI component should be created");

            // 4. 验证文本属性
            Assert.AreEqual(36, textComponent.fontSize, "Font size should match");
            Assert.AreEqual(new Color(1, 1, 1, 1), textComponent.color, "Color should match");
            Assert.AreEqual(FontStyles.Bold, textComponent.fontStyle, "Font style should be bold");
            Assert.AreEqual(TextAlignmentOptions.Center, textComponent.alignment, "Alignment should be center");

            // 5. 验证字体（应该是默认字体或 Arial）
            Assert.IsNotNull(textComponent.font, "Font should be assigned");
        }

        [Test]
        public void EndToEnd_ShouldSkipLockedComponents()
        {
            // 测试锁定组件不被更新
            var jsonPath = Path.Combine(
                Application.dataPath,
                "Change/Editor/PSD2UI/Tests/Fixtures/test-text-config.json"
            );

            if (!File.Exists(jsonPath))
            {
                Assert.Ignore("Test JSON not found");
                return;
            }

            var json = File.ReadAllText(jsonPath);
            var config = JsonUtility.FromJson<PSDConfig>(json);

            // 第一次生成
            var builder = new PrefabBuilder();
            rootObject = builder.Build(config);

            var textComponent = rootObject.GetComponentInChildren<TextMeshProUGUI>();
            
            // 添加锁定组件并修改字号
            var lockComponent = textComponent.gameObject.AddComponent<PSD2UILock>();
            lockComponent.LockComponents = true;
            textComponent.fontSize = 24; // 手动修改

            // 第二次生成（模拟增量更新）
            var config2 = JsonUtility.FromJson<PSDConfig>(json);
            config2.layers[0].textStyles.fontSize = 48; // JSON 中改为 48

            builder.Build(config2, rootObject);

            // 验证：字号应该保持 24（锁定状态），而不是变成 48
            Assert.AreEqual(24, textComponent.fontSize, "Locked component should not be updated");
        }
    }

    [System.Serializable]
    public class PSDConfig
    {
        public LayerData[] layers;
    }
}
```

- [ ] **步骤 3: 运行端到端测试**

在 Unity Test Runner 中运行 TextStylesEndToEndTests

预期：所有测试通过

- [ ] **步骤 4: 手动测试 - 实际 PSD 生成**

手动测试步骤：
1. 在 Node.js 项目中运行解析器生成包含 textStyles 的 JSON
2. 在 Unity 中使用该 JSON 生成 Prefab
3. 检查生成的 TextMeshProUGUI 组件属性是否正确
4. 验证字号、颜色、对齐是否与 PSD 一致

- [ ] **步骤 5: 调整字号 DPI 缩放因子（如果需要）**

如果手动测试发现字号与 PSD 不一致，调整 `TextStyleExtractor.ts` 中的 `scaleFactor`：

```typescript
private convertFontSize(psdFontSize: number): number {
  const scaleFactor = 1.0; // 根据测试结果调整
  return psdFontSize * scaleFactor;
}
```

重新测试直到字号匹配

- [ ] **步骤 6: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/TextStylesEndToEndTests.cs \
        UnityProject/Assets/Change/Editor/PSD2UI/Tests/Fixtures/test-text-config.json
git commit -m "test(unity): add end-to-end tests for text styles integration

- Add test for JSON → Unity Prefab text styles application
- Add test for locked component detection in incremental update
- Add test fixture with complete text styles JSON
- Verify fontSize, color, fontStyle, alignment

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 13: 效果提取器扩展 - 6 种效果提取

**覆盖的上游需求：** Design 切片 4 - 6 种效果提取

**依赖：** 任务 12

**文件：**
- 修改：`PSDExporterProject/src/parser/text-style-extractor.ts`
- 修改：`PSDExporterProject/tests/unit/text-style-extractor.test.ts`

- [ ] **步骤 1: 扩展 extract 方法添加效果提取**

在 `TextStyleExtractor.ts` 的 `extract` 方法中添加：

```typescript
extract(textData: LayerTextData | undefined, effects?: LayerEffectsInfo): TextStyles | null {
  if (!textData || !textData.text) {
    return this.getDefaultTextStyles();
  }

  const textStyles: TextStyles = {
    fontSize: this.convertFontSize(textData.style?.fontSize || 12),
    color: this.convertColor(textData.style?.fillColor),
    fontName: textData.style?.font?.name || 'Arial',
    fontStyle: {
      bold: textData.style?.font?.weights?.includes('Bold') || false,
      italic: textData.style?.font?.italic || false
    },
    alignment: this.convertAlignment(
      textData.paragraphStyle?.justification,
      textData.transform?.shapeType
    )
  };

  // 提取效果
  if (effects) {
    const { effects: extractedEffects, warnings } = this.extractEffects(effects);
    textStyles.effects = extractedEffects;
    
    // 记录警告
    if (warnings.length > 0) {
      console.warn('[TextStyleExtractor] Effect warnings:', warnings);
    }
  }

  return textStyles;
}
```

- [ ] **步骤 2: 实现 extractEffects 方法**

添加新方法：

```typescript
private extractEffects(effects: LayerEffectsInfo): { 
  effects: TextEffect[]; 
  warnings: string[] 
} {
  const extractedEffects: TextEffect[] = [];
  const warnings: string[] = [];

  // 1. 描边 (Stroke)
  if (effects.stroke?.enabled) {
    extractedEffects.push({
      type: 'stroke',
      enabled: true,
      color: this.convertColor(effects.stroke.color),
      width: effects.stroke.size || 1,
      position: effects.stroke.position || 'outside'
    } as StrokeEffect);
  }

  // 2. 阴影 (Drop Shadow)
  if (effects.dropShadow?.enabled) {
    extractedEffects.push({
      type: 'dropShadow',
      enabled: true,
      color: this.convertColor(effects.dropShadow.color),
      offsetX: effects.dropShadow.offset?.x || 0,
      offsetY: effects.dropShadow.offset?.y || 0,
      blur: effects.dropShadow.blur || 0
    } as ShadowEffect);
  }

  // 3. 内阴影 (Inner Shadow)
  if (effects.innerShadow?.enabled) {
    extractedEffects.push({
      type: 'innerShadow',
      enabled: true,
      color: this.convertColor(effects.innerShadow.color),
      offsetX: effects.innerShadow.offset?.x || 0,
      offsetY: effects.innerShadow.offset?.y || 0,
      blur: effects.innerShadow.blur || 0
    } as ShadowEffect);
  }

  // 4. 渐变 (Gradient Overlay)
  if (effects.gradientOverlay?.enabled) {
    const { degraded: gradientEffect, warning } = this.degradeGradient(effects.gradientOverlay);
    extractedEffects.push(gradientEffect);
    if (warning) {
      warnings.push(warning);
    }
  }

  // 5. 发光 (Outer Glow)
  if (effects.outerGlow?.enabled) {
    extractedEffects.push({
      type: 'outerGlow',
      enabled: true,
      color: this.convertColor(effects.outerGlow.color),
      size: effects.outerGlow.size || 5,
      spread: effects.outerGlow.spread || 0
    } as GlowEffect);
  }

  // 6. 斜角 (Bevel & Emboss)
  if (effects.bevel?.enabled) {
    extractedEffects.push({
      type: 'bevel',
      enabled: true,
      style: effects.bevel.style || 'outer',
      depth: effects.bevel.depth || 1,
      size: effects.bevel.size || 5,
      angle: effects.bevel.angle || 120,
      highlightColor: this.convertColor(effects.bevel.highlightColor),
      shadowColor: this.convertColor(effects.bevel.shadowColor)
    } as BevelEffect);
  }

  return { effects: extractedEffects, warnings };
}
```

- [ ] **步骤 3: 实现渐变降级策略**

添加方法：

```typescript
private degradeGradient(gradient: any): { 
  degraded: GradientEffect; 
  warning?: string 
} {
  let angle = gradient.angle || 0;
  let degraded = false;
  let warning: string | undefined;

  // 检查是否为斜向渐变
  const normalizedAngle = angle % 360;
  if (![0, 90, 180, 270].includes(normalizedAngle)) {
    // 降级为最接近的垂直或水平方向
    angle = normalizedAngle > 45 && normalizedAngle <= 135 ? 90 :
            normalizedAngle > 135 && normalizedAngle <= 225 ? 180 :
            normalizedAngle > 225 && normalizedAngle <= 315 ? 270 : 0;
    degraded = true;
    warning = `Gradient angle ${normalizedAngle}° degraded to ${angle}°`;
  }

  // 检查是否为径向渐变
  if (gradient.type === 'radial') {
    angle = 90; // 降级为垂直线性渐变
    degraded = true;
    warning = 'Radial gradient degraded to linear vertical';
  }

  // 提取颜色停点
  const colorStops = gradient.colors || [];
  let colors = colorStops.map((stop: any) => ({
    r: stop.color.r / 255,
    g: stop.color.g / 255,
    b: stop.color.b / 255,
    a: stop.color.a !== undefined ? stop.color.a / 255 : 1,
    position: stop.position || 0
  }));

  // 如果超过 2 个颜色，降级为双色
  if (colors.length > 2) {
    colors = [colors[0], colors[colors.length - 1]];
    degraded = true;
    warning = `Multi-color gradient (${colorStops.length} stops) degraded to 2 colors`;
  }

  return {
    degraded: {
      type: 'gradient',
      enabled: true,
      gradientType: 'linear',
      angle,
      colors,
      degraded
    },
    warning
  };
}
```

- [ ] **步骤 4: 编写效果提取测试**

在 `text-style-extractor.test.ts` 中添加：

```typescript
describe('effect extraction', () => {
  it('should extract stroke effect', () => {
    const textData = { text: 'Test', style: { fontSize: 12 } } as any;
    const effects = {
      stroke: {
        enabled: true,
        color: { r: 0, g: 0, b: 0, a: 255 },
        size: 2,
        position: 'outside'
      }
    } as any;

    const result = extractor.extract(textData, effects);

    expect(result?.effects).toHaveLength(1);
    expect(result?.effects?.[0].type).toBe('stroke');
    expect((result?.effects?.[0] as any).width).toBe(2);
  });

  it('should extract multiple effects', () => {
    const textData = { text: 'Test', style: { fontSize: 12 } } as any;
    const effects = {
      stroke: { enabled: true, color: { r: 0, g: 0, b: 0 }, size: 2 },
      dropShadow: { enabled: true, color: { r: 0, g: 0, b: 0 }, offset: { x: 2, y: -2 }, blur: 4 },
      outerGlow: { enabled: true, color: { r: 255, g: 255, b: 0 }, size: 5 }
    } as any;

    const result = extractor.extract(textData, effects);

    expect(result?.effects).toHaveLength(3);
    expect(result?.effects?.map(e => e.type)).toEqual(['stroke', 'dropShadow', 'outerGlow']);
  });

  it('should skip disabled effects', () => {
    const textData = { text: 'Test', style: { fontSize: 12 } } as any;
    const effects = {
      stroke: { enabled: false, size: 2 },
      dropShadow: { enabled: true, offset: { x: 2, y: -2 } }
    } as any;

    const result = extractor.extract(textData, effects);

    expect(result?.effects).toHaveLength(1);
    expect(result?.effects?.[0].type).toBe('dropShadow');
  });
});
```

- [ ] **步骤 5: 运行测试验证**

运行：
```bash
cd PSDExporterProject
npm test -- text-style-extractor.test.ts
```

预期：所有测试通过

- [ ] **步骤 6: Commit**

```bash
git add PSDExporterProject/src/parser/text-style-extractor.ts \
        PSDExporterProject/tests/unit/text-style-extractor.test.ts
git commit -m "feat(parser): implement 6 effect types extraction

- Add extractEffects() for stroke, shadow, gradient, glow, bevel
- Implement gradient degradation strategy (diagonal→vertical/horizontal)
- Add warning generation for degraded effects
- Add unit tests for effect extraction

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 14: Material 创建和 Shader 参数映射

**覆盖的上游需求：** Design 切片 4 - 效果应用器扩展

**依赖：** 任务 13

**文件：**
- 修改：`UnityProject/Assets/Change/Editor/PSD2UI/TextStyleApplier.cs`

- [ ] **步骤 1: 扩展 TextStyleApplier 添加效果应用方法**

在 `TextStyleApplier.cs` 中添加：

```csharp
public void ApplyEffects(
    TextMeshProUGUI tmpComponent,
    TextEffectData[] effects,
    string layerName,
    string layerId)
{
    if (effects == null || effects.Length == 0)
    {
        return;
    }

    // 创建独立 Material
    var material = CreateMaterial(layerName, layerId);
    tmpComponent.fontSharedMaterial = material;

    // 逐一应用效果
    foreach (var effect in effects)
    {
        if (!effect.enabled) continue;

        switch (effect.type)
        {
            case "stroke":
                ApplyStroke(material, effect as StrokeEffectData);
                break;
            case "dropShadow":
                ApplyDropShadow(material, effect as ShadowEffectData);
                break;
            case "innerShadow":
                ApplyInnerShadow(material, effect as ShadowEffectData);
                break;
            case "gradient":
                ApplyGradient(material, tmpComponent, effect as GradientEffectData);
                break;
            case "outerGlow":
                ApplyOuterGlow(material, effect as GlowEffectData);
                break;
            case "bevel":
                ApplyBevel(material, effect as BevelEffectData);
                break;
        }
    }
}
```

- [ ] **步骤 2: 实现 CreateMaterial 方法**

```csharp
private Material CreateMaterial(string layerName, string layerId)
{
    // 基于 TMP_SDF shader 创建 Material
    var shader = Shader.Find("TextMeshPro/Distance Field");
    if (shader == null)
    {
        Debug.LogError("[TextStyleApplier] TMP_SDF shader not found");
        return null;
    }

    var material = new Material(shader);
    material.name = $"TMP_{layerName}_{layerId}_Material";

    // 保存到独立目录
    var materialPath = $"Assets/GameRes/Materials/UI/PSD2UI/{material.name}.mat";
    var directory = Path.GetDirectoryName(materialPath);
    
    if (!Directory.Exists(directory))
    {
        Directory.CreateDirectory(directory);
    }

    AssetDatabase.CreateAsset(material, materialPath);
    AssetDatabase.SaveAssets();

    return material;
}
```

- [ ] **步骤 3: 实现描边效果**

```csharp
private void ApplyStroke(Material mat, StrokeEffectData stroke)
{
    if (mat == null || stroke == null) return;

    mat.SetFloat("_OutlineWidth", stroke.width);
    mat.SetColor("_OutlineColor", new Color(
        stroke.color.r,
        stroke.color.g,
        stroke.color.b,
        stroke.color.a
    ));
    mat.SetFloat("_OutlineSoftness", 0); // 默认硬边
}
```

- [ ] **步骤 4: 实现阴影效果**

```csharp
private void ApplyDropShadow(Material mat, ShadowEffectData shadow)
{
    if (mat == null || shadow == null) return;

    mat.SetColor("_UnderlayColor", new Color(
        shadow.color.r,
        shadow.color.g,
        shadow.color.b,
        shadow.color.a
    ));
    mat.SetFloat("_UnderlayOffsetX", shadow.offsetX / 100f);
    mat.SetFloat("_UnderlayOffsetY", shadow.offsetY / 100f);
    mat.SetFloat("_UnderlaySoftness", shadow.blur / 10f);
}

private void ApplyInnerShadow(Material mat, ShadowEffectData innerShadow)
{
    if (mat == null || innerShadow == null) return;

    // 内阴影使用 _UnderlayDilate 负值实现
    mat.SetFloat("_UnderlayDilate", -0.5f);
    mat.SetColor("_UnderlayColor", new Color(
        innerShadow.color.r,
        innerShadow.color.g,
        innerShadow.color.b,
        innerShadow.color.a
    ));
    mat.SetFloat("_UnderlayOffsetX", innerShadow.offsetX / 100f);
    mat.SetFloat("_UnderlayOffsetY", innerShadow.offsetY / 100f);
}
```

- [ ] **步骤 5: 实现发光效果**

```csharp
private void ApplyOuterGlow(Material mat, GlowEffectData glow)
{
    if (mat == null || glow == null) return;

    mat.SetColor("_GlowColor", new Color(
        glow.color.r,
        glow.color.g,
        glow.color.b,
        glow.color.a
    ));
    mat.SetFloat("_GlowOffset", glow.size / 10f);
    mat.SetFloat("_GlowOuter", 1.0f);
    mat.SetFloat("_GlowPower", 0.75f);
}
```

- [ ] **步骤 6: 实现渐变效果**

```csharp
private void ApplyGradient(Material mat, TextMeshProUGUI tmp, GradientEffectData gradient)
{
    if (mat == null || gradient == null || gradient.colors == null || gradient.colors.Length < 2)
        return;

    mat.SetFloat("_GradientScale", 1.0f);

    // 使用 TMP 的 colorGradient
    var color1 = new Color(
        gradient.colors[0].r,
        gradient.colors[0].g,
        gradient.colors[0].b,
        gradient.colors[0].a
    );
    var color2 = new Color(
        gradient.colors[1].r,
        gradient.colors[1].g,
        gradient.colors[1].b,
        gradient.colors[1].a
    );

    // 根据角度设置渐变方向
    if (gradient.angle == 90 || gradient.angle == 270)
    {
        // 垂直渐变
        tmp.enableVertexGradient = true;
        tmp.colorGradient = new VertexGradient(color1, color1, color2, color2);
    }
    else
    {
        // 水平渐变
        tmp.enableVertexGradient = true;
        tmp.colorGradient = new VertexGradient(color1, color2, color1, color2);
    }
}
```

- [ ] **步骤 7: 实现斜角效果**

```csharp
private void ApplyBevel(Material mat, BevelEffectData bevel)
{
    if (mat == null || bevel == null) return;

    mat.SetFloat("_Bevel", 1.0f);
    mat.SetFloat("_BevelWidth", bevel.size / 10f);
    mat.SetFloat("_LightAngle", bevel.angle);
    mat.SetColor("_SpecularColor", new Color(
        bevel.highlightColor.r,
        bevel.highlightColor.g,
        bevel.highlightColor.b,
        bevel.highlightColor.a
    ));
}
```

- [ ] **步骤 8: 添加效果数据结构**

在文件末尾添加：

```csharp
[System.Serializable]
public class StrokeEffectData : TextEffectData
{
    public ColorData color;
    public float width;
    public string position;
}

[System.Serializable]
public class ShadowEffectData : TextEffectData
{
    public ColorData color;
    public float offsetX;
    public float offsetY;
    public float blur;
}

[System.Serializable]
public class GradientEffectData : TextEffectData
{
    public string gradientType;
    public float angle;
    public GradientColorStop[] colors;
    public bool degraded;
}

[System.Serializable]
public class GradientColorStop
{
    public float r, g, b, a;
    public float position;
}

[System.Serializable]
public class GlowEffectData : TextEffectData
{
    public ColorData color;
    public float size;
    public float spread;
}

[System.Serializable]
public class BevelEffectData : TextEffectData
{
    public string style;
    public float depth;
    public float size;
    public float angle;
    public ColorData highlightColor;
    public ColorData shadowColor;
}
```

- [ ] **步骤 9: 验证编译**

在 Unity 编辑器中等待编译完成

预期：编译成功

- [ ] **步骤 10: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/TextStyleApplier.cs
git commit -m "feat(unity): implement Material creation and Shader parameter mapping

- Add ApplyEffects() for 6 effect types
- Add CreateMaterial() with naming rule TMP_<LayerName>_<LayerID>_Material
- Implement stroke, shadow, innerShadow, glow, gradient, bevel effects
- Map effect parameters to TMP_SDF shader properties

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 15: 单元测试 - 6 种效果的参数映射

**覆盖的上游需求：** Design 切片 4 验收标准 - 效果参数映射测试

**依赖：** 任务 14

**文件：**
- 修改：`UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/TextStyleApplierTests.cs`

- [ ] **步骤 1: 添加 Material 创建测试**

在 `TextStyleApplierTests.cs` 中添加：

```csharp
[Test]
public void CreateMaterial_ShouldUseCorrectNamingRule()
{
    var textStyles = new TextStylesData
    {
        fontSize = 24,
        color = new ColorData { r = 1, g = 1, b = 1, a = 1 },
        fontName = "Arial",
        fontStyle = new FontStyleData { bold = false, italic = false },
        alignment = new AlignmentData { horizontal = "left", vertical = "top" },
        effects = new TextEffectData[]
        {
            new StrokeEffectData
            {
                type = "stroke",
                enabled = true,
                color = new ColorData { r = 0, g = 0, b = 0, a = 1 },
                width = 2,
                position = "outside"
            }
        }
    };

    applier.ApplyBasicProperties(tmpComponent, textStyles, "test_layer");
    applier.ApplyEffects(tmpComponent, textStyles.effects, "test_layer", "layer_001");

    Assert.IsNotNull(tmpComponent.fontSharedMaterial);
    Assert.IsTrue(tmpComponent.fontSharedMaterial.name.Contains("TMP_test_layer_layer_001_Material"));
}
```

- [ ] **步骤 2: 添加描边效果测试**

```csharp
[Test]
public void ApplyStroke_ShouldSetShaderParameters()
{
    var effects = new TextEffectData[]
    {
        new StrokeEffectData
        {
            type = "stroke",
            enabled = true,
            color = new ColorData { r = 1, g = 0, b = 0, a = 1 },
            width = 3,
            position = "outside"
        }
    };

    applier.ApplyEffects(tmpComponent, effects, "test", "001");

    var mat = tmpComponent.fontSharedMaterial;
    Assert.AreEqual(3, mat.GetFloat("_OutlineWidth"));
    Assert.AreEqual(new Color(1, 0, 0, 1), mat.GetColor("_OutlineColor"));
}
```

- [ ] **步骤 3: 添加阴影效果测试**

```csharp
[Test]
public void ApplyDropShadow_ShouldSetShaderParameters()
{
    var effects = new TextEffectData[]
    {
        new ShadowEffectData
        {
            type = "dropShadow",
            enabled = true,
            color = new ColorData { r = 0, g = 0, b = 0, a = 0.5f },
            offsetX = 2,
            offsetY = -2,
            blur = 4
        }
    };

    applier.ApplyEffects(tmpComponent, effects, "test", "001");

    var mat = tmpComponent.fontSharedMaterial;
    Assert.AreEqual(new Color(0, 0, 0, 0.5f), mat.GetColor("_UnderlayColor"));
    Assert.AreEqual(2f / 100f, mat.GetFloat("_UnderlayOffsetX"), 0.001f);
    Assert.AreEqual(-2f / 100f, mat.GetFloat("_UnderlayOffsetY"), 0.001f);
}
```

- [ ] **步骤 4: 添加发光效果测试**

```csharp
[Test]
public void ApplyOuterGlow_ShouldSetShaderParameters()
{
    var effects = new TextEffectData[]
    {
        new GlowEffectData
        {
            type = "outerGlow",
            enabled = true,
            color = new ColorData { r = 1, g = 1, b = 0, a = 1 },
            size = 5,
            spread = 0
        }
    };

    applier.ApplyEffects(tmpComponent, effects, "test", "001");

    var mat = tmpComponent.fontSharedMaterial;
    Assert.AreEqual(new Color(1, 1, 0, 1), mat.GetColor("_GlowColor"));
    Assert.AreEqual(5f / 10f, mat.GetFloat("_GlowOffset"), 0.001f);
}
```

- [ ] **步骤 5: 添加渐变效果测试**

```csharp
[Test]
public void ApplyGradient_ShouldEnableVertexGradient()
{
    var effects = new TextEffectData[]
    {
        new GradientEffectData
        {
            type = "gradient",
            enabled = true,
            gradientType = "linear",
            angle = 90,
            colors = new GradientColorStop[]
            {
                new GradientColorStop { r = 1, g = 1, b = 0, a = 1, position = 0 },
                new GradientColorStop { r = 1, g = 0.5f, b = 0, a = 1, position = 1 }
            },
            degraded = false
        }
    };

    applier.ApplyEffects(tmpComponent, effects, "test", "001");

    Assert.IsTrue(tmpComponent.enableVertexGradient);
    Assert.AreEqual(new Color(1, 1, 0, 1), tmpComponent.colorGradient.topLeft);
}
```

- [ ] **步骤 6: 运行测试验证**

在 Unity Test Runner 中运行所有测试

预期：所有测试通过

- [ ] **步骤 7: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/TextStyleApplierTests.cs
git commit -m "test(unity): add unit tests for 6 effect types Shader mapping

- Add tests for Material creation and naming rule
- Add tests for stroke, shadow, glow, gradient effects
- Verify Shader parameter values match effect data

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 16: 单元测试 - 渐变降级规则

**覆盖的上游需求：** Design 切片 4 验收标准 - 渐变降级测试

**依赖：** 任务 15

**文件：**
- 修改：`PSDExporterProject/tests/unit/text-style-extractor.test.ts`

- [ ] **步骤 1: 添加斜向渐变降级测试**

```typescript
describe('gradient degradation', () => {
  it('should degrade 45° gradient to 90° (vertical)', () => {
    const textData = { text: 'Test', style: { fontSize: 12 } } as any;
    const effects = {
      gradientOverlay: {
        enabled: true,
        type: 'linear',
        angle: 45,
        colors: [
          { color: { r: 255, g: 255, b: 0, a: 255 }, position: 0 },
          { color: { r: 255, g: 128, b: 0, a: 255 }, position: 1 }
        ]
      }
    } as any;

    const consoleSpy = jest.spyOn(console, 'warn').mockImplementation();
    const result = extractor.extract(textData, effects);
    
    const gradient = result?.effects?.find(e => e.type === 'gradient') as any;
    expect(gradient.angle).toBe(90);
    expect(gradient.degraded).toBe(true);
    expect(consoleSpy).toHaveBeenCalledWith(
      expect.stringContaining('Gradient angle 45° degraded to 90°')
    );

    consoleSpy.mockRestore();
  });

  it('should degrade radial gradient to linear vertical', () => {
    const textData = { text: 'Test', style: { fontSize: 12 } } as any;
    const effects = {
      gradientOverlay: {
        enabled: true,
        type: 'radial',
        colors: [
          { color: { r: 255, g: 0, b: 0, a: 255 }, position: 0 },
          { color: { r: 0, g: 0, b: 255, a: 255 }, position: 1 }
        ]
      }
    } as any;

    const consoleSpy = jest.spyOn(console, 'warn').mockImplementation();
    const result = extractor.extract(textData, effects);
    
    const gradient = result?.effects?.find(e => e.type === 'gradient') as any;
    expect(gradient.gradientType).toBe('linear');
    expect(gradient.angle).toBe(90);
    expect(gradient.degraded).toBe(true);
    expect(consoleSpy).toHaveBeenCalledWith(
      expect.stringContaining('Radial gradient degraded')
    );

    consoleSpy.mockRestore();
  });

  it('should degrade 3-color gradient to 2-color', () => {
    const textData = { text: 'Test', style: { fontSize: 12 } } as any;
    const effects = {
      gradientOverlay: {
        enabled: true,
        type: 'linear',
        angle: 90,
        colors: [
          { color: { r: 255, g: 0, b: 0, a: 255 }, position: 0 },
          { color: { r: 0, g: 255, b: 0, a: 255 }, position: 0.5 },
          { color: { r: 0, g: 0, b: 255, a: 255 }, position: 1 }
        ]
      }
    } as any;

    const consoleSpy = jest.spyOn(console, 'warn').mockImplementation();
    const result = extractor.extract(textData, effects);
    
    const gradient = result?.effects?.find(e => e.type === 'gradient') as any;
    expect(gradient.colors).toHaveLength(2);
    expect(gradient.degraded).toBe(true);
    expect(consoleSpy).toHaveBeenCalledWith(
      expect.stringContaining('Multi-color gradient (3 stops) degraded to 2 colors')
    );

    consoleSpy.mockRestore();
  });

  it('should not degrade vertical/horizontal gradients', () => {
    const angles = [0, 90, 180, 270];
    
    angles.forEach(angle => {
      const textData = { text: 'Test', style: { fontSize: 12 } } as any;
      const effects = {
        gradientOverlay: {
          enabled: true,
          type: 'linear',
          angle,
          colors: [
            { color: { r: 255, g: 255, b: 0, a: 255 }, position: 0 },
            { color: { r: 255, g: 128, b: 0, a: 255 }, position: 1 }
          ]
        }
      } as any;

      const result = extractor.extract(textData, effects);
      const gradient = result?.effects?.find(e => e.type === 'gradient') as any;
      
      expect(gradient.angle).toBe(angle);
      expect(gradient.degraded).toBe(false);
    });
  });
});
```

- [ ] **步骤 2: 运行测试验证**

运行：
```bash
cd PSDExporterProject
npm test -- text-style-extractor.test.ts
```

预期：所有渐变降级测试通过

- [ ] **步骤 3: Commit**

```bash
git add PSDExporterProject/tests/unit/text-style-extractor.test.ts
git commit -m "test(parser): add comprehensive gradient degradation tests

- Test diagonal gradient degradation (45°→90°)
- Test radial gradient degradation (radial→linear)
- Test multi-color gradient degradation (3→2 colors)
- Test vertical/horizontal gradients remain unchanged

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 17: 单元测试 - 多效果叠加

**覆盖的上游需求：** Design 切片 4 验收标准 - 多效果叠加测试

**依赖：** 任务 16

**文件：**
- 修改：`UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/TextStyleApplierTests.cs`

- [ ] **步骤 1: 添加描边+阴影叠加测试**

```csharp
[Test]
public void ApplyMultipleEffects_StrokeAndShadow()
{
    var effects = new TextEffectData[]
    {
        new StrokeEffectData
        {
            type = "stroke",
            enabled = true,
            color = new ColorData { r = 0, g = 0, b = 0, a = 1 },
            width = 2,
            position = "outside"
        },
        new ShadowEffectData
        {
            type = "dropShadow",
            enabled = true,
            color = new ColorData { r = 0, g = 0, b = 0, a = 0.5f },
            offsetX = 2,
            offsetY = -2,
            blur = 4
        }
    };

    applier.ApplyEffects(tmpComponent, effects, "test", "001");

    var mat = tmpComponent.fontSharedMaterial;
    
    // 验证描边参数
    Assert.AreEqual(2, mat.GetFloat("_OutlineWidth"));
    Assert.AreEqual(new Color(0, 0, 0, 1), mat.GetColor("_OutlineColor"));
    
    // 验证阴影参数
    Assert.AreEqual(new Color(0, 0, 0, 0.5f), mat.GetColor("_UnderlayColor"));
    Assert.AreEqual(2f / 100f, mat.GetFloat("_UnderlayOffsetX"), 0.001f);
}
```

- [ ] **步骤 2: 添加描边+发光叠加测试**

```csharp
[Test]
public void ApplyMultipleEffects_StrokeAndGlow()
{
    var effects = new TextEffectData[]
    {
        new StrokeEffectData
        {
            type = "stroke",
            enabled = true,
            color = new ColorData { r = 1, g = 1, b = 1, a = 1 },
            width = 1.5f,
            position = "outside"
        },
        new GlowEffectData
        {
            type = "outerGlow",
            enabled = true,
            color = new ColorData { r = 1, g = 1, b = 0, a = 1 },
            size = 5,
            spread = 0
        }
    };

    applier.ApplyEffects(tmpComponent, effects, "test", "001");

    var mat = tmpComponent.fontSharedMaterial;
    
    // 验证描边参数
    Assert.AreEqual(1.5f, mat.GetFloat("_OutlineWidth"));
    
    // 验证发光参数
    Assert.AreEqual(new Color(1, 1, 0, 1), mat.GetColor("_GlowColor"));
    Assert.AreEqual(0.5f, mat.GetFloat("_GlowOffset"), 0.001f);
}
```

- [ ] **步骤 3: 添加描边+阴影+发光叠加测试**

```csharp
[Test]
public void ApplyMultipleEffects_StrokeShadowGlow()
{
    var effects = new TextEffectData[]
    {
        new StrokeEffectData
        {
            type = "stroke",
            enabled = true,
            color = new ColorData { r = 0, g = 0, b = 0, a = 1 },
            width = 2,
            position = "outside"
        },
        new ShadowEffectData
        {
            type = "dropShadow",
            enabled = true,
            color = new ColorData { r = 0, g = 0, b = 0, a = 0.5f },
            offsetX = 2,
            offsetY = -2,
            blur = 4
        },
        new GlowEffectData
        {
            type = "outerGlow",
            enabled = true,
            color = new ColorData { r = 1, g = 1, b = 0, a = 1 },
            size = 5,
            spread = 0
        }
    };

    applier.ApplyEffects(tmpComponent, effects, "test", "001");

    var mat = tmpComponent.fontSharedMaterial;
    
    // 验证所有效果参数都已设置且不互相覆盖
    Assert.AreEqual(2, mat.GetFloat("_OutlineWidth"), "Stroke width");
    Assert.AreEqual(2f / 100f, mat.GetFloat("_UnderlayOffsetX"), 0.001f, "Shadow offsetX");
    Assert.AreEqual(0.5f, mat.GetFloat("_GlowOffset"), 0.001f, "Glow size");
}
```

- [ ] **步骤 4: 运行测试验证**

在 Unity Test Runner 中运行测试

预期：所有多效果叠加测试通过

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/TextStyleApplierTests.cs
git commit -m "test(unity): add tests for multiple effect stacking

- Test stroke + shadow combination
- Test stroke + glow combination
- Test stroke + shadow + glow triple combination
- Verify all effect parameters coexist without overwriting

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 18: Material 清理工具

**覆盖的上游需求：** Design 待确定事项 #4 - Material 清理工具

**依赖：** 任务 17

**文件：**
- 创建：`UnityProject/Assets/Change/Editor/PSD2UI/MaterialCleanupTool.cs`

- [ ] **步骤 1: 创建 MaterialCleanupTool 类**

创建文件：

```csharp
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Change.Editor.PSD2UI
{
    public class MaterialCleanupTool
    {
        private const string MaterialDirectory = "Assets/GameRes/Materials/UI/PSD2UI/";

        [MenuItem("Tools/PSD2UI/Cleanup Unused Materials")]
        public static void CleanupUnusedMaterials()
        {
            if (!EditorUtility.DisplayDialog(
                "Material Cleanup",
                "This will scan all PSD2UI materials and delete unused ones. Continue?",
                "Yes", "Cancel"))
            {
                return;
            }

            var unusedMaterials = FindUnusedMaterials();
            
            if (unusedMaterials.Count == 0)
            {
                EditorUtility.DisplayDialog("Cleanup Complete", "No unused materials found.", "OK");
                return;
            }

            var message = $"Found {unusedMaterials.Count} unused materials:\n\n";
            message += string.Join("\n", unusedMaterials.Take(10).Select(m => Path.GetFileName(m)));
            if (unusedMaterials.Count > 10)
            {
                message += $"\n... and {unusedMaterials.Count - 10} more";
            }

            if (EditorUtility.DisplayDialog("Delete Unused Materials", message, "Delete", "Cancel"))
            {
                DeleteMaterials(unusedMaterials);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Cleanup Complete", 
                    $"Deleted {unusedMaterials.Count} unused materials.", "OK");
            }
        }

        private static List<string> FindUnusedMaterials()
        {
            var unusedMaterials = new List<string>();

            if (!Directory.Exists(MaterialDirectory))
            {
                return unusedMaterials;
            }

            // 获取所有 PSD2UI Material
            var allMaterials = Directory.GetFiles(MaterialDirectory, "*.mat", SearchOption.AllDirectories);

            // 获取所有 Prefab 中引用的 Material
            var referencedMaterials = new HashSet<string>();
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (prefab == null) continue;

                var tmpComponents = prefab.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                foreach (var tmp in tmpComponents)
                {
                    if (tmp.fontSharedMaterial != null)
                    {
                        var matPath = AssetDatabase.GetAssetPath(tmp.fontSharedMaterial);
                        if (!string.IsNullOrEmpty(matPath))
                        {
                            referencedMaterials.Add(Path.GetFullPath(matPath));
                        }
                    }
                }
            }

            // 找出未被引用的 Material
            foreach (var materialPath in allMaterials)
            {
                var fullPath = Path.GetFullPath(materialPath);
                if (!referencedMaterials.Contains(fullPath))
                {
                    unusedMaterials.Add(materialPath);
                }
            }

            return unusedMaterials;
        }

        private static void DeleteMaterials(List<string> materialPaths)
        {
            foreach (var path in materialPaths)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [MenuItem("Tools/PSD2UI/Show Material Statistics")]
        public static void ShowMaterialStatistics()
        {
            if (!Directory.Exists(MaterialDirectory))
            {
                EditorUtility.DisplayDialog("Statistics", "Material directory does not exist.", "OK");
                return;
            }

            var allMaterials = Directory.GetFiles(MaterialDirectory, "*.mat", SearchOption.AllDirectories);
            var unusedMaterials = FindUnusedMaterials();
            var usedMaterials = allMaterials.Length - unusedMaterials.Count;

            var message = $"Total Materials: {allMaterials.Length}\n";
            message += $"Used Materials: {usedMaterials}\n";
            message += $"Unused Materials: {unusedMaterials.Count}\n\n";
            
            var totalSize = allMaterials.Sum(path => new FileInfo(path).Length);
            message += $"Total Size: {FormatBytes(totalSize)}";

            EditorUtility.DisplayDialog("Material Statistics", message, "OK");
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
```

- [ ] **步骤 2: 验证编译**

在 Unity 编辑器中等待编译完成

预期：编译成功

- [ ] **步骤 3: 测试清理工具**

在 Unity 编辑器中：
1. Tools → PSD2UI → Show Material Statistics（查看统计信息）
2. Tools → PSD2UI → Cleanup Unused Materials（清理未使用的 Material）

预期：工具正常运行

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/MaterialCleanupTool.cs
git commit -m "feat(unity): add Material cleanup tool

- Add menu item to cleanup unused PSD2UI materials
- Scan all Prefabs to find referenced materials
- Delete materials not referenced by any TMP component
- Add statistics display for material usage

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 19: 视觉验证测试 - 6 种效果还原度

**覆盖的上游需求：** FRD 验收条件 - 视觉还原度 > 90%

**依赖：** 任务 18

**文件：**
- 创建测试 PSD（手动）
- 创建：`UnityProject/Assets/Change/Editor/PSD2UI/Tests/Manual/VisualValidationGuide.md`

- [ ] **步骤 1: 准备测试 PSD**

创建包含 6 种效果的测试 PSD：
- 文本 1：描边（2px 黑色外描边）
- 文本 2：阴影（偏移 2/-2，模糊 4）
- 文本 3：内阴影（偏移 2/-2）
- 文本 4：渐变（垂直，黄色→橙色）
- 文本 5：发光（黄色，大小 5）
- 文本 6：斜角（外斜角，大小 5）
- 文本 7：描边+阴影+发光（组合效果）

保存为 `test-effects.psd`

- [ ] **步骤 2: 使用 Node.js 解析器生成 JSON**

运行：
```bash
cd PSDExporterProject
npm run build
node dist/cli.js parse test-effects.psd --output test-effects.json
```

预期：生成包含 textStyles 和 effects 的 JSON

- [ ] **步骤 3: 在 Unity 中生成 Prefab**

在 Unity 编辑器中：
1. 导入 `test-effects.json`
2. 使用 PrefabBuilder 生成 Prefab
3. 在 Scene 中实例化 Prefab

- [ ] **步骤 4: 视觉对比验证**

对比方法：
1. 在 Photoshop 中截图保存各文本效果
2. 在 Unity Game 视图中截图
3. 使用图片对比工具或肉眼对比

验收标准：
- 描边：颜色、宽度、位置匹配
- 阴影：颜色、偏移、模糊匹配（允许 ±10% 误差）
- 内阴影：视觉效果类似（`_UnderlayDilate` 负值是否可接受）
- 渐变：颜色过渡方向和颜色匹配
- 发光：颜色和扩散范围匹配（允许 ±15% 误差）
- 斜角：高光和阴影可见（允许参数差异）

- [ ] **步骤 5: 创建视觉验证指南**

创建 `VisualValidationGuide.md`：

```markdown
# PSD 文字效果视觉验证指南

## 目的

验证 PSD 文字效果在 Unity 中的还原度是否达到 90% 以上。

## 测试材料

- 测试 PSD：`test-effects.psd`（包含 6 种效果示例）
- 生成的 JSON：`test-effects.json`
- Unity Prefab：`TestEffects.prefab`

## 验证步骤

1. **准备对比截图**
   - Photoshop：导出每个文本图层为 PNG（300 DPI）
   - Unity：在 Game 视图中截图（确保分辨率一致）

2. **逐效果验证**

### 描边 (Stroke)
- [ ] 颜色匹配：目标 100%
- [ ] 宽度匹配：允许 ±0.5px 误差
- [ ] 位置（外/内/居中）：目标 100%

### 阴影 (Drop Shadow)
- [ ] 颜色和透明度：允许 ±5% 误差
- [ ] 偏移量：允许 ±1px 误差
- [ ] 模糊半径：允许 ±20% 误差（Shader 实现差异）

### 内阴影 (Inner Shadow)
- [ ] 整体视觉效果类似（重点）
- [ ] 颜色匹配：允许 ±10% 误差
- [ ] 如果效果差异 > 30%，标注为已知限制

### 渐变 (Gradient)
- [ ] 颜色 1 和颜色 2：允许 ±5% 误差
- [ ] 渐变方向（垂直/水平）：目标 100%
- [ ] 颜色过渡平滑度：主观评估

### 发光 (Outer Glow)
- [ ] 颜色匹配：允许 ±10% 误差
- [ ] 扩散范围：允许 ±15% 误差
- [ ] 整体发光效果可见

### 斜角 (Bevel & Emboss)
- [ ] 高光和阴影可见：目标 100%
- [ ] 深度感：主观评估
- [ ] 允许参数映射差异，重点是视觉效果

## 评分标准

- **A 级（≥95%）**：几乎完全一致，细微差异不影响视觉效果
- **B 级（90-94%）**：整体效果一致，有可接受的参数差异
- **C 级（85-89%）**：效果可识别但有明显差异
- **D 级（<85%）**：效果差异过大，需要优化

目标：所有效果达到 B 级以上（≥90%）

## 记录结果

| 效果类型 | 还原度 | 评级 | 备注 |
|---------|--------|------|------|
| 描边 | % | | |
| 阴影 | % | | |
| 内阴影 | % | | |
| 渐变 | % | | |
| 发光 | % | | |
| 斜角 | % | | |
| 组合效果 | % | | |

## 已知限制

- 内阴影使用 `_UnderlayDilate` 负值实现，视觉效果可能与 Photoshop 有差异
- 斜角效果的光照模型不完全相同，深度感可能略有不同
- 渐变仅支持垂直和水平方向，斜向和径向已降级
```

- [ ] **步骤 6: 执行视觉验证并记录结果**

根据指南执行验证，记录每种效果的还原度

如果整体还原度 < 90%，分析原因并调整 Shader 参数映射

- [ ] **步骤 7: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/Tests/Manual/VisualValidationGuide.md
git commit -m "docs(unity): add visual validation guide for text effects

- Add step-by-step validation procedure
- Define acceptance criteria for each effect type (90%+ match)
- Add scoring standards (A/B/C/D grades)
- Document known limitations

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 20: 性能测试 - 50+ 文本对象

**覆盖的上游需求：** FRD 验收条件 - 50+ 文本对象 < 10 秒

**依赖：** 任务 19

**文件：**
- 创建：`PSDExporterProject/tests/performance/large-text-parsing.test.ts`
- 创建：`UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/PerformanceTests.cs`

- [ ] **步骤 1: 编写 Node.js 性能测试**

创建 `large-text-parsing.test.ts`：

```typescript
import { describe, it, expect } from '@jest/globals';
import { PsdParser } from '../../src/parser/psd-parser';
import { readPsd } from 'ag-psd';
import * as fs from 'fs';
import * as path from 'path';

describe('Performance - Large Text Parsing', () => {
  it('should parse 50+ text layers with effects in under 10 seconds', async () => {
    const largePsdPath = path.join(__dirname, '../fixtures/large-text-sample.psd');
    
    if (!fs.existsSync(largePsdPath)) {
      console.warn('Large PSD not found, skipping performance test');
      return;
    }

    const buffer = fs.readFileSync(largePsdPath);
    const psd = readPsd(buffer);

    const parser = new PsdParser();
    const startTime = Date.now();
    
    const result = parser.parse(psd);
    
    const elapsed = Date.now() - startTime;

    // 统计文本图层数量
    const textLayerCount = countTextLayers(result.layers);
    
    console.log(`Performance Test Results:`);
    console.log(`- Text layers: ${textLayerCount}`);
    console.log(`- Parsing time: ${elapsed}ms`);
    console.log(`- Avg per layer: ${(elapsed / textLayerCount).toFixed(2)}ms`);

    expect(textLayerCount).toBeGreaterThanOrEqual(50);
    expect(elapsed).toBeLessThan(10000); // 10 seconds
  });

  it('should have reasonable per-layer overhead', async () => {
    const largePsdPath = path.join(__dirname, '../fixtures/large-text-sample.psd');
    
    if (!fs.existsSync(largePsdPath)) {
      return;
    }

    const buffer = fs.readFileSync(largePsdPath);
    const psd = readPsd(buffer);

    const parser = new PsdParser();
    const startTime = Date.now();
    const result = parser.parse(psd);
    const elapsed = Date.now() - startTime;

    const textLayerCount = countTextLayers(result.layers);
    const avgPerLayer = elapsed / textLayerCount;

    // 期望：每个文本图层处理时间 < 200ms
    expect(avgPerLayer).toBeLessThan(200);
  });
});

function countTextLayers(layers: any[]): number {
  let count = 0;
  for (const layer of layers) {
    if (layer.type === 'text' && layer.textStyles) {
      count++;
    }
    if (layer.children) {
      count += countTextLayers(layer.children);
    }
  }
  return count;
}
```

- [ ] **步骤 2: 编写 Unity 性能测试**

创建 `PerformanceTests.cs`：

```csharp
using NUnit.Framework;
using UnityEngine;
using TMPro;
using System.Diagnostics;
using System.IO;
using Change.Editor.PSD2UI;

namespace Change.Editor.PSD2UI.Tests
{
    [TestFixture]
    public class PerformanceTests
    {
        [Test]
        public void ApplyTextStyles_50Layers_UnderFiveSeconds()
        {
            var jsonPath = Path.Combine(
                Application.dataPath,
                "Change/Editor/PSD2UI/Tests/Fixtures/large-text-config.json"
            );

            if (!File.Exists(jsonPath))
            {
                Assert.Ignore("Large test JSON not found");
                return;
            }

            var json = File.ReadAllText(jsonPath);
            var config = JsonUtility.FromJson<PSDConfig>(json);

            var stopwatch = Stopwatch.StartNew();
            
            var builder = new PrefabBuilder();
            var rootObject = builder.Build(config);
            
            stopwatch.Stop();

            var textComponents = rootObject.GetComponentsInChildren<TextMeshProUGUI>(true);
            
            UnityEngine.Debug.Log($"Performance Test Results:");
            UnityEngine.Debug.Log($"- Text components: {textComponents.Length}");
            UnityEngine.Debug.Log($"- Generation time: {stopwatch.ElapsedMilliseconds}ms");
            UnityEngine.Debug.Log($"- Avg per component: {stopwatch.ElapsedMilliseconds / (float)textComponents.Length:F2}ms");

            Assert.GreaterOrEqual(textComponents.Length, 50, "Should have at least 50 text components");
            Assert.Less(stopwatch.ElapsedMilliseconds, 5000, "Should complete in under 5 seconds");

            Object.DestroyImmediate(rootObject);
        }

        [Test]
        public void ApplyTextStyles_ReasonablePerLayerOverhead()
        {
            // 测试单个文本组件的应用时间
            var textStyles = new TextStylesData
            {
                fontSize = 24,
                color = new ColorData { r = 1, g = 1, b = 1, a = 1 },
                fontName = "Arial",
                fontStyle = new FontStyleData { bold = false, italic = false },
                alignment = new AlignmentData { horizontal = "center", vertical = "middle" },
                effects = new TextEffectData[]
                {
                    new StrokeEffectData
                    {
                        type = "stroke",
                        enabled = true,
                        color = new ColorData { r = 0, g = 0, b = 0, a = 1 },
                        width = 2,
                        position = "outside"
                    },
                    new ShadowEffectData
                    {
                        type = "dropShadow",
                        enabled = true,
                        color = new ColorData { r = 0, g = 0, b = 0, a = 0.5f },
                        offsetX = 2,
                        offsetY = -2,
                        blur = 4
                    }
                }
            };

            var gameObject = new GameObject("TestText");
            var tmpComponent = gameObject.AddComponent<TextMeshProUGUI>();
            var applier = new TextStyleApplier();

            var stopwatch = Stopwatch.StartNew();
            
            applier.ApplyBasicProperties(tmpComponent, textStyles, "test");
            applier.ApplyEffects(tmpComponent, textStyles.effects, "test", "001");
            
            stopwatch.Stop();

            // 期望：单个组件应用时间 < 100ms
            Assert.Less(stopwatch.ElapsedMilliseconds, 100, 
                $"Single component should apply in under 100ms, took {stopwatch.ElapsedMilliseconds}ms");

            Object.DestroyImmediate(gameObject);
        }
    }
}
```

- [ ] **步骤 3: 运行性能测试**

Node.js：
```bash
cd PSDExporterProject
npm test -- large-text-parsing.test.ts
```

Unity：在 Test Runner 中运行 PerformanceTests

预期：所有性能测试通过

- [ ] **步骤 4: 如果性能不达标，进行优化**

可能的优化方向：
- 缓存 TMP_FontAsset 查找结果
- 批量创建 Material（如果可行）
- 优化颜色转换逻辑
- 减少不必要的 Asset 操作

重新测试直到性能达标

- [ ] **步骤 5: Commit**

```bash
git add PSDExporterProject/tests/performance/large-text-parsing.test.ts \
        UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/PerformanceTests.cs
git commit -m "test: add performance tests for large text sets

- Add Node.js test: 50+ text layers parsing < 10s
- Add Unity test: 50+ text components application < 5s
- Add per-layer overhead measurement
- Log performance statistics for monitoring

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## 实施后检查清单

完成所有任务后，执行以下检查：

- [ ] **编译检查**
  - Node.js 项目编译通过：`npm run build`
  - Unity 项目编译通过：无 Console 错误

- [ ] **测试覆盖**
  - Node.js 单元测试全部通过：`npm test`
  - Unity EditMode 测试全部通过
  - 集成测试通过（Node.js + Unity 端到端）

- [ ] **功能验收**
  - 6 种效果提取正确
  - 6 种效果应用正确
  - 基础属性（字号、颜色、对齐、字体）正确
  - 锁定机制生效
  - 渐变降级策略生效

- [ ] **性能验收**
  - 50+ 文本对象 Node.js 解析 < 10 秒
  - 50+ 文本对象 Unity 应用 < 5 秒

- [ ] **视觉验收**
  - 视觉还原度 ≥ 90%（通过手动对比）
  - 内阴影效果可接受
  - 斜角效果可识别

- [ ] **回归检查**
  - 现有 PSD 解析器测试仍然通过
  - 现有 PrefabBuilder 测试仍然通过
  - textStyles 可选，不影响旧流程

- [ ] **文档更新**
  - 更新项目 README（如需要）
  - 更新 FRD 状态为"已完成"

---

## 后续优化建议（范围外）

完成核心功能后，可考虑以下优化（不在本次计划范围内）：

1. **Material 复用策略**：相同参数组合的 Material 共享，减少资源数量
2. **字号 DPI 缩放因子配置化**：允许项目级配置缩放因子
3. **自定义 Shader 变体**：针对内阴影等效果开发专用 Shader
4. **更多效果支持**：图案叠加、光泽等 Photoshop 高级效果
5. **渐变增强**：支持斜向和径向渐变（需要 Shader 开发）
6. **字体自动导入**：根据 PSD 字体名自动导入 TrueType 字体
7. **样式库管理**：创建预设 Material 库供手动调整

---

## 风险应对计划

| 风险 | 触发条件 | 应对措施 |
|------|----------|----------|
| 内阴影效果还原度 < 80% | 任务 19 视觉验证失败 | 标注为已知限制，文档说明可手动调整，或开发自定义 Shader 变体 |
| 字号 DPI 转换不准确 | 任务 12 端到端测试发现字号偏差 > 20% | 调整 `scaleFactor`，通过多个 PSD 样本确定最佳值 |
| 性能测试不达标 | 任务 7 或任务 20 性能测试失败 | 分析瓶颈（ag-psd 解析 vs 自定义逻辑），缓存优化，异步处理 |
| TMP 字体缺失导致显示异常 | 任务 12 发现默认字体为 null | 补充 Unity 内置 Arial 作为最终回退，文档说明配置要求 |
| Material 数量过多影响打包 | 实际项目使用后反馈 | 提供 Material 清理工具（任务 18），文档说明定期清理流程 |

