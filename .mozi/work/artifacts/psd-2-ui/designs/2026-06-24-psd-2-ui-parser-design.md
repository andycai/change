# PSD 解析与智能分析管线 架构设计

> 日期: 2026-06-24 | 状态: 草稿
> FRD: `.mozi/artifacts/psd-2-ui/discover/2026-06-24-psd-2-ui-parser-frd.md`
> 上游: `.mozi/artifacts/psd-2-ui/solutions/2026-06-24-psd-2-ui-parser-solution.md`

## 上游产出引用

### 来自 FRD

| 引用内容 | 如何使用 |
|---------|----------|
| 决策 #1: PSD 解析库选择 ag-psd | 切片 A 使用 ag-psd 实现 PSD 解析核心 |
| 决策 #2: 工具链语言 Node.js + TypeScript | 整体技术栈基础 |
| 决策 #3: 九宫格检测用 OpenCV 像素相似度 | 切片 C 实现九宫格检测算法 |
| 决策 #4: AI 识别接口通过 Claude API | 切片 B 实现 AI 识别调用 Claude API Vision |
| 决策 #5: 兼容现有标签体系 | 切片 B 优先解析 PSD2UGUI-LayerTagMenu.jsx 标签 |
| 决策 #7: JSON 配置结构为扁平化组件列表 + 树形引用 | 切片 E 输出 JSON 结构设计 |
| 验收条件 Phase 1: 解析完整图层树 | 切片 A 验收标准 |
| 验收条件 Phase 2: 九宫格检测准确率 > 80% | 切片 C 验收标准 |
| 验收条件 Phase 3: AI 识别准确率 > 70% | 切片 B 验收标准 |
| 验收条件 Phase 4: 布局识别（垂直/水平/网格） | 切片 D 验收标准 |

### 来自 Solutions

| 引用内容 | 如何使用 |
|---------|----------|
| 推荐方案: 方案 C（混合模式） | 整体架构采用三级识别策略（标签 → CV → AI） |
| 三级识别策略: 标签优先 + 选择性 AI + CV | 切片 B 实现组件识别调度器 |
| 运行成本: ~$0.05/PSD（80% 标签 + 15% CV + 5% AI） | 切片 B 实现 AI 调用阈值配置 |
| 风险 1: CV 规则初期准确率不足 | 切片 C 第一版仅覆盖简单组件（矩形、单图片） |
| 风险 2: 三级策略调试复杂 | 切片 B 实现结构化日志和 --debug 模式 |
| 风险 3: AI 调用阈值需调优 | 切片 B 提供配置文件 psd-exporter.config.json |
| 实施建议 Phase 1: 标签 + AI（跳过 CV） | 初期实现策略，快速验证端到端 |
| 实施建议 Phase 2: 增加 CV 层 | 降低 AI 调用频率，优化成本 |

## 架构决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 模块划分策略 | 管道架构（Pipeline）：PSD 解析 → 组件识别 → 九宫格检测 → 布局识别 → JSON 生成 | 职责清晰，每个阶段可独立测试和替换，符合单一职责原则 |
| 组件识别调度 | 三级策略（标签解析器 → CV 启发式 → AI 识别）+ 策略模式 | 灵活可配置，支持按项目预算调整 AI 调用阈值 |
| 错误处理 | 渐进式降级 + 结构化日志 | 部分失败不影响整体，程序员可选择性修复，符合 FRD 决策 #8 |
| 配置管理 | 外部配置文件 psd-exporter.config.json | 支持 AI 阈值、CV 置信度等参数动态调整 |
| 测试策略 | 单元测试（各模块） + 集成测试（端到端） + 性能测试（九宫格/AI） | 确保各切片可独立验证，符合验收条件 |

## 文件地图

