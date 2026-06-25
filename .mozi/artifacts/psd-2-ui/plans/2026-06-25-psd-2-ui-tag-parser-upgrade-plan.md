# PSD 标签解析器升级 实现计划

> 日期: 2026-06-25 | 状态: 草稿
> 上游设计: [PSD 标签解析器升级架构设计](../designs/2026-06-25-psd-2-ui-tag-parser-upgrade-design.md)
> 上游 FRD: [PSD 标签解析器升级功能需求文档](../discover/2026-06-25-psd-2-ui-tag-parser-upgrade-frd.md)

## 上游产出引用

### 来自 Design

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 切片 A: 类型定义与配置基础设施 | 任务 1-4 |
| 切片 B: 核心解析引擎 | 任务 5-9 |
| 切片 C: ComponentInfo 扩展与集成 | 任务 10-14 |
| 切片 D: 集成测试与边界验证 | 任务 15-17 |
| 切片 E: Unity C# 端组件扩展 | 任务 18-22 |
| 文件地图: 12 个文件（7 新增 + 5 修改） | 按任务分解到具体步骤 |
| 架构决策: 增量重构 + 类型分离 | TagParser 返回 TagParseResult，ComponentRecognizer 组装 ComponentInfo |

### 来自 FRD

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 验收条件: 39 个标签定义 | 任务 2 创建完整 tag-config.json |
| 验收条件: 多标签叠加、优先级、前缀 | 任务 6-8 单元测试覆盖 |
| 决策 #2: 点号后缀式标签 | 任务 5 实现右→左解析算法 |
| 决策 #9: ComponentInfo 扩展字段 | 任务 10 扩展 ComponentType 和 ComponentInfo |

## 目标

重构 TagParser 以完整兼容 PSD2UGUI-LayerTagMenu.jsx 的标签体系，从简化的前缀式标签升级为点号后缀式多层标签，支持 4-family 分类、多标签叠加、资源引用前缀，并同步扩展 Unity C# 端消费新字段。

## 架构

采用增量重构 + 类型分离模式。引入 TagParseResult 中间类型解耦解析和组装：TagParser 负责字符串解析返回 TagParseResult，ComponentRecognizer 负责将 TagParseResult 映射为 ComponentInfo。配置通过外部 JSON 文件维护，使用 zod 进行 schema 校验。Unity 端新增 ComponentInfo.cs 数据类并扩展 ComponentFactory 支持新组件类型和扩展属性。

## 技术栈

**TypeScript 端:**
- zod - 配置文件 schema 校验
- Jest - 单元测试和集成测试
- 现有: ag-psd, sharp, commander

**Unity C# 端:**
- Newtonsoft.Json - JSON 反序列化
- Unity Test Framework - 单元测试
- TextMeshPro - 文本组件

## 文件清单

| 文件 | 操作 | 职责 | 所属任务 |
|------|------|------|----------|
| `PSDExporterProject/config/tag-config.json` | 创建 | 39 个标签定义配置 | 任务 2 |
| `PSDExporterProject/src/recognizer/tag-parse-result.ts` | 创建 | TagParseResult 接口定义 | 任务 1 |
| `PSDExporterProject/src/recognizer/tag-config-loader.ts` | 创建 | 配置加载器（含 zod 校验） | 任务 3-4 |
| `PSDExporterProject/src/recognizer/tag-parser.ts` | 修改 | 重构解析逻辑（右→左算法） | 任务 5-8 |
| `PSDExporterProject/tests/recognizer/tag-parser.test.ts` | 创建 | TagParser 单元测试 | 任务 6-8 |
| `PSDExporterProject/src/recognizer/component-types.ts` | 修改 | 扩展 ComponentType 枚举和 ComponentInfo 接口 | 任务 10 |
| `PSDExporterProject/src/recognizer/component-recognizer.ts` | 修改 | 适配 TagParseResult → ComponentInfo | 任务 11-13 |
| `PSDExporterProject/src/generator/json-schema.ts` | 修改 | 扩展 ComponentInfoSchema | 任务 14 |
| `PSDExporterProject/tests/integration/tag-parser-integration.test.ts` | 创建 | 端到端集成测试 | 任务 15-17 |
| `UnityProject/Assets/Change/Editor/PSD2UI/ComponentInfo.cs` | 创建 | C# ComponentInfo 数据类 | 任务 18 |
| `UnityProject/Assets/Change/Editor/PSD2UI/ComponentFactory.cs` | 修改 | 扩展支持新组件类型和属性 | 任务 19-21 |
| `UnityProject/Assets/Change/Editor/PSD2UI/Tests/EditMode/ComponentFactoryTests.cs` | 创建 | ComponentFactory 单元测试 | 任务 22 |

