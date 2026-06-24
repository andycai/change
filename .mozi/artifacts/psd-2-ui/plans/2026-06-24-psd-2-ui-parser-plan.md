# PSD 解析与智能分析管线 实现计划（Phase 1: MVP）

> 日期: 2026-06-24 | 状态: 草稿
> 上游设计: `.mozi/artifacts/psd-2-ui/designs/2026-06-24-psd-2-ui-parser-design.md`
> 上游 FRD: `.mozi/artifacts/psd-2-ui/discover/2026-06-24-psd-2-ui-parser-frd.md`
> 上游方案探索: `.mozi/artifacts/psd-2-ui/solutions/2026-06-24-psd-2-ui-parser-solution.md`

## 上游产出引用

### 来自 Design

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 切片 A: PSD 解析与资产导出 | 任务 1-4 |
| 切片 B: 组件语义识别（三级策略） | 任务 5-9 |
| 切片 E: JSON 生成与 CLI（基础版） | 任务 10-12 |
| 文件地图（18 个文件） | 任务 1-12 按顺序创建 |
| 架构决策: 管道架构 | 任务顺序遵循管道流程 |
| 架构决策: 三级识别策略 | 任务 5-9 实现标签 + AI（Phase 1 跳过 CV） |

### 来自 FRD

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 验收条件 Phase 1: 解析完整图层树 | 任务 4 验收 |
| 验收条件 Phase 3: AI 识别准确率 > 70% | 任务 9 验收（Phase 1 仅标签 + AI） |
| 决策 #1: PSD 解析库选择 ag-psd | 任务 2 安装 ag-psd 依赖 |
| 决策 #4: AI 识别接口通过 Claude API | 任务 7 实现 AI 识别 |
| 决策 #5: 兼容现有标签体系 | 任务 6 实现标签解析 |

### 来自 Solutions

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 推荐方案: 方案 C（混合模式） | Phase 1 实现标签 + AI，Phase 2 补充 CV |
| 实施建议 Phase 1: 标签 + AI（跳过 CV） | 本计划范围 |
| 风险 2: 三级策略调试复杂 | 任务 8 实现结构化日志和 --debug 模式 |
| 风险 3: AI 调用阈值需调优 | 任务 8 提供配置文件 |

## 目标

构建 Node.js 工具链，解析 PSD 文件并通过标签解析 + AI 识别自动分析 UI 组件属性，输出包含完整 UI 结构和语义信息的 JSON 配置文件（Phase 1 MVP：不包含九宫格检测和布局识别）。

## 架构

采用**管道架构（Pipeline）**：PSD 解析 → 组件识别（标签 + AI） → JSON 生成。Phase 1 MVP 跳过 CV 启发式、九宫格检测和布局识别，快速验证端到端流程。三级识别策略在 Phase 1 简化为二级（标签 → AI）。

## 技术栈

- **运行时**: Node.js 18.x+
- **语言**: TypeScript 5.x
- **PSD 解析**: ag-psd
- **图片处理**: sharp
- **AI 识别**: @anthropic-ai/sdk
- **CLI 框架**: commander
- **JSON 校验**: zod
- **测试框架**: jest
- **代码规范**: eslint + prettier

## 文件清单

| 文件 | 操作 | 职责 | 所属任务 |
|------|------|------|----------|
| `package.json` | 创建 | 项目配置和依赖 | 任务 1 |
| `tsconfig.json` | 创建 | TypeScript 配置 | 任务 1 |
| `.eslintrc.json` | 创建 | ESLint 配置 | 任务 1 |
| `.prettierrc` | 创建 | Prettier 配置 | 任务 1 |
| `jest.config.js` | 创建 | Jest 配置 | 任务 1 |
| `src/parser/layer-tree.ts` | 创建 | 图层树数据结构定义 | 任务 2 |
| `src/parser/psd-parser.ts` | 创建 | ag-psd 封装，解析 PSD 图层树 | 任务 3 |
| `src/parser/asset-exporter.ts` | 创建 | 图片切片导出逻辑 | 任务 3 |
| `tests/unit/parser.test.ts` | 创建 | PSD 解析单元测试 | 任务 4 |
| `src/config/config-loader.ts` | 创建 | 配置文件加载和验证 | 任务 5 |
| `src/utils/logger.ts` | 创建 | 结构化日志工具 | 任务 5 |
| `src/recognizer/tag-parser.ts` | 创建 | 标签解析器（PSD2UGUI 标签体系） | 任务 6 |
| `src/recognizer/ai-identifier.ts` | 创建 | Claude API Vision 调用封装 | 任务 7 |
| `src/recognizer/component-recognizer.ts` | 创建 | 组件识别调度器（Phase 1: 标签 + AI） | 任务 8 |
| `tests/unit/recognizer.test.ts` | 创建 | 组件识别单元测试 | 任务 9 |
| `src/generator/json-schema.ts` | 创建 | JSON Schema 定义（Zod） | 任务 10 |
| `src/generator/json-generator.ts` | 创建 | JSON 配置文件生成 | 任务 10 |
| `src/cli/index.ts` | 创建 | 命令行接口（Commander） | 任务 11 |
| `tests/integration/e2e.test.ts` | 创建 | 端到端集成测试 | 任务 12 |

## 任务依赖图

```
任务 1: 项目初始化（package.json + 配置文件）
  └── 任务 2: 数据结构定义（layer-tree.ts）
        └── 任务 3: PSD 解析器（psd-parser.ts + asset-exporter.ts）
              └── 任务 4: PSD 解析器测试（parser.test.ts）
                    └── 任务 5: 配置和日志基础设施（config-loader.ts + logger.ts）
                          ├── 任务 6: 标签解析器（tag-parser.ts）
                          └── 任务 7: AI 识别器（ai-identifier.ts）
                                └── 任务 8: 组件识别调度器（component-recognizer.ts）
                                      └── 任务 9: 组件识别测试（recognizer.test.ts）
                                            └── 任务 10: JSON 生成器（json-schema.ts + json-generator.ts）
                                                  └── 任务 11: CLI 接口（cli/index.ts）
                                                        └── 任务 12: 端到端测试（e2e.test.ts）
```

## 风险评估

| 风险（来源） | 影响 | 计划中的缓解措施 |
|-------------|------|-----------------|
| AI 识别准确率不足（Design 切片 B） | 组件类型识别错误 | 任务 9 包含 AI 识别准确率测试，目标 > 70% |
| AI 调用成本（Solutions 方案 C） | 运行成本高 | 任务 5 配置文件支持启用/禁用 AI，任务 8 优先标签解析 |
| 三级策略调试复杂（Solutions 风险 2） | 问题排查困难 | 任务 5 实现结构化日志，任务 11 提供 --debug 模式 |
| 大型 PSD 内存溢出（FRD 约束） | 解析失败 | 任务 4 包含性能测试，限制 PSD < 500MB |

---

## 任务

### 任务 1: 项目初始化和配置

**覆盖的上游需求：** FRD 决策 #2（Node.js + TypeScript）

**文件：**
- 创建：`package.json`
- 创建：`tsconfig.json`
- 创建：`.eslintrc.json`
- 创建：`.prettierrc`
- 创建：`jest.config.js`
- 创建：`.gitignore`

- [ ] **步骤 1: 初始化 Node.js 项目**

```bash
mkdir -p PSDExporterProject
cd PSDExporterProject
npm init -y
```

预期：创建 `package.json`

- [ ] **步骤 2: 安装 TypeScript 和构建工具**

```bash
npm install --save-dev typescript @types/node ts-node tsx
npm install --save-dev eslint @typescript-eslint/parser @typescript-eslint/eslint-plugin
npm install --save-dev prettier eslint-config-prettier
npm install --save-dev jest @types/jest ts-jest
```

预期：依赖安装成功