| 文件 | 所属切片 | 职责 |
|------|----------|------|
| `src/parser/psd-parser.ts` | 切片 A | ag-psd 封装，解析 PSD 图层树和资产 |
| `src/parser/layer-tree.ts` | 切片 A | 图层树数据结构定义 |
| `src/parser/asset-exporter.ts` | 切片 A | 图片切片导出逻辑 |
| `src/recognizer/component-recognizer.ts` | 切片 B | 组件识别调度器（三级策略） |
| `src/recognizer/tag-parser.ts` | 切片 B | 标签解析器（PSD2UGUI 标签体系） |
| `src/recognizer/ai-identifier.ts` | 切片 B | Claude API Vision 调用封装 |
| `src/recognizer/cv-heuristic.ts` | 切片 B | CV 启发式识别（简单组件） |
| `src/detector/slice9-detector.ts` | 切片 C | 九宫格检测（OpenCV 像素相似度） |
| `src/detector/layout-detector.ts` | 切片 D | 布局识别（垂直/水平/网格） |
| `src/generator/json-generator.ts` | 切片 E | JSON 配置文件生成 |
| `src/generator/json-schema.ts` | 切片 E | JSON Schema 定义（Zod） |
| `src/cli/index.ts` | 切片 E | 命令行接口（Commander） |
| `src/config/config-loader.ts` | 切片 B | 配置文件加载和验证 |
| `src/utils/logger.ts` | 切片 B | 结构化日志工具 |
| `tests/unit/parser.test.ts` | 切片 A | PSD 解析单元测试 |
| `tests/unit/recognizer.test.ts` | 切片 B | 组件识别单元测试 |
| `tests/unit/detector.test.ts` | 切片 C/D | 九宫格/布局检测单元测试 |
| `tests/integration/e2e.test.ts` | 切片 E | 端到端集成测试 |

## 切片分解

### 切片 A: PSD 解析与资产导出

**依赖：** 无
**风险等级：** 低
**涉及文件：** `src/parser/psd-parser.ts`, `src/parser/layer-tree.ts`, `src/parser/asset-exporter.ts`, `tests/unit/parser.test.ts`

**内容：** 使用 ag-psd 解析 PSD 文件，提取完整图层树（图层名称、类型、位置、大小、可见性、嵌套关系），导出图片资产为 PNG 格式（命名规则：`<图层名>_<图层ID>.png`）。这是整个管线的数据源头，为后续所有切片提供输入。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `PsdParser.parse()` | `async parse(psdPath: string): Promise<LayerTree>` | 解析 PSD 文件，返回图层树数据结构 |
| `AssetExporter.export()` | `async export(layer: Layer, outputDir: string): Promise<string>` | 导出单个图层为 PNG，返回文件路径 |
| `LayerTree` | `interface LayerTree { root: Layer; metadata: PsdMetadata }` | 图层树根节点和元数据容器 |
| `Layer` | `interface Layer { id: string; name: string; type: LayerType; bounds: Rect; children?: Layer[] }` | 单个图层节点 |

**数据契约：**

```typescript
// 切片 A 暴露给切片 B/C/D/E 的核心数据结构
interface LayerTree {
  root: Layer;
  metadata: PsdMetadata;
}

interface Layer {
  id: string;              // 唯一标识符
  name: string;            // 图层名称
  type: LayerType;         // 'group' | 'image' | 'text' | 'shape'
  bounds: Rect;            // 位置和大小 { x, y, width, height }
  visible: boolean;        // 可见性
  opacity: number;         // 不透明度 0-1
  children?: Layer[];      // 子图层（仅 group 类型）
  assetPath?: string;      // 导出的图片路径（仅 image/shape 类型）
}

interface PsdMetadata {
  psdPath: string;
  canvasSize: { width: number; height: number };
  timestamp: string;
}
```

**验收标准：**
- [ ] 能成功解析符合规范的 PSD 文件（图层嵌套 ≤10 层，无 3D/视频图层）
- [ ] 导出完整图层树，包含所有必要字段（名称、类型、位置、大小、可见性、嵌套关系）
- [ ] 正确切片导出所有图片资产（Image 图层、Shape 图层栅格化），格式为 PNG（32 位 RGBA）
- [ ] 图片命名规则：`<图层名>_<图层ID>.png`，避免重名冲突
- [ ] 单元测试覆盖：正常 PSD、空 PSD、深层嵌套、特殊字符图层名

**回归风险评估：**
- 影响范围：无（新功能，无现有依赖）
- 缓解措施：N/A

---

### 切片 B: 组件语义识别（三级策略）