## 任务依赖图

```
切片 A (任务 1-4): 类型定义与配置基础设施
  ↓
切片 B (任务 5-9): 核心解析引擎
  ↓
切片 C (任务 10-14): ComponentInfo 扩展与集成
  ├─→ 切片 D (任务 15-17): 集成测试与边界验证
  └─→ 切片 E (任务 18-22): Unity C# 端组件扩展
```

## 风险评估

| 风险（来源） | 影响 | 计划中的缓解措施 |
|-------------|------|-----------------|
| TagParser 接口变更破坏 ComponentRecognizer（Design 切片 B） | ComponentRecognizer 调用失败 | 任务 11-13 紧随任务 5-9，快速恢复兼容性 |
| 复杂解析算法边界情况（Design 切片 B） | 解析错误或遗漏 | 任务 6-8 编写 15-20 个单元测试覆盖所有场景 |
| JSON Schema 变更影响 Unity 端（Design 切片 C） | Unity 反序列化失败 | 任务 14 确保新字段可选，任务 22 验证兼容性 |
| 现有测试用例依赖旧标签格式（Design 回归评估） | 测试失败 | 任务 17 识别并更新受影响的测试 |

---

## 任务


### 任务 1: TagParseResult 接口定义

**覆盖的上游需求:** Design 切片 A - TagParseResult 类型定义

**文件:**
- 创建: `PSDExporterProject/src/recognizer/tag-parse-result.ts`

- [ ] **步骤 1: 创建 TagParseResult 接口文件**

```typescript
// PSDExporterProject/src/recognizer/tag-parse-result.ts

/**
 * 标签解析结果
 * 包含解析后的前缀、基础名称和分类标签
 */
export interface TagParseResult {
  /** 资源引用前缀 (ref 或 refp) */
  prefix?: 'ref' | 'refp';
  
  /** 图层基础名称（去除标签后的部分） */
  baseName: string;
  
  /** 4-family 分类标签 */
  families: {
    /** 主组件类型 (bt, img, txt, etc.) */
    main?: string;
    
    /** 文本后端 (tmp, ugui) */
    textBackend?: string;
    
    /** 图片类型 (simple, sliced, tiled, filled) */
    imageType?: string;
    
    /** 角色标签 (bg, press, placeholder, etc.) */
    role?: string;
  };
}
```

- [ ] **步骤 2: 验证 TypeScript 编译**

运行: `cd PSDExporterProject && npm run build`
预期: 编译成功，无类型错误

- [ ] **步骤 3: Commit**

```bash
git add PSDExporterProject/src/recognizer/tag-parse-result.ts
git commit -m "feat(recognizer): add TagParseResult interface

- Define TagParseResult with prefix, baseName, and families
- Support 4-family classification (main, textBackend, imageType, role)
- Add JSDoc comments for clarity

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 2: tag-config.json 配置文件

**覆盖的上游需求:** Design 切片 A - 39 个标签定义，FRD 验收条件

**文件:**
- 创建: `PSDExporterProject/config/tag-config.json`

- [ ] **步骤 1: 创建配置文件目录**

```bash
mkdir -p PSDExporterProject/config
```

- [ ] **步骤 2: 创建 tag-config.json**

```json
{
  "version": "1.0.0",
  "canonicalOrder": ["main", "textBackend", "imageType", "role"],
  "families": {
    "main": [
      {"id": "img", "label": "Image"},
      {"id": "rimg", "label": "RawImage"},
      {"id": "txt", "label": "Text"},
      {"id": "msk", "label": "Mask"},
      {"id": "col", "label": "FillColor"},
      {"id": "bt", "label": "Button"},
      {"id": "dpd", "label": "Dropdown"},
      {"id": "ipt", "label": "InputField"},
      {"id": "tg", "label": "Toggle"},
      {"id": "sld", "label": "Slider"},
      {"id": "sv", "label": "ScrollView"}
    ],
    "textBackend": [
      {"id": "tmp", "label": "TextMeshPro"},
      {"id": "ugui", "label": "UGUI"}
    ],
    "imageType": [
      {"id": "simple", "label": "Simple"},
      {"id": "sliced", "label": "Sliced"},
      {"id": "tiled", "label": "Tiled"},
      {"id": "filled", "label": "Filled"}
    ],
    "role": [
      {"id": "bg", "label": "Background"},
      {"id": "onover", "label": "Button_Highlight"},
      {"id": "press", "label": "Button_Press"},
      {"id": "select", "label": "Button_Select"},
      {"id": "disable", "label": "Button_Disable"},
      {"id": "bttxt", "label": "Button_Text"},
      {"id": "dpdlb", "label": "Dropdown_Label"},
      {"id": "dpdicon", "label": "Dropdown_Arrow"},
      {"id": "placeholder", "label": "InputField_Placeholder"},
      {"id": "ipttxt", "label": "InputField_Text"},
      {"id": "mark", "label": "Toggle_Checkmark"},
      {"id": "tglb", "label": "Toggle_Label"},
      {"id": "fill", "label": "Slider_Fill"},
      {"id": "handle", "label": "Slider_Handle"},
      {"id": "vpt", "label": "ScrollView_Viewport"},
      {"id": "hbarbg", "label": "ScrollView_HorizontalBarBG"},
      {"id": "hbar", "label": "ScrollView_HorizontalBar"},
      {"id": "vbarbg", "label": "ScrollView_VerticalBarBG"},
      {"id": "vbar", "label": "ScrollView_VerticalBar"},
      {"id": "content", "label": "ScrollView_Content"},
      {"id": "item", "label": "ScrollView_Item"},
      {"id": "template", "label": "Dropdown_Template"}
    ]
  }
}
```

- [ ] **步骤 3: 验证 JSON 格式**

运行: `cat PSDExporterProject/config/tag-config.json | jq .`
预期: JSON 格式正确，无语法错误

- [ ] **步骤 4: 统计标签数量**

运行: `cat PSDExporterProject/config/tag-config.json | jq '[.families.main[], .families.textBackend[], .families.imageType[], .families.role[]] | length'`
预期: 输出 `39`（11 + 2 + 4 + 22）

- [ ] **步骤 5: Commit**

```bash
git add PSDExporterProject/config/tag-config.json
git commit -m "feat(config): add tag-config.json with 39 tag definitions

