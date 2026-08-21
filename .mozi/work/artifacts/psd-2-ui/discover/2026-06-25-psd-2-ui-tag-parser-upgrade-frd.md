# PSD 标签解析器升级 功能需求文档

> 日期: 2026-06-25 | 状态: 草稿
> 父功能: PSD 转 UGUI 自动化工具链 ([README](./README.md))

## 概述

重构 TagParser 以完整兼容 PSD2UGUI-LayerTagMenu.jsx 的标签体系，从简化的前缀式标签（`btn_name`）升级为点号后缀式多层标签（`name.bt.tmp.bg`），支持 4-family 分类、多标签叠加、资源引用前缀，并通过外部配置文件维护 39 个规范标签定义。

## 用户故事

| 角色 | 场景 | 目标 |
|------|------|------|
| 程序员 | 美术使用 PSD2UGUI-LayerTagMenu.jsx 标记图层为 `close.bt.tmp.bg` | TagParser 正确解析为 Button 组件 + TMP 文本后端 + Background 角色，输出完整的语义信息到 JSON 配置 |
| 程序员 | 美术标记图层为 `ref icon.img` 表示引用公共资源 | TagParser 识别 ref 前缀并保留到解析结果，供 Unity 生成器判断引用关系 |
| 程序员 | 美术标记图层为 `panel.bt.dpd`（两个 main family 标签冲突） | TagParser 按右→左优先级规则，最终识别为 Dropdown（后面的 dpd 覆盖前面的 bt） |
| 程序员 | 美术标记图层为 `bg.unknowntag.img.sliced` | TagParser 跳过未识别的 unknowntag，正确解析为 Image + Sliced 类型 |
| 维护者 | 需要新增或修改标签定义 | 修改外部配置文件 tag-config.json，无需修改代码和重新编译 |

## 功能边界

**范围内：**
- **标签格式切换** — 从前缀式（`btn_name`）改为点号后缀式（`name.bt`）
- **4-family 标签体系** — 支持 main / textBackend / imageType / role 四层分类
- **规范标签解析** — 解析 39 个规范标签（11 main + 2 textBackend + 4 imageType + 22 role）
- **多标签叠加** — 支持单个图层名包含多个标签（如 `close.bt.tmp.bg`）
- **右→左解析优先级** — 点号分隔的标签从右往左逐个解析，同 family 后面的覆盖前面的
- **ref/refp 前缀支持** — 识别并保留资源引用前缀到解析结果
- **外部配置文件** — 从 JSON 文件加载标签定义，支持运行时加载
- **宽松解析策略** — 跳过未识别标签，无标签返回 null，不中断流程
- **类型系统扩展** — ComponentInfo 新增 textBackend / imageType / role 可选字段
- **TagParseResult 结构** — 独立的解析结果类型（包含 prefix + baseName + families）
- **单元测试覆盖** — 多标签、优先级、前缀、边界情况的完整测试

**范围外：**
- **不实现别名映射** — 不支持 `button` → `bt` 等 132 个别名转换（美术使用 JSX 菜单已写入规范标签）
- **不修改 JSX 菜单** — PSD2UGUI-LayerTagMenu.jsx 保持不变
- **不改变 AI 识别逻辑** — AiIdentifier 和 ComponentRecognizer 保持现有接口
- **不实现资源引用推断** — 基于图层结构的引用识别由后续 Unity 生成器实现
- **不重写现有测试** — 只更新受 TagParser 影响的测试用例
- **不迁移现有 PSD** — 旧格式（`btn_name`）的 PSD 需要美术手动更新或走 AI 识别

## 验收条件

### 核心解析功能
- [ ] 解析 `close.bt.tmp.bg` 返回 `{main: 'bt', textBackend: 'tmp', role: 'bg'}`
- [ ] 解析 `panel.bt.dpd` 返回 `{main: 'dpd'}`（后面优先）
- [ ] 解析 `icon.img.sliced` 返回 `{main: 'img', imageType: 'sliced'}`
- [ ] 解析 `input.ipt.tmp.placeholder` 返回 `{main: 'ipt', textBackend: 'tmp', role: 'placeholder'}`

### 前缀处理
- [ ] 解析 `ref icon.img` 返回 `{prefix: 'ref', baseName: 'icon', families: {main: 'img'}}`
- [ ] 解析 `refp panel.bt` 返回 `{prefix: 'refp', baseName: 'panel', families: {main: 'bt'}}`
- [ ] 解析 `ref close button.bt.tmp` 返回 `{prefix: 'ref', baseName: 'close button', families: {main: 'bt', textBackend: 'tmp'}}`

### 边界情况
- [ ] 解析 `name.unknowntag.bt` 跳过 unknowntag，返回 `{main: 'bt'}`
- [ ] 解析 `background`（无标签）返回 null
- [ ] 解析 `name.xyz.abc.def`（全是未识别标签）返回空 families 对象

### 配置文件
- [ ] 成功加载 tag-config.json（39 个标签定义）
- [ ] 配置文件不存在时抛出明确错误
- [ ] 配置文件格式错误时抛出校验错误

### 类型系统集成
- [ ] ComponentInfo 包含 textBackend / imageType / role 可选字段
- [ ] ComponentRecognizer.recognize() 正确填充扩展字段
- [ ] 现有 AI 识别流程不受影响（标签优先 → AI 兜底）

### 代码质量
- [ ] TypeScript 类型安全，无 any 类型
- [ ] 单元测试覆盖率 > 80%
- [ ] 通过 ESLint + Prettier 检查
- [ ] 所有现有集成测试通过（或按需更新）

## Decisions