**依赖：** 切片 A（需要 LayerTree 作为输入）
**风险等级：** 高（涉及 AI 调用、成本控制、准确率）
**涉及文件：** `src/recognizer/component-recognizer.ts`, `src/recognizer/tag-parser.ts`, `src/recognizer/ai-identifier.ts`, `src/recognizer/cv-heuristic.ts`, `src/config/config-loader.ts`, `src/utils/logger.ts`, `tests/unit/recognizer.test.ts`

**内容：** 实现三级识别策略：(1) 优先解析标签（PSD2UGUI-LayerTagMenu.jsx 标签体系）；(2) 标签缺失时，对简单组件用 CV 启发式识别；(3) 复杂组件调用 Claude API Vision。提供配置文件支持 AI 调用阈值调整，结构化日志记录每个组件的识别路径。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `ComponentRecognizer.recognize()` | `async recognize(layer: Layer, config: Config): Promise<ComponentInfo>` | 调度三级识别策略，返回组件类型和置信度 |
| `TagParser.parse()` | `parse(layerName: string): ComponentType \| null` | 解析图层名标签，返回组件类型或 null |
| `CvHeuristic.identify()` | `identify(layer: Layer): { type: ComponentType; confidence: number } \| null` | CV 启发式识别简单组件 |
| `AiIdentifier.identify()` | `async identify(layer: Layer, imagePath: string): Promise<{ type: ComponentType; confidence: number }>` | 调用 Claude API Vision 识别组件类型 |

**数据契约：**

```typescript
// 切片 B 暴露给切片 E 的识别结果
interface ComponentInfo {
  type: ComponentType;      // 'Button' | 'ScrollView' | 'InputField' | 'Image' | 'Text' | ...
  confidence: number;       // 置信度 0-1
  source: 'tag' | 'cv' | 'ai';  // 识别来源
  needsReview: boolean;     // 是否需要人工审查（置信度 < 0.6）
}

type ComponentType = 
  | 'Button' | 'Image' | 'Text' | 'ScrollView' | 'InputField' 
  | 'VerticalLayoutGroup' | 'HorizontalLayoutGroup' | 'GridLayoutGroup'
  | 'Unknown';

interface Config {
  aiThreshold: number;       // AI 调用阈值（CV 置信度低于此值时调用 AI）
  cvConfidenceMin: number;   // CV 最低置信度（低于此值标记 needsReview）
  enableAI: boolean;         // 是否启用 AI 识别
  claudeApiKey?: string;     // Claude API Key
}
```

**行为契约：**
- 输入：LayerTree（来自切片 A）
- 输出：每个 Layer 附加 ComponentInfo
- 识别策略：
  1. 标签解析：匹配 btn_*, txt_*, img_*, sv_*, ipt_* 等标签 → 置信度 1.0，source='tag'
  2. CV 启发式：边界清晰、结构简单的组件 → 置信度 0.7-0.9，source='cv'
  3. AI 识别：复杂组件或 CV 置信度 < aiThreshold → 置信度 0.6-1.0，source='ai'
- 性能要求：100 图层 PSD，80% 有标签，15% 走 CV，5% 走 AI，总耗时 < 30 秒

**验收标准：**
- [ ] 正确解析 PSD2UGUI-LayerTagMenu.jsx 标签（btn, txt, img, sv, ipt 等），匹配率 100%
- [ ] 对无标签图层，CV 启发式识别简单组件（矩形、单图片），准确率 > 60%
- [ ] AI 识别准确率 > 70%（常见组件如按钮、列表、输入框）
- [ ] 低置信度（< 0.6）标记 needsReview = true
- [ ] 配置文件 psd-exporter.config.json 可动态调整 aiThreshold、cvConfidenceMin
- [ ] 结构化日志记录每个组件的识别路径（tag/cv/ai）和置信度
- [ ] --debug 模式输出详细决策树
- [ ] 单元测试覆盖：标签解析、CV 识别、AI 调用（mock）、配置加载

**回归风险评估：**
- 影响范围：无（新功能，无现有依赖）
- 缓解措施：N/A

---

### 切片 C: 九宫格自动检测

**依赖：** 切片 A（需要导出的图片资产）
**风险等级：** 中（算法准确率需验证）
**涉及文件：** `src/detector/slice9-detector.ts`, `tests/unit/detector.test.ts`