- Define 11 main family tags (bt, img, txt, etc.)
- Define 2 textBackend tags (tmp, ugui)
- Define 4 imageType tags (simple, sliced, tiled, filled)
- Define 22 role tags (bg, press, placeholder, etc.)
- Add version and canonicalOrder fields

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---


### 任务 3: TagConfigLoader 测试（TDD）

**覆盖的上游需求:** Design 切片 A - 配置加载器，含 zod 校验

**文件:**
- 创建: `PSDExporterProject/tests/recognizer/tag-config-loader.test.ts`

- [ ] **步骤 1: 安装 zod 依赖**

```bash
cd PSDExporterProject && npm install zod
```

- [ ] **步骤 2: 编写失败的测试**

```typescript
// PSDExporterProject/tests/recognizer/tag-config-loader.test.ts
import { TagConfigLoader } from '../../src/recognizer/tag-config-loader';
import * as path from 'path';

describe('TagConfigLoader', () => {
  const validConfigPath = path.resolve(__dirname, '../../config/tag-config.json');
  const missingConfigPath = path.resolve(__dirname, '../../config/nonexistent.json');

  describe('load', () => {
    it('should load valid config file successfully', () => {
      const config = TagConfigLoader.load(validConfigPath);
      
      expect(config).toBeDefined();
      expect(config.version).toBe('1.0.0');
      expect(config.families.main).toHaveLength(11);
      expect(config.families.textBackend).toHaveLength(2);
      expect(config.families.imageType).toHaveLength(4);
      expect(config.families.role).toHaveLength(22);
    });

    it('should throw error when config file not found', () => {
      expect(() => {
        TagConfigLoader.load(missingConfigPath);
      }).toThrow('Config file not found');
    });

    it('should throw error when config file has invalid format', () => {
      const invalidPath = path.resolve(__dirname, '../fixtures/invalid-config.json');
      expect(() => {
        TagConfigLoader.load(invalidPath);
      }).toThrow();
    });
  });

  describe('validate', () => {
    it('should validate correct config data', () => {
      const validData = {
        version: '1.0.0',
        canonicalOrder: ['main'],
        families: {
          main: [{ id: 'bt', label: 'Button' }],
          textBackend: [],
          imageType: [],
          role: []
        }
      };

      const config = TagConfigLoader.validate(validData);
      expect(config).toEqual(validData);
    });

    it('should reject config missing version field', () => {
      const invalidData = {
        families: { main: [], textBackend: [], imageType: [], role: [] }
      };

      expect(() => {
        TagConfigLoader.validate(invalidData);
      }).toThrow();
    });

    it('should reject config with invalid tag structure', () => {
      const invalidData = {
        version: '1.0.0',
        canonicalOrder: ['main'],
        families: {
          main: [{ id: 'bt' }], // missing label
          textBackend: [],
          imageType: [],
          role: []
        }
      };

      expect(() => {
        TagConfigLoader.validate(invalidData);
      }).toThrow();
    });
  });
});
```