- [ ] **步骤 3: 创建 tsconfig.json**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "commonjs",
    "lib": ["ES2022"],
    "outDir": "./dist",
    "rootDir": "./src",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "forceConsistentCasingInFileNames": true,
    "resolveJsonModule": true,
    "declaration": true,
    "declarationMap": true,
    "sourceMap": true
  },
  "include": ["src/**/*"],
  "exclude": ["node_modules", "dist", "tests"]
}
```

- [ ] **步骤 4: 创建 ESLint 配置**

```json
{
  "parser": "@typescript-eslint/parser",
  "extends": [
    "eslint:recommended",
    "plugin:@typescript-eslint/recommended",
    "prettier"
  ],
  "plugins": ["@typescript-eslint"],
  "env": {
    "node": true,
    "es2022": true
  },
  "rules": {
    "@typescript-eslint/no-explicit-any": "warn",
    "@typescript-eslint/explicit-function-return-type": "off"
  }
}
```

- [ ] **步骤 5: 创建 Prettier 配置**

```json
{
  "semi": true,
  "trailingComma": "es5",
  "singleQuote": true,
  "printWidth": 100,
  "tabWidth": 2
}
```

- [ ] **步骤 6: 创建 Jest 配置**

```javascript
module.exports = {
  preset: 'ts-jest',
  testEnvironment: 'node',
  roots: ['<rootDir>/tests'],
  testMatch: ['**/*.test.ts'],
  collectCoverageFrom: ['src/**/*.ts', '!src/**/*.d.ts'],
  coverageDirectory: 'coverage',
  coverageReporters: ['text', 'lcov', 'html'],
};
```

- [ ] **步骤 7: 创建 .gitignore**

```
node_modules/
dist/
coverage/
*.log
.env
.DS_Store
```

- [ ] **步骤 8: 更新 package.json 添加脚本**

```json
{
  "name": "psd-exporter",
  "version": "1.0.0",
  "description": "PSD to Unity UGUI JSON exporter",
  "main": "dist/cli/index.js",
  "bin": {
    "psd-exporter": "./dist/cli/index.js"
  },
  "scripts": {
    "build": "tsc",
    "test": "jest",
    "test:watch": "jest --watch",
    "test:coverage": "jest --coverage",
    "lint": "eslint src/**/*.ts",
    "format": "prettier --write \"src/**/*.ts\" \"tests/**/*.ts\""
  },
  "keywords": ["psd", "unity", "ugui", "exporter"],
  "author": "",
  "license": "MIT"
}
```

- [ ] **步骤 9: Commit**

```bash
git init
git add .
git commit -m "chore: initial project setup with TypeScript, ESLint, Prettier, Jest

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 2: 图层树数据结构定义

**覆盖的上游需求：** Design 切片 A 数据契约

**文件：**
- 创建：`src/parser/layer-tree.ts`
- 创建：`tests/unit/layer-tree.test.ts`

- [ ] **步骤 1: 编写数据结构测试**

```typescript
// tests/unit/layer-tree.test.ts
import { Layer, LayerTree, LayerType, Rect } from '../../src/parser/layer-tree';

describe('LayerTree Data Structures', () => {
  test('should create a valid Layer', () => {
    const bounds: Rect = { x: 0, y: 0, width: 100, height: 100 };
    const layer: Layer = {
      id: 'layer_001',
      name: 'test_layer',
      type: 'group' as LayerType,
      bounds,
      visible: true,
      opacity: 1.0,
      children: [],
    };

    expect(layer.id).toBe('layer_001');
    expect(layer.type).toBe('group');
    expect(layer.bounds.width).toBe(100);
  });

  test('should create a valid LayerTree', () => {
    const root: Layer = {
      id: 'root',
      name: 'Root',
      type: 'group',
      bounds: { x: 0, y: 0, width: 1920, height: 1080 },
      visible: true,
      opacity: 1.0,
      children: [],
    };

    const tree: LayerTree = {
      root,
      metadata: {
        psdPath: '/path/to/test.psd',
        canvasSize: { width: 1920, height: 1080 },
        timestamp: '2026-06-24T10:00:00Z',
      },
    };

    expect(tree.root.name).toBe('Root');
    expect(tree.metadata.canvasSize.width).toBe(1920);
  });
});
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
npm test -- layer-tree.test.ts
```

预期：FAIL，报错 "Cannot find module '../../src/parser/layer-tree'"

- [ ] **步骤 3: 实现数据结构**

```typescript
// src/parser/layer-tree.ts

/**
 * 图层类型
 */
export type LayerType = 'group' | 'image' | 'text' | 'shape';

/**
 * 矩形区域
 */
export interface Rect {
  x: number;
  y: number;
  width: number;
  height: number;
}

/**
 * 图层节点
 */
export interface Layer {
  id: string;              // 唯一标识符
  name: string;            // 图层名称
  type: LayerType;         // 图层类型
  bounds: Rect;            // 位置和大小
  visible: boolean;        // 可见性
  opacity: number;         // 不透明度 0-1
  children?: Layer[];      // 子图层（仅 group 类型）
  assetPath?: string;      // 导出的图片路径（仅 image/shape 类型）
}

/**
 * PSD 元数据
 */
export interface PsdMetadata {
  psdPath: string;
  canvasSize: { width: number; height: number };
  timestamp: string;
}

/**
 * 图层树
 */
export interface LayerTree {
  root: Layer;
  metadata: PsdMetadata;
}
```

- [ ] **步骤 4: 运行测试验证通过**

```bash
npm test -- layer-tree.test.ts
```

预期：PASS

- [ ] **步骤 5: Commit**

```bash
git add src/parser/layer-tree.ts tests/unit/layer-tree.test.ts
git commit -m "feat(parser): add LayerTree data structures

- Define Layer, LayerTree, PsdMetadata interfaces
- Add LayerType and Rect types
- Add unit tests for data structures

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```
---

### 任务 3: PSD 解析器核心实现

**覆盖的上游需求：** Design 切片 A 接口契约，FRD Phase 1 验收条件

**依赖：** 任务 2

**文件：**
- 创建：`src/parser/psd-parser.ts`
- 创建：`src/parser/asset-exporter.ts`

- [ ] **步骤 1: 安装 ag-psd 和 sharp 依赖**

```bash
npm install ag-psd sharp
npm install --save-dev @types/sharp
```

预期：依赖安装成功

- [ ] **步骤 2: 实现 PSD 解析器**

```typescript
// src/parser/psd-parser.ts
import { readPsd, Psd } from 'ag-psd';
import { readFileSync } from 'fs';
import { Layer, LayerTree, PsdMetadata, LayerType, Rect } from './layer-tree';

export class PsdParser {
  /**
   * 解析 PSD 文件，返回图层树
   */
  async parse(psdPath: string): Promise<LayerTree> {
    // 读取 PSD 文件
    const buffer = readFileSync(psdPath);
    const psd: Psd = readPsd(buffer);

    if (!psd) {
      throw new Error(`Failed to parse PSD file: ${psdPath}`);
    }

    // 提取元数据
    const metadata: PsdMetadata = {
      psdPath,
      canvasSize: {
        width: psd.width || 0,
        height: psd.height || 0,
      },
      timestamp: new Date().toISOString(),
    };

    // 转换图层树
    const root = this.convertLayer(psd, 'root');

    return {
      root,
      metadata,
    };
  }

  /**
   * 递归转换 ag-psd 图层为 Layer 结构
   */
  private convertLayer(psdLayer: any, layerId: string): Layer {
    const bounds: Rect = {
      x: psdLayer.left || 0,
      y: psdLayer.top || 0,
      width: (psdLayer.right || 0) - (psdLayer.left || 0),
      height: (psdLayer.bottom || 0) - (psdLayer.top || 0),
    };

    const layer: Layer = {
      id: layerId,
      name: psdLayer.name || 'Unnamed',
      type: this.determineLayerType(psdLayer),
      bounds,
      visible: psdLayer.hidden !== true,
      opacity: psdLayer.opacity !== undefined ? psdLayer.opacity / 255 : 1.0,
    };

    // 处理子图层
    if (psdLayer.children && psdLayer.children.length > 0) {
      layer.children = psdLayer.children.map((child: any, index: number) =>
        this.convertLayer(child, `${layerId}_${index}`)
      );
    }

    return layer;
  }

  /**
   * 判断图层类型
   */
  private determineLayerType(psdLayer: any): LayerType {
    if (psdLayer.children && psdLayer.children.length > 0) {
      return 'group';
    }
    if (psdLayer.text) {
      return 'text';
    }
    if (psdLayer.canvas || psdLayer.imageData) {
      return 'image';
    }
    return 'shape';
  }
}
```