**内容：** 使用 OpenCV.js 像素相似度分析检测九宫格边界，分级策略：简单纹理（相似度 > 90%）自动检测，中等复杂（70-90%）标记"需审查"，高复杂（< 70%）标记"手动处理"。第一版仅覆盖简单组件（矩形、单图片），复杂渐变在后续迭代优化。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `Slice9Detector.detect()` | `async detect(imagePath: string): Promise<Slice9Info>` | 检测九宫格边界，返回 left/right/top/bottom 像素值和置信度 |

**数据契约：**

```typescript
// 切片 C 暴露给切片 E 的九宫格数据
interface Slice9Info {
  left: number;           // 左边界像素值
  right: number;          // 右边界像素值
  top: number;            // 上边界像素值
  bottom: number;         // 下边界像素值
  confidence: number;     // 置信度 0-1
  needsReview: boolean;   // 是否需要人工审查
}
```

**行为契约：**
- 输入：图片路径（来自切片 A 的 assetPath）
- 输出：Slice9Info（九宫格边界 + 置信度）
- 检测策略：
  1. 计算图片四边像素相似度（OpenCV 像素对比）
  2. 相似度 > 90% → 自动检测边界，confidence = 0.9-1.0，needsReview = false
  3. 相似度 70-90% → 检测边界但标记审查，confidence = 0.7-0.9，needsReview = true
  4. 相似度 < 70% → 跳过检测，confidence = 0，needsReview = true
- 性能要求：单张图片检测 < 500ms

**验收标准：**
- [ ] 对简单纹理（像素相似度 > 90%）自动检测九宫格边界，准确率 > 80%
- [ ] 对中等复杂纹理（70-90%）标记 needsReview = true
- [ ] 对高复杂纹理（< 70%）标记 needsReview = true，不设置边界值
- [ ] 九宫格数据写入 JSON 配置（left, right, top, bottom 像素值）
- [ ] 单元测试覆盖：简单纹理、渐变、复杂图案、空图片

**回归风险评估：**
- 影响范围：无（新功能，无现有依赖）
- 缓解措施：N/A

---

### 切片 D: 布局模式识别

**依赖：** 切片 A（需要 LayerTree 检测子节点排列）
**风险等级：** 低（几何规则相对简单）
**涉及文件：** `src/detector/layout-detector.ts`, `tests/unit/detector.test.ts`

**内容：** 检测垂直/水平/网格等距排列，标记为 VerticalLayoutGroup/HorizontalLayoutGroup/GridLayoutGroup 候选。检测重复子节点（模板折叠候选），相似度判定：位置、大小、图层结构。间距误差容忍 ±2px。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `LayoutDetector.detect()` | `detect(parent: Layer): LayoutInfo[]` | 检测父节点下的布局模式，返回布局信息列表 |

**数据契约：**

```typescript
// 切片 D 暴露给切片 E 的布局数据
interface LayoutInfo {
  parentId: string;                    // 父节点 ID
  type: 'VerticalLayoutGroup' | 'HorizontalLayoutGroup' | 'GridLayoutGroup';
  children: string[];                  // 子节点 ID 列表
  spacing: number;                     // 间距（px）
  templateMode: boolean;               // 是否为模板模式（重复子节点）
  templateId?: string;                 // 模板节点 ID（templateMode = true 时）
}
```

**行为契约：**
- 输入：LayerTree（来自切片 A）
- 输出：LayoutInfo[]（布局候选列表）
- 检测策略：
  1. 垂直等距排列：子节点 Y 坐标间距误差 ±2px → type = 'VerticalLayoutGroup'
  2. 水平等距排列：子节点 X 坐标间距误差 ±2px → type = 'HorizontalLayoutGroup'
  3. 网格排列：行列间距独立检测 → type = 'GridLayoutGroup'
  4. 重复子节点：位置、大小、图层结构相似度 > 80% → templateMode = true
- 性能要求：100 个子节点检测 < 1 秒