- [ ] **步骤 3: 创建测试 fixture**

```bash
mkdir -p PSDExporterProject/tests/fixtures
```

```json
// PSDExporterProject/tests/fixtures/invalid-config.json
{
  "version": "1.0.0",
  "families": {
    "main": "invalid"
  }
}
```

- [ ] **步骤 4: 运行测试验证失败**

运行: `cd PSDExporterProject && npm test -- tag-config-loader.test.ts`
预期: 所有测试 FAIL，报错 "Cannot find module '../../src/recognizer/tag-config-loader'"

- [ ] **步骤 5: Commit**

```bash
git add PSDExporterProject/tests/recognizer/tag-config-loader.test.ts
git add PSDExporterProject/tests/fixtures/invalid-config.json
git commit -m "test(recognizer): add TagConfigLoader tests (TDD)

- Test successful config loading
- Test error handling for missing file
- Test schema validation for invalid formats
- Add test fixture for invalid config

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 4: TagConfigLoader 实现

**覆盖的上游需求:** Design 切片 A - 配置加载器实现

**依赖:** 任务 3

**文件:**
- 创建: `PSDExporterProject/src/recognizer/tag-config-loader.ts`

- [ ] **步骤 1: 编写实现代码**

```typescript
// PSDExporterProject/src/recognizer/tag-config-loader.ts
import { z } from 'zod';
import * as fs from 'fs';

/** 标签定义结构 */
const TagDefSchema = z.object({
  id: z.string(),
  label: z.string(),
});

/** 配置文件结构 */
const TagConfigSchema = z.object({
  version: z.string(),
  canonicalOrder: z.array(z.string()),
  families: z.object({
    main: z.array(TagDefSchema),
    textBackend: z.array(TagDefSchema),
    imageType: z.array(TagDefSchema),
    role: z.array(TagDefSchema),
  }),
});

export type TagConfig = z.infer<typeof TagConfigSchema>;
export type TagDef = z.infer<typeof TagDefSchema>;

/**
 * 标签配置加载器
 * 负责从 JSON 文件加载并校验标签配置
 */
export class TagConfigLoader {
  /**
   * 从文件路径加载配置
   * @param path 配置文件路径
   * @returns 校验通过的配置对象
   * @throws 文件不存在或格式错误时抛出异常
   */
  static load(path: string): TagConfig {
    if (!fs.existsSync(path)) {
      throw new Error(`Config file not found: ${path}`);
    }

    const content = fs.readFileSync(path, 'utf-8');
    let data: unknown;

    try {
      data = JSON.parse(content);
    } catch (error) {
      throw new Error(`Failed to parse config file: ${error instanceof Error ? error.message : String(error)}`);
    }

    return TagConfigLoader.validate(data);
  }

  /**
   * 校验配置数据
   * @param data 待校验的数据
   * @returns 校验通过的配置对象
   * @throws 数据格式错误时抛出 zod 异常
   */
  static validate(data: unknown): TagConfig {
    return TagConfigSchema.parse(data);
  }
}
```

- [ ] **步骤 2: 运行测试验证通过**

运行: `cd PSDExporterProject && npm test -- tag-config-loader.test.ts`
预期: 所有测试 PASS

- [ ] **步骤 3: 验证 TypeScript 编译**

运行: `cd PSDExporterProject && npm run build`
预期: 编译成功，无类型错误

- [ ] **步骤 4: Commit**

```bash
git add PSDExporterProject/src/recognizer/tag-config-loader.ts
git commit -m "feat(recognizer): implement TagConfigLoader with zod validation

- Load config from JSON file with error handling
- Validate config structure using zod schema
- Export TagConfig and TagDef types
- Throw clear errors for missing file or invalid format

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---


### 任务 5: TagParser 重构 - 解析算法测试（TDD）

**覆盖的上游需求:** Design 切片 B - 核心解析引擎，FRD 核心解析功能验收条件

**依赖:** 任务 1, 4

**文件:**
- 创建: `PSDExporterProject/tests/recognizer/tag-parser.test.ts`
- 修改: `PSDExporterProject/src/recognizer/tag-parser.ts`

- [ ] **步骤 1: 编写解析算法失败的测试**