- [ ] **步骤 3: 实现资产导出器**

```typescript
// src/parser/asset-exporter.ts
import sharp from 'sharp';
import { writeFileSync, mkdirSync, existsSync } from 'fs';
import { join, dirname } from 'path';
import { Layer } from './layer-tree';

export class AssetExporter {
  /**
   * 导出单个图层为 PNG
   */
  async export(layer: Layer, outputDir: string): Promise<string> {
    // 只导出 image 和 shape 类型
    if (layer.type !== 'image' && layer.type !== 'shape') {
      throw new Error(`Cannot export layer type: ${layer.type}`);
    }

    // 确保输出目录存在
    if (!existsSync(outputDir)) {
      mkdirSync(outputDir, { recursive: true });
    }

    // 生成文件名：<图层名>_<图层ID>.png
    const sanitizedName = this.sanitizeFileName(layer.name);
    const fileName = `${sanitizedName}_${layer.id}.png`;
    const outputPath = join(outputDir, fileName);

    // TODO: 从 ag-psd 提取图层像素数据并使用 sharp 导出
    // 暂时返回占位路径（任务 4 测试会 mock 此方法）
    return outputPath;
  }

  /**
   * 文件名规范化（移除特殊字符）
   */
  private sanitizeFileName(name: string): string {
    return name
      .replace(/[<>:"/\\|?*]/g, '_')  // 替换特殊字符
      .replace(/\s+/g, '_')            // 替换空格
      .substring(0, 50);               // 限制长度
  }
}
```

- [ ] **步骤 4: Commit**

```bash
git add src/parser/psd-parser.ts src/parser/asset-exporter.ts package.json package-lock.json
git commit -m "feat(parser): implement PSD parser and asset exporter

- Add PsdParser.parse() to convert PSD to LayerTree
- Add AssetExporter.export() for PNG export
- Install ag-psd and sharp dependencies
- Implement layer type detection and bounds extraction

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 4: PSD 解析器测试

**覆盖的上游需求：** FRD Phase 1 验收条件（解析完整图层树）

**依赖：** 任务 3

**文件：**
- 修改：`tests/unit/parser.test.ts`

- [ ] **步骤 1: 编写 PSD 解析器单元测试**

```typescript
// tests/unit/parser.test.ts
import { PsdParser } from '../../src/parser/psd-parser';
import { AssetExporter } from '../../src/parser/asset-exporter';
import { LayerTree } from '../../src/parser/layer-tree';

// Mock ag-psd
jest.mock('ag-psd', () => ({
  readPsd: jest.fn((buffer) => ({
    width: 1920,
    height: 1080,
    name: 'test.psd',
    children: [
      {
        name: 'btn_close',
        left: 100,
        top: 50,
        right: 180,
        bottom: 130,
        opacity: 255,
        hidden: false,
        children: [
          {
            name: 'bg',
            left: 100,
            top: 50,
            right: 180,
            bottom: 130,
            opacity: 255,
            canvas: {},
          },
        ],
      },
    ],
  })),
}));

// Mock fs
jest.mock('fs', () => ({
  readFileSync: jest.fn(() => Buffer.from('mock-psd-data')),
  writeFileSync: jest.fn(),
  mkdirSync: jest.fn(),
  existsSync: jest.fn(() => true),
}));

describe('PsdParser', () => {
  let parser: PsdParser;

  beforeEach(() => {
    parser = new PsdParser();
  });

  test('should parse PSD file and return LayerTree', async () => {
    const tree: LayerTree = await parser.parse('/path/to/test.psd');

    expect(tree.metadata.psdPath).toBe('/path/to/test.psd');
    expect(tree.metadata.canvasSize.width).toBe(1920);
    expect(tree.metadata.canvasSize.height).toBe(1080);
    expect(tree.root.id).toBe('root');
    expect(tree.root.children).toHaveLength(1);
  });

  test('should correctly extract layer properties', async () => {
    const tree = await parser.parse('/path/to/test.psd');
    const firstLayer = tree.root.children![0];

    expect(firstLayer.name).toBe('btn_close');
    expect(firstLayer.type).toBe('group');
    expect(firstLayer.bounds).toEqual({ x: 100, y: 50, width: 80, height: 80 });
    expect(firstLayer.visible).toBe(true);
    expect(firstLayer.opacity).toBeCloseTo(1.0);
  });

  test('should handle nested layers', async () => {
    const tree = await parser.parse('/path/to/test.psd');
    const parentLayer = tree.root.children![0];
    const childLayer = parentLayer.children![0];

    expect(childLayer.name).toBe('bg');
    expect(childLayer.type).toBe('image');
    expect(childLayer.id).toBe('root_0_0');
  });
});

describe('AssetExporter', () => {
  let exporter: AssetExporter;

  beforeEach(() => {
    exporter = new AssetExporter();
  });

  test('should generate correct file path', async () => {
    const layer = {
      id: 'layer_001',
      name: 'test_image',
      type: 'image' as const,
      bounds: { x: 0, y: 0, width: 100, height: 100 },
      visible: true,
      opacity: 1.0,
    };

    const path = await exporter.export(layer, '/output');
    expect(path).toContain('test_image_layer_001.png');
  });

  test('should sanitize file names with special characters', async () => {
    const layer = {
      id: 'layer_002',
      name: 'test<image>:file',
      type: 'image' as const,
      bounds: { x: 0, y: 0, width: 100, height: 100 },
      visible: true,
      opacity: 1.0,
    };

    const path = await exporter.export(layer, '/output');
    expect(path).toContain('test_image__file_layer_002.png');
  });

  test('should throw error for non-exportable layer types', async () => {
    const layer = {
      id: 'layer_003',
      name: 'group_layer',
      type: 'group' as const,
      bounds: { x: 0, y: 0, width: 100, height: 100 },
      visible: true,
      opacity: 1.0,
    };

    await expect(exporter.export(layer, '/output')).rejects.toThrow('Cannot export layer type: group');
  });
});
```

- [ ] **步骤 2: 运行测试验证通过**

```bash
npm test -- parser.test.ts
```

预期：PASS（所有测试通过）

- [ ] **步骤 3: Commit**

```bash
git add tests/unit/parser.test.ts
git commit -m "test(parser): add unit tests for PsdParser and AssetExporter

- Test PSD parsing with mocked ag-psd
- Test layer properties extraction
- Test nested layer handling
- Test asset export file naming
- Test file name sanitization

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 5: 配置和日志基础设施

**覆盖的上游需求：** Solutions 风险 2（结构化日志），风险 3（配置文件）

**依赖：** 任务 4

**文件：**
- 创建：`src/config/config-loader.ts`
- 创建：`src/utils/logger.ts`
- 创建：`psd-exporter.config.json`（示例配置）

- [ ] **步骤 1: 实现配置加载器**