**验收标准：**
- [ ] 检测垂直等距排列（Vertical Layout 候选），间距误差容忍 ±2px
- [ ] 检测水平等距排列（Horizontal Layout 候选），间距误差容忍 ±2px
- [ ] 检测网格排列（Grid Layout 候选），行列间距独立检测
- [ ] 标记重复子节点（模板折叠候选），相似度判定：位置、大小、图层结构
- [ ] 单元测试覆盖：垂直/水平/网格布局、模板检测、非布局场景

**回归风险评估：**
- 影响范围：无（新功能，无现有依赖）
- 缓解措施：N/A

---

### 切片 E: JSON 配置生成与 CLI

**依赖：** 切片 A/B/C/D（汇总所有数据）
**风险等级：** 低
**涉及文件：** `src/generator/json-generator.ts`, `src/generator/json-schema.ts`, `src/cli/index.ts`, `tests/integration/e2e.test.ts`

**内容：** 汇总 PSD 解析、组件识别、九宫格检测、布局识别的结果，生成符合规范的 JSON 配置文件。实现命令行接口 `psd-exporter parse <input.psd> --output <config.json>`。使用 Zod 进行 JSON Schema 校验，确保输出格式正确。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `JsonGenerator.generate()` | `generate(layerTree: LayerTree, components: Map<string, ComponentInfo>, slice9: Map<string, Slice9Info>, layouts: LayoutInfo[]): JsonConfig` | 汇总所有数据生成 JSON 配置 |
| `CLI.parse()` | `async parse(inputPsd: string, outputJson: string, options: CliOptions): Promise<void>` | 命令行入口，编排整个解析流程 |

**数据契约：**

```typescript
// 切片 E 输出的 JSON 配置结构
interface JsonConfig {
  version: string;              // 版本号 "1.0.0"
  metadata: PsdMetadata;        // 来自切片 A
  layers: LayerConfig[];        // 图层配置列表
  layouts: LayoutInfo[];        // 来自切片 D
}

interface LayerConfig {
  id: string;                   // 来自切片 A
  name: string;                 // 来自切片 A
  type: string;                 // 来自切片 A
  component: ComponentInfo;     // 来自切片 B
  bounds: Rect;                 // 来自切片 A
  assetPath?: string;           // 来自切片 A
  slice9?: Slice9Info;          // 来自切片 C
  children?: string[];          // 子节点 ID 列表（来自切片 A）
}
```

**行为契约：**
- 输入：LayerTree, ComponentInfo[], Slice9Info[], LayoutInfo[]（来自切片 A/B/C/D）
- 输出：JsonConfig（JSON 文件）
- CLI 命令格式：`psd-exporter parse <input.psd> --output <config.json> [--debug] [--config <config-file>]`
- JSON 格式：扁平化组件列表 + 树形图层引用（符合 FRD 决策 #7）
- 错误处理：警告而非中断，标记问题节点继续解析（符合 FRD 决策 #8）

**验收标准：**
- [ ] JSON 格式符合 FRD 中的输出物规范（version, metadata, layers, layouts 字段完整）
- [ ] 支持人工编辑修正（友好的结构，带注释说明）
- [ ] 包含元数据：PSD 文件路径、解析时间、工具版本
- [ ] 命令行接口 `psd-exporter parse <input> --output <json>` 可正常执行
- [ ] 集成测试：端到端解析 PSD → 生成 JSON → 验证 JSON 格式正确
- [ ] Zod Schema 校验通过

**回归风险评估：**
- 影响范围：无（新功能，无现有依赖）
- 缓解措施：N/A

## 切片依赖图

```
切片 A (PSD 解析与资产导出) ← 数据源头，无依赖
  ├── 切片 B (组件语义识别) ← 高风险，优先验证
  ├── 切片 C (九宫格检测) ← 中风险，并行切片 B
  └── 切片 D (布局识别) ← 低风险，并行切片 B/C
        └── 切片 E (JSON 生成与 CLI) ← 汇总所有数据，完整用户旅程
```

**实施顺序：**
1. 切片 A（必须先完成，为后续切片提供数据）
2. 切片 B/C/D（可并行开发，互不依赖）
3. 切片 E（依赖切片 A/B/C/D 全部完成）

## 关键接口

### 切片 A → 切片 B/C/D/E