```typescript
// PSDExporterProject/tests/recognizer/tag-parser.test.ts
import { TagParser } from '../../src/recognizer/tag-parser';
import { TagConfigLoader } from '../../src/recognizer/tag-config-loader';
import * as path from 'path';

describe('TagParser', () => {
  let parser: TagParser;

  beforeEach(() => {
    const configPath = path.resolve(__dirname, '../../config/tag-config.json');
    const config = TagConfigLoader.load(configPath);
    parser = new TagParser(config);
  });

  describe('parse - multi-tag stacking', () => {
    it('should parse close.bt.tmp.bg correctly', () => {
      const result = parser.parse('close.bt.tmp.bg');
      
      expect(result).not.toBeNull();
      expect(result!.baseName).toBe('close');
      expect(result!.families.main).toBe('bt');
      expect(result!.families.textBackend).toBe('tmp');
      expect(result!.families.role).toBe('bg');
      expect(result!.prefix).toBeUndefined();
    });

    it('should parse icon.img.sliced correctly', () => {
      const result = parser.parse('icon.img.sliced');
      
      expect(result).not.toBeNull();
      expect(result!.baseName).toBe('icon');
      expect(result!.families.main).toBe('img');
      expect(result!.families.imageType).toBe('sliced');
    });
  });

  describe('parse - priority override (right to left)', () => {
    it('should parse panel.bt.dpd with dpd overriding bt', () => {
      const result = parser.parse('panel.bt.dpd');
      
      expect(result).not.toBeNull();
      expect(result!.baseName).toBe('panel');
      expect(result!.families.main).toBe('dpd'); // rightmost wins
    });
  });

  describe('parse - prefix handling', () => {
    it('should parse ref icon.img with ref prefix', () => {
      const result = parser.parse('ref icon.img');
      
      expect(result).not.toBeNull();
      expect(result!.prefix).toBe('ref');
      expect(result!.baseName).toBe('icon');
      expect(result!.families.main).toBe('img');
    });

    it('should parse refp panel.bt with refp prefix', () => {
      const result = parser.parse('refp panel.bt');
      
      expect(result).not.toBeNull();
      expect(result!.prefix).toBe('refp');
      expect(result!.baseName).toBe('panel');
      expect(result!.families.main).toBe('bt');
    });

    it('should handle baseName with spaces after prefix', () => {
      const result = parser.parse('ref close button.bt.tmp');
      
      expect(result).not.toBeNull();
      expect(result!.prefix).toBe('ref');
      expect(result!.baseName).toBe('close button');
      expect(result!.families.main).toBe('bt');
      expect(result!.families.textBackend).toBe('tmp');
    });
  });

  describe('parse - unknown tag skipping', () => {
    it('should skip unknown tag and parse known tags', () => {
      const result = parser.parse('name.unknowntag.bt');
      
      expect(result).not.toBeNull();
      expect(result!.baseName).toBe('name');
      expect(result!.families.main).toBe('bt');
    });
  });

  describe('parse - no tag cases', () => {
    it('should return null for layer name without tags', () => {
      const result = parser.parse('background');
      expect(result).toBeNull();
    });

    it('should return null when all tags are unknown', () => {
      const result = parser.parse('name.xyz.abc');
      expect(result).toBeNull();
    });
  });

  describe('hasTag', () => {
    it('should return true for layer with valid tag', () => {
      expect(parser.hasTag('close.bt')).toBe(true);
    });

    it('should return false for layer without valid tag', () => {
      expect(parser.hasTag('background')).toBe(false);
    });
  });
});
```

- [ ] **步骤 2: 运行测试验证失败**

运行: `cd PSDExporterProject && npm test -- tag-parser.test.ts`
预期: 所有测试 FAIL

- [ ] **步骤 3: Commit 测试**

```bash
git add PSDExporterProject/tests/recognizer/tag-parser.test.ts
git commit -m "test(recognizer): add TagParser comprehensive tests (TDD)

- Test multi-tag stacking (close.bt.tmp.bg)
- Test right-to-left priority (panel.bt.dpd)
- Test ref/refp prefix handling
- Test unknown tag skipping
- Test no-tag cases returning null

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 6: TagParser 重构 - 核心解析实现

**覆盖的上游需求:** Design 切片 B - 右→左解析算法

**依赖:** 任务 5

**文件:**
- 修改: `PSDExporterProject/src/recognizer/tag-parser.ts`

- [ ] **步骤 1: 重构 TagParser 实现**

```typescript
// PSDExporterProject/src/recognizer/tag-parser.ts
import { TagParseResult } from './tag-parse-result';
import { TagConfig } from './tag-config-loader';

export class TagParser {
  private familyMaps: Map<string, Set<string>>;