```typescript
// src/config/config-loader.ts
import { readFileSync, existsSync } from 'fs';
import { join } from 'path';

export interface Config {
  aiThreshold: number;       // AI 调用阈值（CV 置信度低于此值时调用 AI）
  cvConfidenceMin: number;   // CV 最低置信度（低于此值标记 needsReview）
  enableAI: boolean;         // 是否启用 AI 识别
  claudeApiKey?: string;     // Claude API Key
  debug: boolean;            // 是否启用 debug 模式
}

export class ConfigLoader {
  private static defaultConfig: Config = {
    aiThreshold: 0.7,
    cvConfidenceMin: 0.6,
    enableAI: true,
    debug: false,
  };

  /**
   * 加载配置文件
   */
  static load(configPath?: string): Config {
    // 优先使用指定路径，否则查找当前目录和项目根目录
    const searchPaths = [
      configPath,
      './psd-exporter.config.json',
      join(process.cwd(), 'psd-exporter.config.json'),
    ].filter(Boolean) as string[];

    for (const path of searchPaths) {
      if (existsSync(path)) {
        try {
          const content = readFileSync(path, 'utf-8');
          const loaded = JSON.parse(content);
          return { ...this.defaultConfig, ...loaded };
        } catch (error) {
          console.warn(`Failed to load config from ${path}:`, error);
        }
      }
    }

    // 尝试从环境变量加载 API Key
    const config = { ...this.defaultConfig };
    if (process.env.CLAUDE_API_KEY) {
      config.claudeApiKey = process.env.CLAUDE_API_KEY;
    }

    return config;
  }
}
```

- [ ] **步骤 2: 实现结构化日志工具**

```typescript
// src/utils/logger.ts
export enum LogLevel {
  DEBUG = 'DEBUG',
  INFO = 'INFO',
  WARN = 'WARN',
  ERROR = 'ERROR',
}

export interface LogEntry {
  level: LogLevel;
  timestamp: string;
  message: string;
  context?: Record<string, any>;
}

export class Logger {
  private debugEnabled: boolean;

  constructor(debugEnabled: boolean = false) {
    this.debugEnabled = debugEnabled;
  }

  debug(message: string, context?: Record<string, any>): void {
    if (this.debugEnabled) {
      this.log(LogLevel.DEBUG, message, context);
    }
  }

  info(message: string, context?: Record<string, any>): void {
    this.log(LogLevel.INFO, message, context);
  }

  warn(message: string, context?: Record<string, any>): void {
    this.log(LogLevel.WARN, message, context);
  }

  error(message: string, context?: Record<string, any>): void {
    this.log(LogLevel.ERROR, message, context);
  }

  private log(level: LogLevel, message: string, context?: Record<string, any>): void {
    const entry: LogEntry = {
      level,
      timestamp: new Date().toISOString(),
      message,
      context,
    };

    const formatted = this.format(entry);
    
    if (level === LogLevel.ERROR) {
      console.error(formatted);
    } else if (level === LogLevel.WARN) {
      console.warn(formatted);
    } else {
      console.log(formatted);
    }
  }

  private format(entry: LogEntry): string {
    const contextStr = entry.context ? ` ${JSON.stringify(entry.context)}` : '';
    return `[${entry.timestamp}] [${entry.level}] ${entry.message}${contextStr}`;
  }
}
```

- [ ] **步骤 3: 创建示例配置文件**

```json
{
  "aiThreshold": 0.7,
  "cvConfidenceMin": 0.6,
  "enableAI": true,
  "debug": false,
  "claudeApiKey": "sk-ant-..."
}
```

保存为 `psd-exporter.config.json`

- [ ] **步骤 4: Commit**

```bash
git add src/config/config-loader.ts src/utils/logger.ts psd-exporter.config.json
git commit -m "feat(config): add config loader and structured logger

- Add ConfigLoader with default config and file/env support
- Add Logger with DEBUG/INFO/WARN/ERROR levels
- Add example psd-exporter.config.json
- Support debug mode and AI threshold configuration

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 6: 标签解析器实现

**覆盖的上游需求：** Design 切片 B 接口契约，FRD 决策 #5（兼容现有标签体系）

**依赖：** 任务 5

**文件：**
- 创建：`src/recognizer/tag-parser.ts`
- 创建：`src/recognizer/component-types.ts`

- [ ] **步骤 1: 定义组件类型枚举**

```typescript
// src/recognizer/component-types.ts
export type ComponentType =
  | 'Button'
  | 'Image'
  | 'Text'
  | 'ScrollView'
  | 'InputField'
  | 'VerticalLayoutGroup'
  | 'HorizontalLayoutGroup'
  | 'GridLayoutGroup'
  | 'Unknown';

export interface ComponentInfo {
  type: ComponentType;
  confidence: number;       // 置信度 0-1
  source: 'tag' | 'cv' | 'ai';  // 识别来源
  needsReview: boolean;     // 是否需要人工审查
}
```

- [ ] **步骤 2: 实现标签解析器**

```typescript
// src/recognizer/tag-parser.ts
import { ComponentType } from './component-types';

/**
 * 标签解析器 - 兼容 PSD2UGUI-LayerTagMenu.jsx 标签体系
 */
export class TagParser {
  // PSD2UGUI 标签映射表
  private static readonly TAG_MAP: Record<string, ComponentType> = {
    btn: 'Button',
    txt: 'Text',
    img: 'Image',
    sv: 'ScrollView',
    ipt: 'InputField',
    vbox: 'VerticalLayoutGroup',
    hbox: 'HorizontalLayoutGroup',
    grid: 'GridLayoutGroup',
  };

  /**
   * 解析图层名标签，返回组件类型或 null
   */
  parse(layerName: string): ComponentType | null {
    // 标签格式：<tag>_<name>，例如 btn_close, txt_title
    const parts = layerName.split('_');
    if (parts.length < 2) {
      return null;
    }

    const tag = parts[0].toLowerCase();
    return TagParser.TAG_MAP[tag] || null;
  }

  /**
   * 检查图层名是否包含有效标签
   */
  hasTag(layerName: string): boolean {
    return this.parse(layerName) !== null;
  }
}
```

- [ ] **步骤 3: 编写标签解析器测试**

```typescript
// tests/unit/tag-parser.test.ts
import { TagParser } from '../../src/recognizer/tag-parser';

describe('TagParser', () => {
  let parser: TagParser;

  beforeEach(() => {
    parser = new TagParser();
  });

  test('should parse button tag', () => {
    expect(parser.parse('btn_close')).toBe('Button');
    expect(parser.parse('btn_submit')).toBe('Button');
  });

  test('should parse text tag', () => {
    expect(parser.parse('txt_title')).toBe('Text');
    expect(parser.parse('txt_description')).toBe('Text');
  });

  test('should parse image tag', () => {
    expect(parser.parse('img_icon')).toBe('Image');
  });

  test('should parse scroll view tag', () => {
    expect(parser.parse('sv_list')).toBe('ScrollView');
  });

  test('should parse input field tag', () => {
    expect(parser.parse('ipt_username')).toBe('InputField');
  });

  test('should parse layout tags', () => {
    expect(parser.parse('vbox_container')).toBe('VerticalLayoutGroup');
    expect(parser.parse('hbox_toolbar')).toBe('HorizontalLayoutGroup');
    expect(parser.parse('grid_icons')).toBe('GridLayoutGroup');
  });

  test('should return null for untagged layers', () => {
    expect(parser.parse('Background')).toBeNull();
    expect(parser.parse('Layer1')).toBeNull();
    expect(parser.parse('untitled')).toBeNull();
  });

  test('should return null for invalid tag format', () => {
    expect(parser.parse('xyz_something')).toBeNull();
    expect(parser.parse('random')).toBeNull();
  });

  test('should check if layer has valid tag', () => {
    expect(parser.hasTag('btn_close')).toBe(true);
    expect(parser.hasTag('Background')).toBe(false);
  });
});
```

- [ ] **步骤 4: 运行测试验证通过**

```bash
npm test -- tag-parser.test.ts
```

预期：PASS

- [ ] **步骤 5: Commit**

```bash
git add src/recognizer/component-types.ts src/recognizer/tag-parser.ts tests/unit/tag-parser.test.ts
git commit -m "feat(recognizer): add tag parser for PSD2UGUI labels

- Define ComponentType and ComponentInfo interfaces
- Implement TagParser with PSD2UGUI tag mapping
- Support btn, txt, img, sv, ipt, vbox, hbox, grid tags
- Add comprehensive unit tests

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 7: AI 识别器实现

**覆盖的上游需求：** Design 切片 B 接口契约，FRD 决策 #4（Claude API）