```typescript
// PSD 解析结果，所有切片的输入数据
interface LayerTree {
  root: Layer;
  metadata: PsdMetadata;
}

interface Layer {
  id: string;
  name: string;
  type: LayerType;
  bounds: Rect;
  visible: boolean;
  opacity: number;
  children?: Layer[];
  assetPath?: string;
}
```

### 切片 B → 切片 E

```typescript
// 组件识别结果
interface ComponentInfo {
  type: ComponentType;
  confidence: number;
  source: 'tag' | 'cv' | 'ai';
  needsReview: boolean;
}
```

### 切片 C → 切片 E

```typescript
// 九宫格检测结果
interface Slice9Info {
  left: number;
  right: number;
  top: number;
  bottom: number;
  confidence: number;
  needsReview: boolean;
}
```

### 切片 D → 切片 E

```typescript
// 布局识别结果
interface LayoutInfo {
  parentId: string;
  type: 'VerticalLayoutGroup' | 'HorizontalLayoutGroup' | 'GridLayoutGroup';
  children: string[];
  spacing: number;
  templateMode: boolean;
  templateId?: string;
}
```

### 切片 E 对外暴露

```typescript
// CLI 入口
psd-exporter parse <input.psd> --output <config.json> [--debug] [--config <config-file>]

// JSON 配置输出
interface JsonConfig {
  version: string;
  metadata: PsdMetadata;
  layers: LayerConfig[];
  layouts: LayoutInfo[];
}
```

## 回归风险评估

| 影响范围 | 风险等级 | 缓解措施 |
|---------|---------|----------|
| 无现有依赖 | 无风险 | 新功能独立开发，不影响现有系统 |

**说明：** 本功能为全新的 Node.js 工具链，独立于现有 Unity 项目和 PSD2UGUI 工具，不涉及现有功能的回归风险。后续与 Unity 插件（FRD #2）的集成在下一阶段设计中处理。

## 技术债务与优化空间

| 项 | 现状 | 优化方向 | 优先级 |
|----|------|----------|--------|
| CV 启发式规则 | 第一版仅覆盖简单组件（矩形、单图片） | 扩展规则库，支持更多复杂组件 | P2（Phase 2） |
| AI 调用并发 | 串行调用 Claude API | 批量并发调用，降低总耗时 | P2（性能优化） |
| 九宫格复杂渐变 | 相似度 < 70% 直接标记"需审查" | 研究更高级的 CV 算法（如边缘检测） | P3（长期优化） |
| 配置热重载 | 需重启进程加载新配置 | 支持配置文件监听和热重载 | P3（便利性） |
| 缓存机制 | 每次解析重新识别所有组件 | 本地缓存 AI 识别结果（基于图层哈希） | P2（成本优化） |

## 实施建议（分阶段交付）

### Phase 1: MVP（标签 + AI，跳过 CV）
- 切片 A: PSD 解析与资产导出
- 切片 B: 组件识别（仅标签 + AI，跳过 CV 启发式）
- 切片 E: JSON 生成与 CLI（不包含九宫格和布局）
- **验收目标：** 端到端流程走通，80% 有标签的 PSD 可正常解析

### Phase 2: 完整功能（增加 CV + 九宫格 + 布局）
- 切片 B: 增加 CV 启发式识别
- 切片 C: 九宫格检测
- 切片 D: 布局识别
- 切片 E: 完善 JSON 输出（包含所有数据）
- **验收目标：** 符合 FRD 所有验收条件

### Phase 3: 优化与扩展（根据实际使用反馈）
- 扩展 CV 规则库
- AI 调用并发优化
- 缓存机制
- 配置热重载

## 与 FRD #2 的集成点

**下游接口约定：**
- FRD #2（Unity Prefab 生成器）将读取本设计输出的 JSON 配置文件
- JSON 配置结构已在本设计中定义（JsonConfig），FRD #2 设计时需遵循此契约
- 集成点验收：FRD #2 可正确解析本设计输出的 JSON，生成对应的 Unity Prefab

**待 FRD #2 设计时确认：**
- Unity 插件是否需要额外的字段（如锚点模式、Pivot 信息）
- JSON 配置的版本兼容策略（向后兼容）
- 错误处理和容错机制（JSON 格式错误时的降级方案）