| # | 决策点 | 选择 | 理由 | 影响范围 |
|---|--------|------|------|----------|
| 1 | 别名映射 | 不实现（去掉 132 个别名） | 美术使用 JSX 菜单已写入规范缩写，解析器只负责读取，不需要猜测用户输入；AI 兜底非标签名称 | 配置文件大小、解析复杂度、维护成本 |
| 2 | 标签格式 | 点号后缀式（`name.bt.tmp`） | 完整兼容 PSD2UGUI-LayerTagMenu.jsx，支持多标签叠加 | 解析逻辑、现有 PSD 迁移 |
| 3 | 解析方向 | 右→左（从最后一个 `.` 开始） | 符合 JSX 标签消费逻辑，同 family 后面优先 | 解析算法、优先级规则 |
| 4 | 配置文件格式 | JSON（tag-config.json） | 类型安全（配合 zod 校验），易于编辑，与 JSX 配置结构对齐 | 加载机制、Schema 校验 |
| 5 | 配置文件位置 | `PSDExporterProject/config/tag-config.json` | 与代码分离，独立维护，便于版本控制 | 项目结构、加载路径 |
| 6 | ref/refp 处理 | 识别并保留到 TagParseResult，不污染 ComponentInfo | 保持组件类型系统清晰，引用语义由 Unity 生成器处理 | 类型设计、下游集成 |
| 7 | 未识别标签策略 | 跳过并继续解析（宽松模式） | 部分标签错误不影响整体可用性，程序员可选择性修正 | 健壮性、用户体验 |
| 8 | 无标签返回值 | 返回 null | 明确表示"无标签"状态，触发 AI 识别流程 | ComponentRecognizer 逻辑 |
| 9 | ComponentInfo 扩展 | 新增 textBackend / imageType / role 可选字段 | 保持粗粒度 ComponentType（11 个 main family），通过元数据表达细分语义 | 类型定义、JSON Schema、Unity 生成器 |
| 10 | TagParseResult 结构 | 独立类型（prefix + baseName + families） | 与 ComponentInfo 解耦，TagParser 专注解析，ComponentRecognizer 负责组装 | 类型设计、职责分离 |

## 未决问题

| # | 问题 | 建议解决阶段 |
|---|------|-------------|
| 1 | 配置文件的加载时机（启动时一次性加载 vs 每次解析时加载） | design - 需要权衡性能和灵活性 |
| 2 | 是否需要标签校验 API（供 Unity 编辑器插件调用，实时校验美术输入） | design - 取决于 Unity 端需求 |
| 3 | 未来是否支持自定义扩展标签（项目特定标签） | explore - 取决于团队工作流和扩展性需求 |

## 约束与假设

**技术约束：**
- Node.js 版本 ≥ 18.x（ES2022 支持）
- TypeScript 5.x 类型系统
- 配置文件必须符合 JSON 规范且通过 Schema 校验

**规范约束：**
- 美术使用 PSD2UGUI-LayerTagMenu.jsx 标记图层（写入规范标签，非别名）
- 标签使用点号分隔（`.`），标签 ID 使用小写字母
- ref/refp 前缀与后续名称用空格分隔（`ref name` 非 `refname`）

**假设：**
- 美术理解并遵循 JSX 标签体系
- 现有 PSD 如果使用旧格式（`btn_name`），会走 AI 识别流程或手动更新
- Unity 生成器会基于 ref/refp 前缀实现资源引用逻辑

## 技术选型摘要

**核心依赖：**
- `zod` - 配置文件 Schema 校验
- 现有：`ag-psd`, `sharp`, `commander`（不变）

**新增文件：**
- `config/tag-config.json` - 标签定义配置
- `src/recognizer/tag-parse-result.ts` - TagParseResult 类型定义
- `src/recognizer/tag-config-loader.ts` - 配置加载器
- `tests/recognizer/tag-parser.test.ts` - 单元测试

**修改文件：**
- `src/recognizer/tag-parser.ts` - 核心重构
- `src/recognizer/component-types.ts` - 扩展 ComponentInfo 接口
- `src/recognizer/component-recognizer.ts` - 适配新接口

## 输出物规范

### TagParseResult 结构

```typescript
interface TagParseResult {
  prefix?: 'ref' | 'refp';
  baseName: string;
  families: {
    main?: string;
    textBackend?: string;
    imageType?: string;
    role?: string;
  };
}
```

### ComponentInfo 扩展

```typescript
interface ComponentInfo {
  type: ComponentType;  // 现有：Button, Image, Text, ...
  textBackend?: 'tmp' | 'ugui';  // 新增
  imageType?: 'simple' | 'sliced' | 'tiled' | 'filled';  // 新增
  role?: string;  // 新增：bg, press, fill, handle, ...
  confidence: number;
  source: 'tag' | 'cv' | 'ai';
  needsReview: boolean;
}
```

### tag-config.json 结构

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
      {"id": "vbar", "label": "ScrollView_VerticalBar"}
    ]
  }
}
```

## 实施建议

**Phase 1：类型定义与配置**
1. 定义 TagParseResult 接口
2. 创建 tag-config.json
3. 实现配置加载器（带 zod 校验）

**Phase 2：核心解析逻辑**
4. 重写 TagParser.parse() 方法
5. 实现右→左解析算法
6. 实现 ref/refp 前缀识别

**Phase 3：集成与扩展**
7. 扩展 ComponentInfo 接口
8. 更新 ComponentRecognizer 适配新接口
9. 更新受影响的测试用例

**Phase 4：测试与验证**
10. 编写单元测试（15-20 个用例）
11. 运行集成测试
12. 手动测试典型 PSD 场景