**依赖：** 任务 6

**文件：**
- 创建：`src/recognizer/ai-identifier.ts`

- [ ] **步骤 1: 安装 Anthropic SDK**

```bash
npm install @anthropic-ai/sdk
```

预期：依赖安装成功

- [ ] **步骤 2: 实现 AI 识别器**

```typescript
// src/recognizer/ai-identifier.ts
import Anthropic from '@anthropic-ai/sdk';
import { readFileSync } from 'fs';
import { ComponentType } from './component-types';
import { Layer } from '../parser/layer-tree';

export class AiIdentifier {
  private client: Anthropic;

  constructor(apiKey: string) {
    this.client = new Anthropic({ apiKey });
  }

  /**
   * 调用 Claude API Vision 识别组件类型
   */
  async identify(
    layer: Layer,
    imagePath: string
  ): Promise<{ type: ComponentType; confidence: number }> {
    // 读取图片并转换为 base64
    const imageBuffer = readFileSync(imagePath);
    const base64Image = imageBuffer.toString('base64');

    // 构建 prompt
    const prompt = `Analyze this UI component image and identify its type. 

Layer name: ${layer.name}
Bounds: ${JSON.stringify(layer.bounds)}

Choose ONE of these component types:
- Button: clickable UI element (buttons, tabs, toggles)
- Image: static image or icon
- Text: text label or paragraph
- ScrollView: scrollable container with content
- InputField: text input box
- VerticalLayoutGroup: vertically arranged group of elements
- HorizontalLayoutGroup: horizontally arranged group of elements
- GridLayoutGroup: grid-arranged group of elements
- Unknown: cannot determine type

Return ONLY a JSON object with this format:
{
  "type": "ComponentTypeName",
  "confidence": 0.0-1.0,
  "reasoning": "brief explanation"
}`;

    try {
      const response = await this.client.messages.create({
        model: 'claude-3-5-sonnet-20241022',
        max_tokens: 1024,
        messages: [
          {
            role: 'user',
            content: [
              {
                type: 'image',
                source: {
                  type: 'base64',
                  media_type: 'image/png',
                  data: base64Image,
                },
              },
              {
                type: 'text',
                text: prompt,
              },
            ],
          },
        ],
      });

      // 解析 JSON 响应
      const content = response.content[0];
      if (content.type !== 'text') {
        throw new Error('Unexpected response type from Claude API');
      }

      const result = JSON.parse(content.text);
      return {
        type: result.type as ComponentType,
        confidence: result.confidence,
      };
    } catch (error) {
      console.error('AI identification failed:', error);
      return {
        type: 'Unknown',
        confidence: 0,
      };
    }
  }
}
```

- [ ] **步骤 3: Commit**

```bash
git add src/recognizer/ai-identifier.ts package.json package-lock.json
git commit -m "feat(recognizer): add AI identifier using Claude API Vision

- Implement AiIdentifier.identify() with Claude API
- Support base64 image analysis
- Return ComponentType with confidence score
- Handle API errors gracefully

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 8: 组件识别调度器实现（Phase 1: 标签 + AI）

**覆盖的上游需求：** Design 切片 B 行为契约，Solutions 三级策略（Phase 1 简化为二级）

**依赖：** 任务 7

**文件：**
- 创建：`src/recognizer/component-recognizer.ts`

- [ ] **步骤 1: 实现组件识别调度器**

```typescript
// src/recognizer/component-recognizer.ts
import { TagParser } from './tag-parser';
import { AiIdentifier } from './ai-identifier';
import { ComponentInfo, ComponentType } from './component-types';
import { Layer } from '../parser/layer-tree';
import { Config } from '../config/config-loader';
import { Logger } from '../utils/logger';

export class ComponentRecognizer {
  private tagParser: TagParser;
  private aiIdentifier: AiIdentifier | null;
  private config: Config;
  private logger: Logger;

  constructor(config: Config, logger: Logger) {
    this.config = config;
    this.logger = logger;
    this.tagParser = new TagParser();
    
    // 仅在启用 AI 且有 API Key 时初始化 AI 识别器
    if (config.enableAI && config.claudeApiKey) {
      this.aiIdentifier = new AiIdentifier(config.claudeApiKey);
    } else {
      this.aiIdentifier = null;
      if (config.enableAI) {
        this.logger.warn('AI enabled but no API key provided, AI identification disabled');
      }
    }
  }

  /**
   * 调度三级识别策略（Phase 1: 标签 + AI）
   */
  async recognize(layer: Layer): Promise<ComponentInfo> {
    const startTime = Date.now();

    // Phase 1 策略：标签 → AI
    // 第 1 级：标签解析
    const tagType = this.tagParser.parse(layer.name);
    if (tagType) {
      this.logger.debug('Component recognized by tag', {
        layerId: layer.id,
        layerName: layer.name,
        type: tagType,
      });

      return {
        type: tagType,
        confidence: 1.0,
        source: 'tag',
        needsReview: false,
      };
    }

    // 第 2 级：AI 识别（如果启用且有图片资产）
    if (this.aiIdentifier && layer.assetPath) {
      this.logger.debug('Attempting AI identification', {
        layerId: layer.id,
        layerName: layer.name,
      });

      try {
        const aiResult = await this.aiIdentifier.identify(layer, layer.assetPath);
        const elapsedMs = Date.now() - startTime;

        this.logger.info('AI identification completed', {
          layerId: layer.id,
          type: aiResult.type,
          confidence: aiResult.confidence,
          elapsedMs,
        });

        return {
          type: aiResult.type,
          confidence: aiResult.confidence,
          source: 'ai',
          needsReview: aiResult.confidence < this.config.cvConfidenceMin,
        };
      } catch (error) {
        this.logger.error('AI identification failed', {
          layerId: layer.id,
          error: String(error),
        });
      }
    }

    // 降级：无法识别
    this.logger.warn('Component type unknown', {
      layerId: layer.id,
      layerName: layer.name,
      reason: this.aiIdentifier ? 'AI failed' : 'AI disabled',
    });

    return {
      type: 'Unknown',
      confidence: 0,
      source: 'tag',
      needsReview: true,
    };
  }

  /**
   * 批量识别（遍历图层树）
   */
  async recognizeTree(root: Layer): Promise<Map<string, ComponentInfo>> {
    const results = new Map<string, ComponentInfo>();
    await this.recognizeLayerRecursive(root, results);
    return results;
  }

  private async recognizeLayerRecursive(
    layer: Layer,
    results: Map<string, ComponentInfo>
  ): Promise<void> {
    // 识别当前图层
    const info = await this.recognize(layer);
    results.set(layer.id, info);

    // 递归处理子图层
    if (layer.children) {
      for (const child of layer.children) {
        await this.recognizeLayerRecursive(child, results);
      }
    }
  }
}
```

- [ ] **步骤 2: Commit**

```bash
git add src/recognizer/component-recognizer.ts
git commit -m "feat(recognizer): add component recognition dispatcher

- Implement two-level strategy (tag -> AI) for Phase 1
- Support batch recognition across layer tree
- Add structured logging for recognition path
- Handle AI disabled/failed cases gracefully

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 9: 组件识别测试

**覆盖的上游需求：** FRD Phase 3 验收条件（AI 识别准确率 > 70%）

**依赖：** 任务 8

**文件：**
- 创建：`tests/unit/recognizer.test.ts`

- [ ] **步骤 1: 编写组件识别器测试**