  constructor(config: TagConfig) {
    // Build family lookup maps for O(1) tag classification
    this.familyMaps = new Map();
    
    this.familyMaps.set('main', new Set(config.families.main.map(t => t.id)));
    this.familyMaps.set('textBackend', new Set(config.families.textBackend.map(t => t.id)));
    this.familyMaps.set('imageType', new Set(config.families.imageType.map(t => t.id)));
    this.familyMaps.set('role', new Set(config.families.role.map(t => t.id)));
  }

  /**
   * Parse layer name into structured tag result
   * @param layerName Layer name to parse
   * @returns TagParseResult or null if no valid tags found
   */
  parse(layerName: string): TagParseResult | null {
    // Step 1: Check for ref/refp prefix
    let prefix: 'ref' | 'refp' | undefined;
    let remaining = layerName;

    if (layerName.startsWith('ref ')) {
      prefix = 'ref';
      remaining = layerName.slice(4); // Remove "ref "
    } else if (layerName.startsWith('refp ')) {
      prefix = 'refp';
      remaining = layerName.slice(5); // Remove "refp "
    }

    // Step 2: Split by dot
    const tokens = remaining.split('.');
    if (tokens.length === 0) return null;

    // Step 3: Extract baseName (first token before tags)
    const baseName = tokens[0];

    // Step 4: Parse tags from right to left
    const families: TagParseResult['families'] = {};
    
    for (let i = tokens.length - 1; i >= 1; i--) {
      const token = tokens[i];
      
      // Try to classify this token into a family
      for (const [familyName, tagSet] of this.familyMaps.entries()) {
        if (tagSet.has(token)) {
          // Only assign if this family hasn't been set yet (right-to-left priority)
          if (!families[familyName as keyof typeof families]) {
            families[familyName as keyof typeof families] = token;
          }
          break; // Found family, move to next token
        }
      }
      // If token not recognized, skip it (graceful degradation)
    }

    // Step 5: Return null if no valid families found
    if (Object.keys(families).length === 0) {
      return null;
    }

    return {
      prefix,
      baseName,
      families,
    };
  }

  /**
   * Check if layer name contains at least one valid tag
   */
  hasTag(layerName: string): boolean {
    return this.parse(layerName) !== null;
  }
}
```

- [ ] **步骤 2: 运行测试验证通过**

运行: `cd PSDExporterProject && npm test -- tag-parser.test.ts`
预期: 所有测试 PASS

- [ ] **步骤 3: 验证编译**

运行: `cd PSDExporterProject && npm run build`
预期: 编译成功

- [ ] **步骤 4: Commit**

```bash
git add PSDExporterProject/src/recognizer/tag-parser.ts
git commit -m "refactor(recognizer): implement new TagParser with dot-suffix parsing

- Implement right-to-left parsing algorithm
- Support ref/refp prefix detection
- Build family lookup maps for O(1) classification
- Skip unknown tags gracefully
- Return null when no valid tags found

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 7-9: 补充边界测试与优化（简化）

**说明:** 这部分包括补充更多边界测试、性能优化等，按 TDD 继续完善。为节省篇幅，合并为一个任务组。

---

### 任务 10: 扩展 ComponentType 和 ComponentInfo

**覆盖的上游需求:** Design 切片 C - 类型系统扩展

**依赖:** 任务 6

**文件:**
- 修改: `PSDExporterProject/src/recognizer/component-types.ts`

- [ ] **步骤 1: 扩展 ComponentType 枚举**

```typescript
// PSDExporterProject/src/recognizer/component-types.ts
export type ComponentType =
  | 'Button'
  | 'Image'
  | 'RawImage'      // 新增
  | 'Text'
  | 'ScrollView'
  | 'InputField'
  | 'Dropdown'      // 新增
  | 'Toggle'        // 新增
  | 'Slider'        // 新增
  | 'Mask'          // 新增
  | 'FillColor'     // 新增
  | 'VerticalLayoutGroup'
  | 'HorizontalLayoutGroup'
  | 'GridLayoutGroup'
  | 'Unknown';

export interface ComponentInfo {
  type: ComponentType;
  textBackend?: 'tmp' | 'ugui';      // 新增
  imageType?: 'simple' | 'sliced' | 'tiled' | 'filled';  // 新增
  role?: string;                      // 新增
  confidence: number;
  source: 'tag' | 'cv' | 'ai';
  needsReview: boolean;
}
```

- [ ] **步骤 2: 验证编译**

运行: `cd PSDExporterProject && npm run build`
预期: 编译成功

- [ ] **步骤 3: Commit**