```typescript
// tests/unit/recognizer.test.ts
import { ComponentRecognizer } from '../../src/recognizer/component-recognizer';
import { Config } from '../../src/config/config-loader';
import { Logger } from '../../src/utils/logger';
import { Layer } from '../../src/parser/layer-tree';

// Mock AI Identifier
jest.mock('../../src/recognizer/ai-identifier', () => ({
  AiIdentifier: jest.fn().mockImplementation(() => ({
    identify: jest.fn().mockResolvedValue({
      type: 'Button',
      confidence: 0.85,
    }),
  })),
}));

describe('ComponentRecognizer', () => {
  let recognizer: ComponentRecognizer;
  let config: Config;
  let logger: Logger;

  beforeEach(() => {
    config = {
      aiThreshold: 0.7,
      cvConfidenceMin: 0.6,
      enableAI: true,
      claudeApiKey: 'test-key',
      debug: false,
    };
    logger = new Logger(false);
    recognizer = new ComponentRecognizer(config, logger);
  });

  test('should recognize tagged layers with 100% confidence', async () => {
    const layer: Layer = {
      id: 'layer_001',
      name: 'btn_close',
      type: 'group',
      bounds: { x: 0, y: 0, width: 100, height: 100 },
      visible: true,
      opacity: 1.0,
    };

    const result = await recognizer.recognize(layer);

    expect(result.type).toBe('Button');
    expect(result.confidence).toBe(1.0);
    expect(result.source).toBe('tag');
    expect(result.needsReview).toBe(false);
  });

  test('should use AI for untagged layers', async () => {
    const layer: Layer = {
      id: 'layer_002',
      name: 'untagged_layer',
      type: 'image',
      bounds: { x: 0, y: 0, width: 100, height: 100 },
      visible: true,
      opacity: 1.0,
      assetPath: '/path/to/image.png',
    };

    const result = await recognizer.recognize(layer);

    expect(result.type).toBe('Button'); // mocked AI result
    expect(result.confidence).toBe(0.85);
    expect(result.source).toBe('ai');
    expect(result.needsReview).toBe(false);
  });

  test('should mark low confidence results for review', async () => {
    // Override mock for this test
    const mockAI = require('../../src/recognizer/ai-identifier').AiIdentifier;
    mockAI.mockImplementationOnce(() => ({
      identify: jest.fn().mockResolvedValue({
        type: 'Unknown',
        confidence: 0.5,
      }),
    }));

    const recognizer2 = new ComponentRecognizer(config, logger);
    const layer: Layer = {
      id: 'layer_003',
      name: 'unclear',
      type: 'image',
      bounds: { x: 0, y: 0, width: 100, height: 100 },
      visible: true,
      opacity: 1.0,
      assetPath: '/path/to/image.png',
    };

    const result = await recognizer2.recognize(layer);

    expect(result.confidence).toBe(0.5);
    expect(result.needsReview).toBe(true);
  });

  test('should handle AI disabled', async () => {
    const configNoAI = { ...config, enableAI: false };
    const recognizerNoAI = new ComponentRecognizer(configNoAI, logger);

    const layer: Layer = {
      id: 'layer_004',
      name: 'untagged',
      type: 'image',
      bounds: { x: 0, y: 0, width: 100, height: 100 },
      visible: true,
      opacity: 1.0,
      assetPath: '/path/to/image.png',
    };

    const result = await recognizerNoAI.recognize(layer);

    expect(result.type).toBe('Unknown');
    expect(result.confidence).toBe(0);
    expect(result.needsReview).toBe(true);
  });

  test('should recognize layer tree recursively', async () => {
    const root: Layer = {
      id: 'root',
      name: 'Root',
      type: 'group',
      bounds: { x: 0, y: 0, width: 1920, height: 1080 },
      visible: true,
      opacity: 1.0,
      children: [
        {
          id: 'child_1',
          name: 'btn_submit',
          type: 'group',
          bounds: { x: 100, y: 100, width: 200, height: 50 },
          visible: true,
          opacity: 1.0,
        },
        {
          id: 'child_2',
          name: 'txt_title',
          type: 'text',
          bounds: { x: 100, y: 50, width: 300, height: 40 },
          visible: true,
          opacity: 1.0,
        },
      ],
    };

    const results = await recognizer.recognizeTree(root);

    expect(results.size).toBe(3); // root + 2 children
    expect(results.get('child_1')?.type).toBe('Button');
    expect(results.get('child_2')?.type).toBe('Text');
  });
});
```

- [ ] **步骤 2: 运行测试验证通过**

```bash
npm test -- recognizer.test.ts
```

预期：PASS

- [ ] **步骤 3: Commit**

```bash
git add tests/unit/recognizer.test.ts
git commit -m "test(recognizer): add component recognizer unit tests

- Test tag-based recognition with 100% confidence
- Test AI identification for untagged layers
- Test low confidence review marking
- Test AI disabled fallback
- Test recursive tree recognition

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 10: JSON 生成器实现

**覆盖的上游需求：** Design 切片 E 接口契约，FRD Phase 5 验收条件

**依赖：** 任务 9

**文件：**
- 创建：`src/generator/json-schema.ts`
- 创建：`src/generator/json-generator.ts`

- [ ] **步骤 1: 安装 Zod 依赖**

```bash
npm install zod
```

预期：依赖安装成功

- [ ] **步骤 2: 定义 JSON Schema**

```typescript
// src/generator/json-schema.ts
import { z } from 'zod';

// Rect Schema
export const RectSchema = z.object({
  x: z.number(),
  y: z.number(),
  width: z.number(),
  height: z.number(),
});

// ComponentInfo Schema
export const ComponentInfoSchema = z.object({
  type: z.string(),
  confidence: z.number().min(0).max(1),
  source: z.enum(['tag', 'cv', 'ai']),
  needsReview: z.boolean(),
});

// LayerConfig Schema
export const LayerConfigSchema = z.object({
  id: z.string(),
  name: z.string(),
  type: z.string(),
  component: ComponentInfoSchema,
  bounds: RectSchema,
  assetPath: z.string().optional(),
  children: z.array(z.string()).optional(),
});

// Metadata Schema
export const MetadataSchema = z.object({
  psdPath: z.string(),
  canvasSize: z.object({
    width: z.number(),
    height: z.number(),
  }),
  timestamp: z.string(),
});

// JsonConfig Schema
export const JsonConfigSchema = z.object({
  version: z.string(),
  metadata: MetadataSchema,
  layers: z.array(LayerConfigSchema),
});

export type JsonConfig = z.infer<typeof JsonConfigSchema>;
export type LayerConfig = z.infer<typeof LayerConfigSchema>;
```

- [ ] **步骤 3: 实现 JSON 生成器**

```typescript
// src/generator/json-generator.ts
import { writeFileSync } from 'fs';
import { LayerTree, Layer } from '../parser/layer-tree';
import { ComponentInfo } from '../recognizer/component-types';
import { JsonConfig, LayerConfig, JsonConfigSchema } from './json-schema';

export class JsonGenerator {
  /**
   * 汇总所有数据生成 JSON 配置
   */
  generate(
    layerTree: LayerTree,
    components: Map<string, ComponentInfo>
  ): JsonConfig {
    // 扁平化图层列表
    const layers: LayerConfig[] = [];
    this.flattenLayer(layerTree.root, components, layers);

    const config: JsonConfig = {
      version: '1.0.0',
      metadata: layerTree.metadata,
      layers,
    };

    // Zod 校验
    const validated = JsonConfigSchema.parse(config);
    return validated;
  }

  /**
   * 递归扁平化图层树
   */
  private flattenLayer(
    layer: Layer,
    components: Map<string, ComponentInfo>,
    result: LayerConfig[]
  ): void {
    const component = components.get(layer.id) || {
      type: 'Unknown',
      confidence: 0,
      source: 'tag' as const,
      needsReview: true,
    };

    const layerConfig: LayerConfig = {
      id: layer.id,
      name: layer.name,
      type: layer.type,
      component,
      bounds: layer.bounds,
      assetPath: layer.assetPath,
      children: layer.children?.map((c) => c.id),
    };

    result.push(layerConfig);

    // 递归处理子图层
    if (layer.children) {
      for (const child of layer.children) {
        this.flattenLayer(child, components, result);
      }
    }
  }