```bash
git add PSDExporterProject/src/recognizer/component-types.ts
git commit -m "feat(recognizer): extend ComponentType and ComponentInfo

- Add 5 new ComponentType values (Dropdown, Toggle, Slider, RawImage, Mask, FillColor)
- Add textBackend optional field (tmp | ugui)
- Add imageType optional field (simple | sliced | tiled | filled)
- Add role optional field (string)

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 11-14: ComponentRecognizer 适配与 JSON Schema 扩展（简化）

**说明:** 包括适配 TagParser 新接口、实现 mainFamilyToComponentType 映射、更新 ComponentRecognizer.recognize()、扩展 json-schema.ts。按 TDD 流程实施，为节省篇幅合并描述。

**核心代码片段:**

```typescript
// component-recognizer.ts 映射表
const MAIN_FAMILY_MAP: Record<string, ComponentType> = {
  'bt': 'Button',
  'img': 'Image',
  'rimg': 'RawImage',
  'txt': 'Text',
  'ipt': 'InputField',
  'dpd': 'Dropdown',
  'tg': 'Toggle',
  'sld': 'Slider',
  'sv': 'ScrollView',
  'msk': 'Mask',
  'col': 'FillColor',
};

// recognize() 新逻辑
const tagResult = this.tagParser.parse(layer.name);
if (tagResult && tagResult.families.main) {
  const type = MAIN_FAMILY_MAP[tagResult.families.main] || 'Unknown';
  return {
    type,
    textBackend: tagResult.families.textBackend as 'tmp' | 'ugui' | undefined,
    imageType: tagResult.families.imageType as any,
    role: tagResult.families.role,
    confidence: 1.0,
    source: 'tag',
    needsReview: false,
  };
}
// Fall back to AI...
```

---

### 任务 15-17: 集成测试（简化）

**说明:** 编写端到端集成测试，覆盖 FRD 所有验收条件，运行现有测试套件并修复失败用例。

---

### 任务 18: Unity C# ComponentInfo 数据类

**覆盖的上游需求:** Design 切片 E - C# 数据类

**依赖:** 任务 14

**文件:**
- 创建: `UnityProject/Assets/Change/Editor/PSD2UI/ComponentInfo.cs`

- [ ] **步骤 1: 创建 ComponentInfo.cs**

```csharp
// UnityProject/Assets/Change/Editor/PSD2UI/ComponentInfo.cs
namespace Change.Editor.PSD2UI
{
    /// <summary>
    /// Component information from PSD exporter JSON
    /// Maps to TypeScript ComponentInfo structure
    /// </summary>
    public class ComponentInfo
    {
        public string Type { get; set; }
        public string TextBackend { get; set; }
        public string ImageType { get; set; }
        public string Role { get; set; }
        public float Confidence { get; set; }
        public string Source { get; set; }
        public bool NeedsReview { get; set; }
    }
}
```

- [ ] **步骤 2: 验证 Unity 编译**

运行: 在 Unity Editor 中打开项目，等待编译完成
预期: 无编译错误

- [ ] **步骤 3: Commit**

```bash
git add UnityProject/Assets/Change/Editor/PSD2UI/ComponentInfo.cs
git commit -m "feat(unity): add ComponentInfo C# data class

- Map to TypeScript ComponentInfo structure
- Support textBackend, imageType, role fields
- Use Newtonsoft.Json for deserialization

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 19-22: Unity ComponentFactory 扩展与测试（简化）

**说明:** 扩展 ComponentFactory 支持新组件类型（Dropdown, Toggle, Slider, RawImage, Mask），实现 ConfigureImageType() 和 CreateTextComponent()，编写单元测试验证新旧调用方式。

**核心代码片段:**

```csharp
// ComponentFactory.cs
public Component CreateComponent(string type, GameObject target, ComponentInfo info = null)
{
    switch (type)
    {
        case "Image":
            var image = target.AddComponent<Image>();
            if (info?.ImageType != null)
                ConfigureImageType(image, info.ImageType);
            return image;
            
        case "Text":
            return CreateTextComponent(info?.TextBackend ?? "tmp", target);
            
        case "Dropdown":
            return target.AddComponent<Dropdown>();
        // ... 其他新类型
    }
}
```

---

## 总结与下一步

本计划包含 **22 个任务**，覆盖 TypeScript 端（切片 A-D）和 Unity C# 端（切片 E）的完整实现。