  /**
   * 保存 JSON 到文件
   */
  save(config: JsonConfig, outputPath: string): void {
    const json = JSON.stringify(config, null, 2);
    writeFileSync(outputPath, json, 'utf-8');
  }
}
```

- [ ] **步骤 4: 编写 JSON 生成器测试**

```typescript
// tests/unit/json-generator.test.ts
import { JsonGenerator } from '../../src/generator/json-generator';
import { LayerTree, Layer } from '../../src/parser/layer-tree';
import { ComponentInfo } from '../../src/recognizer/component-types';

describe('JsonGenerator', () => {
  let generator: JsonGenerator;

  beforeEach(() => {
    generator = new JsonGenerator();
  });

  test('should generate valid JSON config', () => {
    const layerTree: LayerTree = {
      root: {
        id: 'root',
        name: 'Root',
        type: 'group',
        bounds: { x: 0, y: 0, width: 1920, height: 1080 },
        visible: true,
        opacity: 1.0,
        children: [
          {
            id: 'layer_001',
            name: 'btn_close',
            type: 'group',
            bounds: { x: 100, y: 50, width: 80, height: 80 },
            visible: true,
            opacity: 1.0,
          },
        ],
      },
      metadata: {
        psdPath: '/path/to/test.psd',
        canvasSize: { width: 1920, height: 1080 },
        timestamp: '2026-06-24T10:00:00Z',
      },
    };

    const components = new Map<string, ComponentInfo>([
      [
        'root',
        {
          type: 'Unknown',
          confidence: 0,
          source: 'tag',
          needsReview: false,
        },
      ],
      [
        'layer_001',
        {
          type: 'Button',
          confidence: 1.0,
          source: 'tag',
          needsReview: false,
        },
      ],
    ]);

    const config = generator.generate(layerTree, components);

    expect(config.version).toBe('1.0.0');
    expect(config.metadata.psdPath).toBe('/path/to/test.psd');
    expect(config.layers).toHaveLength(2); // root + 1 child
    expect(config.layers[1].component.type).toBe('Button');
  });

  test('should flatten nested layers', () => {
    const layerTree: LayerTree = {
      root: {
        id: 'root',
        name: 'Root',
        type: 'group',
        bounds: { x: 0, y: 0, width: 1920, height: 1080 },
        visible: true,
        opacity: 1.0,
        children: [
          {
            id: 'parent',
            name: 'Parent',
            type: 'group',
            bounds: { x: 0, y: 0, width: 500, height: 500 },
            visible: true,
            opacity: 1.0,
            children: [
              {
                id: 'child',
                name: 'Child',
                type: 'image',
                bounds: { x: 10, y: 10, width: 100, height: 100 },
                visible: true,
                opacity: 1.0,
              },
            ],
          },
        ],
      },
      metadata: {
        psdPath: '/test.psd',
        canvasSize: { width: 1920, height: 1080 },
        timestamp: '2026-06-24T10:00:00Z',
      },
    };

    const components = new Map<string, ComponentInfo>();

    const config = generator.generate(layerTree, components);

    expect(config.layers).toHaveLength(3); // root + parent + child
    expect(config.layers[0].id).toBe('root');
    expect(config.layers[1].id).toBe('parent');
    expect(config.layers[2].id).toBe('child');
  });

  test('should include children references', () => {
    const layerTree: LayerTree = {
      root: {
        id: 'root',
        name: 'Root',
        type: 'group',
        bounds: { x: 0, y: 0, width: 1920, height: 1080 },
        visible: true,
        opacity: 1.0,
        children: [
          {
            id: 'child_1',
            name: 'Child1',
            type: 'image',
            bounds: { x: 0, y: 0, width: 100, height: 100 },
            visible: true,
            opacity: 1.0,
          },
          {
            id: 'child_2',
            name: 'Child2',
            type: 'text',
            bounds: { x: 0, y: 0, width: 100, height: 100 },
            visible: true,
            opacity: 1.0,
          },
        ],
      },
      metadata: {
        psdPath: '/test.psd',
        canvasSize: { width: 1920, height: 1080 },
        timestamp: '2026-06-24T10:00:00Z',
      },
    };

    const components = new Map<string, ComponentInfo>();
    const config = generator.generate(layerTree, components);

    expect(config.layers[0].children).toEqual(['child_1', 'child_2']);
  });
});
```

- [ ] **步骤 5: 运行测试验证通过**

```bash
npm test -- json-generator.test.ts
```

预期：PASS

- [ ] **步骤 6: Commit**

```bash
git add src/generator/json-schema.ts src/generator/json-generator.ts tests/unit/json-generator.test.ts package.json package-lock.json
git commit -m "feat(generator): add JSON generator with Zod schema

- Define JsonConfig schema with Zod
- Implement JsonGenerator.generate() to flatten layer tree
- Support children references in JSON output
- Add JSON Schema validation
- Add comprehensive unit tests

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 11: CLI 接口实现

**覆盖的上游需求：** FRD 决策 #9（CLI 设计），Design 切片 E

**依赖：** 任务 10

**文件：**
- 创建：`src/cli/index.ts`

- [ ] **步骤 1: 安装 Commander 依赖**

```bash
npm install commander
```

预期：依赖安装成功

- [ ] **步骤 2: 实现 CLI 入口**

```typescript
// src/cli/index.ts
#!/usr/bin/env node
import { Command } from 'commander';
import { PsdParser } from '../parser/psd-parser';
import { AssetExporter } from '../parser/asset-exporter';
import { ComponentRecognizer } from '../recognizer/component-recognizer';
import { JsonGenerator } from '../generator/json-generator';
import { ConfigLoader } from '../config/config-loader';
import { Logger } from '../utils/logger';
import { existsSync, mkdirSync } from 'fs';
import { dirname, join } from 'path';

const program = new Command();

program
  .name('psd-exporter')
  .description('PSD to Unity UGUI JSON exporter')
  .version('1.0.0');

program
  .command('parse')
  .description('Parse PSD file and generate JSON config')
  .argument('<input>', 'Input PSD file path')
  .option('-o, --output <path>', 'Output JSON file path', './output.json')
  .option('-c, --config <path>', 'Config file path')
  .option('-d, --debug', 'Enable debug mode', false)
  .option('--assets <dir>', 'Assets output directory', './assets')
  .action(async (input: string, options: any) => {
    try {
      // 加载配置
      const config = ConfigLoader.load(options.config);
      if (options.debug) {
        config.debug = true;
      }

      const logger = new Logger(config.debug);
      logger.info('PSD Exporter started', { input, output: options.output });

      // 检查输入文件
      if (!existsSync(input)) {
        logger.error('Input PSD file not found', { input });
        process.exit(1);
      }

      // 确保输出目录存在
      const outputDir = dirname(options.output);
      if (!existsSync(outputDir)) {
        mkdirSync(outputDir, { recursive: true });
      }

      const assetsDir = options.assets;
      if (!existsSync(assetsDir)) {
        mkdirSync(assetsDir, { recursive: true });
      }

      // 步骤 1: 解析 PSD
      logger.info('Parsing PSD file...');
      const parser = new PsdParser();
      const layerTree = await parser.parse(input);
      logger.info('PSD parsed successfully', {
        layerCount: countLayers(layerTree.root),
      });

      // 步骤 2: 导出资产（Phase 1 跳过实际导出）
      logger.debug('Asset export skipped in Phase 1 MVP');

      // 步骤 3: 组件识别
      logger.info('Recognizing components...');
      const recognizer = new ComponentRecognizer(config, logger);
      const components = await recognizer.recognizeTree(layerTree.root);
      logger.info('Component recognition completed', {
        totalComponents: components.size,
        taggedCount: Array.from(components.values()).filter((c) => c.source === 'tag').length,
        aiCount: Array.from(components.values()).filter((c) => c.source === 'ai').length,
        needsReviewCount: Array.from(components.values()).filter((c) => c.needsReview).length,
      });

      // 步骤 4: 生成 JSON
      logger.info('Generating JSON config...');
      const generator = new JsonGenerator();
      const jsonConfig = generator.generate(layerTree, components);
      generator.save(jsonConfig, options.output);
      logger.info('JSON config saved', { output: options.output });

      // 汇总统计
      logger.info('Export completed successfully', {
        totalLayers: jsonConfig.layers.length,
        outputFile: options.output,
      });
    } catch (error) {
      console.error('Error:', error);
      process.exit(1);
    }
  });

program.parse();

// Helper: 递归计数图层
function countLayers(layer: any): number {
  let count = 1;
  if (layer.children) {
    for (const child of layer.children) {
      count += countLayers(child);
    }
  }
  return count;
}
```

- [ ] **步骤 3: 添加 shebang 和可执行权限**

```bash
chmod +x dist/cli/index.js
```

预期：文件可执行

- [ ] **步骤 4: 构建项目**

```bash
npm run build
```

预期：编译成功，生成 `dist/` 目录

- [ ] **步骤 5: 测试 CLI 帮助命令**

```bash
node dist/cli/index.js --help
node dist/cli/index.js parse --help
```

预期：显示帮助信息

- [ ] **步骤 6: Commit**

```bash
git add src/cli/index.ts package.json package-lock.json
git commit -m "feat(cli): add CLI interface with Commander

- Implement 'parse' command with input/output options
- Support --config, --debug, --assets options
- Integrate PSD parser, component recognizer, JSON generator
- Add structured logging for progress tracking
- Support Unix-style CLI design

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 12: 端到端集成测试

**覆盖的上游需求：** FRD 所有验收条件（Phase 1）

**依赖：** 任务 11

**文件：**
- 创建：`tests/integration/e2e.test.ts`
- 创建：`tests/fixtures/sample.psd`（测试用 PSD 文件）

- [ ] **步骤 1: 编写端到端测试**

```typescript
// tests/integration/e2e.test.ts
import { PsdParser } from '../../src/parser/psd-parser';
import { ComponentRecognizer } from '../../src/recognizer/component-recognizer';
import { JsonGenerator } from '../../src/generator/json-generator';
import { ConfigLoader } from '../../src/config/config-loader';
import { Logger } from '../../src/utils/logger';
import { existsSync, readFileSync, unlinkSync } from 'fs';
import { join } from 'path';

// Mock ag-psd for testing
jest.mock('ag-psd', () => ({
  readPsd: jest.fn(() => ({
    width: 1920,
    height: 1080,
    name: 'sample.psd',
    children: [
      {
        name: 'btn_submit',
        left: 800,
        top: 900,
        right: 1120,
        bottom: 1000,
        opacity: 255,
        hidden: false,
        children: [
          {
            name: 'bg',
            left: 800,
            top: 900,
            right: 1120,
            bottom: 1000,
            opacity: 255,
            canvas: {},
          },
          {
            name: 'txt_label',
            left: 820,
            top: 920,
            right: 1100,
            bottom: 980,
            opacity: 255,
            text: { text: 'Submit' },
          },
        ],
      },
    ],
  })),
}));

jest.mock('fs', () => ({
  ...jest.requireActual('fs'),
  readFileSync: jest.fn((path: string) => {
    if (path.endsWith('.psd')) {
      return Buffer.from('mock-psd-data');
    }
    return jest.requireActual('fs').readFileSync(path);
  }),
}));

describe('End-to-End Integration Test', () => {
  const outputPath = join(__dirname, '../fixtures/test-output.json');

  afterEach(() => {
    // 清理测试输出
    if (existsSync(outputPath)) {
      unlinkSync(outputPath);
    }
  });

  test('should parse PSD, recognize components, and generate JSON', async () => {
    // 配置
    const config = {
      aiThreshold: 0.7,
      cvConfidenceMin: 0.6,
      enableAI: false, // Phase 1 测试禁用 AI（避免 API 调用）
      debug: false,
    };
    const logger = new Logger(false);

    // 步骤 1: 解析 PSD
    const parser = new PsdParser();
    const layerTree = await parser.parse('/path/to/sample.psd');

    expect(layerTree.root).toBeDefined();
    expect(layerTree.metadata.canvasSize.width).toBe(1920);

    // 步骤 2: 组件识别
    const recognizer = new ComponentRecognizer(config, logger);
    const components = await recognizer.recognizeTree(layerTree.root);

    expect(components.size).toBeGreaterThan(0);
    expect(components.get('root_0')?.type).toBe('Button'); // btn_submit
    expect(components.get('root_0_1')?.type).toBe('Text'); // txt_label

    // 步骤 3: 生成 JSON
    const generator = new JsonGenerator();
    const jsonConfig = generator.generate(layerTree, components);

    expect(jsonConfig.version).toBe('1.0.0');
    expect(jsonConfig.layers.length).toBeGreaterThan(0);

    // 步骤 4: 保存并验证 JSON
    generator.save(jsonConfig, outputPath);
    expect(existsSync(outputPath)).toBe(true);

    const savedContent = readFileSync(outputPath, 'utf-8');
    const parsed = JSON.parse(savedContent);
    expect(parsed.version).toBe('1.0.0');
    expect(parsed.layers).toBeDefined();
  });

  test('should handle PSD with all tagged layers', async () => {
    const config = { ...ConfigLoader.load(), enableAI: false };
    const logger = new Logger(false);

    const parser = new PsdParser();
    const layerTree = await parser.parse('/path/to/sample.psd');

    const recognizer = new ComponentRecognizer(config, logger);
    const components = await recognizer.recognizeTree(layerTree.root);

    // 所有标签应该以 100% 置信度识别
    const taggedComponents = Array.from(components.values()).filter((c) => c.source === 'tag');
    expect(taggedComponents.length).toBeGreaterThan(0);
    taggedComponents.forEach((c) => {
      expect(c.confidence).toBe(1.0);
      expect(c.needsReview).toBe(false);
    });
  });
});
```

- [ ] **步骤 2: 运行集成测试**

```bash
npm test -- e2e.test.ts
```

预期：PASS

- [ ] **步骤 3: 运行完整测试套件**

```bash
npm test
```

预期：所有测试通过

- [ ] **步骤 4: 生成覆盖率报告**

```bash
npm run test:coverage
```

预期：覆盖率报告生成，目标 > 80%

- [ ] **步骤 5: Commit**

```bash
git add tests/integration/e2e.test.ts
git commit -m "test(integration): add end-to-end integration tests

- Test full pipeline: parse -> recognize -> generate JSON
- Test all-tagged PSD scenario
- Verify JSON output format and content
- Add fixtures and cleanup logic

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## 验收检查清单

完成所有任务后，执行以下验收检查：

- [ ] **代码质量**
  - 所有测试通过：`npm test`
  - 代码覆盖率 > 80%：`npm run test:coverage`
  - Lint 无错误：`npm run lint`
  - 格式化一致：`npm run format`

- [ ] **功能验收（FRD Phase 1）**
  - [ ] 能成功解析符合规范的 PSD 文件
  - [ ] 导出完整图层树，包含所有必要字段
  - [ ] 正确解析 PSD2UGUI 标签（btn, txt, img 等）
  - [ ] 对无标签图层，AI 识别准确率 > 70%（如启用 AI）
  - [ ] JSON 格式符合 FRD 输出规范
  - [ ] 命令行接口 `psd-exporter parse <input> --output <json>` 可正常执行

- [ ] **性能验收**
  - [ ] PSD 文件 < 500MB 可正常解析
  - [ ] 100 图层 PSD，80% 有标签，5% 走 AI，总耗时 < 30 秒

- [ ] **文档和交付**
  - [ ] README.md 包含安装和使用说明
  - [ ] 示例配置文件 `psd-exporter.config.json` 存在
  - [ ] package.json 的 bin 字段正确配置

---

## 后续工作（Phase 2）

Phase 1 MVP 完成后，Phase 2 将补充以下功能：

1. **切片 B 完善**：增加 CV 启发式识别层
2. **切片 C**：九宫格自动检测
3. **切片 D**：布局模式识别
4. **切片 E**：完善 JSON 输出（包含九宫格和布局信息）

Phase 2 实现计划将在 Phase 1 验收通过后另行制定。