**任务统计:**
- 切片 A (类型定义与配置): 任务 1-4
- 切片 B (核心解析引擎): 任务 5-9
- 切片 C (ComponentInfo 扩展): 任务 10-14
- 切片 D (集成测试): 任务 15-17
- 切片 E (Unity C# 端): 任务 18-22

**预计时间:** 每任务 2-5 分钟，总计约 60-110 分钟

**执行建议:** 使用 `mz-implement-subagent` 技能，每个任务调度一个子代理，实现 TDD 和两阶段审查的快速迭代。


---

## 附录：关键任务详细展开

### 任务 11 详细步骤: ComponentRecognizer 适配测试（TDD）

**文件:**
- 修改: `PSDExporterProject/src/recognizer/component-recognizer.ts`
- 测试: `PSDExporterProject/tests/recognizer/component-recognizer.test.ts`

- [ ] **步骤 1: 编写适配新 TagParser 的测试**

```typescript
// 测试文件中添加新测试用例
describe('ComponentRecognizer with new TagParser', () => {
  it('should recognize close.bt.tmp.bg as Button with textBackend and role', async () => {
    const layer = { id: '1', name: 'close.bt.tmp.bg', /* ... */ };
    const result = await recognizer.recognize(layer);
    
    expect(result.type).toBe('Button');
    expect(result.textBackend).toBe('tmp');
    expect(result.role).toBe('bg');
    expect(result.confidence).toBe(1.0);
    expect(result.source).toBe('tag');
  });
  
  it('should recognize icon.img.sliced as Image with imageType', async () => {
    const layer = { id: '2', name: 'icon.img.sliced', /* ... */ };
    const result = await recognizer.recognize(layer);
    
    expect(result.type).toBe('Image');
    expect(result.imageType).toBe('sliced');
  });
});
```

- [ ] **步骤 2: 运行测试验证失败**
- [ ] **步骤 3: Commit 测试**

### 任务 12 详细步骤: ComponentRecognizer 实现 mainFamilyToComponentType

- [ ] **步骤 1: 添加映射表**

```typescript
// component-recognizer.ts
const MAIN_FAMILY_MAP: Record<string, ComponentType> = {
  'bt': 'Button',
  'img': 'Image',
  'rimg': 'RawImage',
  'txt': 'Text',
  'ipt': 'InputField',
  'dpd': 'Dropdown',
  'tg': 'Toggle',
  'sld': 'Slider',
  'sv': 'ScrollView',
  'msk': 'Mask',
  'col': 'FillColor',
};

private mainFamilyToComponentType(mainFamily: string): ComponentType {
  return MAIN_FAMILY_MAP[mainFamily] || 'Unknown';
}
```

### 任务 13 详细步骤: 更新 recognize() 方法

- [ ] **步骤 1: 重构 recognize() 适配 TagParseResult**

```typescript
async recognize(layer: Layer, imagePath?: string): Promise<ComponentInfo> {
  // 1. Try tag recognition with new parser
  const tagResult = this.tagParser.parse(layer.name);
  
  if (tagResult && tagResult.families.main) {
    const type = this.mainFamilyToComponentType(tagResult.families.main);
    
    return {
      type,
      textBackend: tagResult.families.textBackend as 'tmp' | 'ugui' | undefined,
      imageType: tagResult.families.imageType as ComponentInfo['imageType'],
      role: tagResult.families.role,
      confidence: 1.0,
      source: 'tag',
      needsReview: false,
    };
  }

  // 2. Fall back to AI recognition (existing logic)
  if (this.options.enableAI && this.aiIdentifier && imagePath) {
    const result = await this.aiIdentifier.identify(layer, imagePath);
    return {
      type: result.type,
      confidence: result.confidence,
      source: 'ai',
      needsReview: result.confidence < this.options.cvConfidenceMin,
    };
  }

  // 3. Unknown fallback
  return {
    type: 'Unknown',
    confidence: 0,
    source: 'ai',
    needsReview: this.options.enableAI,
  };
}
```

- [ ] **步骤 2: 运行测试验证通过**
- [ ] **步骤 3: Commit**

### 任务 14 详细步骤: 扩展 JSON Schema

- [ ] **步骤 1: 修改 ComponentInfoSchema**

```typescript
// json-schema.ts
export const ComponentInfoSchema = z.object({
  type: z.enum([
    'Button', 'Image', 'RawImage', 'Text', 'ScrollView', 'InputField',
    'Dropdown', 'Toggle', 'Slider', 'Mask', 'FillColor',
    'VerticalLayoutGroup', 'HorizontalLayoutGroup', 'GridLayoutGroup', 'Unknown',
  ]),
  textBackend: z.enum(['tmp', 'ugui']).optional(),
  imageType: z.enum(['simple', 'sliced', 'tiled', 'filled']).optional(),
  role: z.string().optional(),
  confidence: z.number().min(0).max(1),
  source: z.enum(['tag', 'cv', 'ai']),
  needsReview: z.boolean(),
});
```

- [ ] **步骤 2: 验证 JSON 导出**
- [ ] **步骤 3: Commit**

