# PSD 解析与智能分析管线 功能需求文档

> 日期: 2026-06-24 | 状态: 草稿
> 父功能: PSD 转 UGUI 自动化工具链 ([README](./README.md))

## 概述

建立 Node.js 工具链，解析 PSD 文件并通过 CV 算法和 AI 识别自动分析 UI 组件属性，输出包含完整 UI 结构和语义信息的 JSON 配置文件，为后续 Unity Prefab 生成提供数据基础。

## 用户故事

| 角色 | 场景 | 目标 |
|------|------|------|
| 程序员 | 拿到美术提供的 PSD 设计稿（已通过 PSD2UIForm 优化导出） | 运行一条命令，自动解析 PSD 并生成包含组件类型、九宫格、布局等完整信息的 JSON 配置文件 |
| 程序员 | PSD 中某些组件 AI 识别不准确 | 查看生成的 JSON 配置文件，手动修正组件类型或九宫格参数，然后继续后续流程 |
| 程序员 | 美术在 PSD 中已按现有标签规范命名图层（btn_xxx, txt_xxx 等） | 工具直接识别标签，不调用 AI，快速准确生成配置 |

## 功能边界

**范围内：**
- PSD 文件无损解析（使用 ag-psd 库）
- 图层树结构导出为 JSON
- 图片资产切片导出（PNG 格式）
- 九宫格自动检测（CV 像素相似度分析，分级策略：自动/需审查/手动）
- 组件语义识别：
  - 优先解析现有标签体系（btn, txt, img, sv, ipt 等几十种标签）
  - 无标签时通过 Codex/Claude Code AI 识别组件类型
- 布局模式识别（等距排列检测，标记为 Vertical/Horizontal/Grid Layout 候选）
- JSON 配置文件生成（包含图层树、组件类型、九宫格数据、布局信息、位置锚点原始数据）
- 命令行接口（`psd-exporter parse <input.psd> --output <config.json>`）

**范围外：**
- 不生成 Unity Prefab（由 FRD #2 实现）
- 不生成 C# 代码（由 FRD #2 实现）
- 不处理 PSD 优化导出（美术需先用 PSD2UIForm 脚本处理）
- 不支持动画/特效解析（仅静态 UI 结构）
- 不提供 GUI 界面（仅命令行工具）

## 验收条件

### Phase 1：基础解析（必须）
- [ ] 能成功解析符合规范的 PSD 文件（图层嵌套 ≤10 层，无 3D/视频图层）
- [ ] 导出完整的图层树 JSON（包含图层名称、类型、位置、大小、可见性、嵌套关系）
- [ ] 正确切片导出所有需要的图片资产（Image 图层、Shape 图层栅格化）
- [ ] 图片命名规则：`<图层名>_<图层ID>.png`，避免重名冲突

### Phase 2：九宫格检测（必须）
- [ ] 对简单纹理（像素相似度 > 90%）自动检测九宫格边界，准确率 > 80%
- [ ] 对中等复杂纹理（70-90%）标记为"需审查"
- [ ] 对高复杂纹理（< 70%）标记为"手动处理"，不自动设置九宫格
- [ ] 九宫格数据写入 JSON 配置（left, right, top, bottom 像素值）

### Phase 3：组件语义识别（必须）
- [ ] 正确解析现有标签体系（btn, txt, img, sv, ipt 等），匹配 PSD2UGUI-LayerTagMenu.jsx 的标签映射
- [ ] 对无标签的图层组，通过 Codex/Claude Code 识别组件类型（Button, ScrollView, InputField 等）
- [ ] AI 识别准确率目标：> 70%（常见组件如按钮、列表、输入框）
- [ ] 识别结果包含置信度，低置信度（< 60%）标记为"需审查"

### Phase 4：布局识别（必须）
- [ ] 检测垂直等距排列（Vertical Layout 候选），间距误差容忍 ±2px
- [ ] 检测水平等距排列（Horizontal Layout 候选），间距误差容忍 ±2px
- [ ] 检测网格排列（Grid Layout 候选），行列间距独立检测
- [ ] 标记重复子节点（模板折叠候选），相似度判定：位置、大小、图层结构

### Phase 5：JSON 配置（必须）
- [ ] JSON 格式规范化，包含所有必要字段
- [ ] 支持人工编辑修正（友好的结构，带注释说明）
- [ ] 包含元数据：PSD 文件路径、解析时间、工具版本

## Decisions

| # | 决策点 | 选择 | 理由 | 影响范围 |
|---|--------|------|------|----------|
| 1 | PSD 解析库 | ag-psd (Node.js) | 成熟稳定，支持无损解析，社区活跃 | 技术栈、开发效率 |
| 2 | 工具链语言 | Node.js + TypeScript | 生态完善，ag-psd 原生支持，跨平台部署方便 | 技术栈、团队技能要求 |
| 3 | 九宫格检测算法 | OpenCV.js 像素相似度分析 | 经典 CV 算法，准确率高，无需 AI，成本低 | 准确率、性能、成本 |
| 4 | AI 识别接口 | 通过 Codex/Claude Code 调用 | 利用现有工具，无需额外 API Key，团队已熟悉 | 成本、易用性 |
| 5 | 标签体系 | 兼容现有 PSD2UGUI-LayerTagMenu.jsx 标签 | 美术已有使用习惯，降低迁移成本 | 用户体验、兼容性 |
| 6 | 图片导出格式 | PNG（32位 RGBA） | 保留透明度，Unity 原生支持 | 资产质量、兼容性 |
| 7 | JSON 配置结构 | 扁平化组件列表 + 树形图层引用 | 便于程序员审查修正，便于 Unity 插件遍历 | 易用性、FRD #2 实现复杂度 |
| 8 | 错误处理策略 | 警告而非中断，标记问题节点继续解析 | 部分失败不影响整体可用性，程序员可选择性修复 | 健壮性、用户体验 |
| 9 | 命令行接口设计 | `psd-exporter parse <input> --output <json>` | 简洁明确，符合 Unix 风格，便于 CI/CD 集成 | 易用性、自动化能力 |
| 10 | 工程目录 | `PSDExporterProject/` 根目录下 | 与 Unity 项目分离，独立维护和版本控制 | 项目结构、维护性 |

## 未决问题

| # | 问题 | 建议解决阶段 |
|---|------|-------------|
| 1 | Codex/Claude Code 的具体调用方式（API、CLI、其他） | design - 需要验证技术可行性和接口设计 |
| 2 | 九宫格检测对于复杂渐变（如光影渐变按钮）的准确率 | explore - 需要用实际案例测试多种 CV 算法 |
| 3 | AI 识别对于游戏特定美术风格（如二次元、写实、像素风）的泛化能力 | explore - 需要用不同风格的 PSD 样本测试 |
| 4 | 图层名包含特殊字符（中文、空格、符号）时的文件命名策略 | design - 需要定义文件名规范化规则 |
| 5 | 大型 PSD（几百个图层）的解析性能和内存占用 | design - 可能需要流式解析或分块处理 |

## 约束与假设

**技术约束：**
- Node.js 版本 ≥ 18.x（支持 ES2022）
- 美术提供的 PSD 必须先通过 PSD2UIForm 脚本优化导出
- PSD 文件大小 < 500MB（超大文件可能导致内存溢出）

**规范约束（硬约束）：**
- 图层嵌套深度 ≤ 10 层
- 禁止 3D 图层、视频图层
- 文本必须用 Photoshop 文本图层（非栅格化）
- 设计稿推荐 1920x1080，允许其他 16:9 分辨率

**规范约束（软约束）：**
- 组件命名可选（btn_xxx, txt_xxx 等），无命名时走 AI 识别
- 九宫格图片无需特殊标记，靠 CV 自动检测

**假设：**
- 美术理解并遵循最小化规范
- 程序员有基本的命令行使用能力
- 开发环境可访问 Codex/Claude Code（网络连接正常）
- Unity 项目使用 uGUI（非 UI Toolkit）

## 技术选型摘要

**核心依赖：**
- `ag-psd` - PSD 解析
- `opencv4nodejs` 或 `opencv.js` - 九宫格 CV 检测
- `sharp` - 图片处理和导出
- `commander` - 命令行接口
- `zod` - JSON Schema 校验

**开发工具：**
- TypeScript 5.x
- Node.js 18.x+
- Jest - 单元测试
- ESLint + Prettier - 代码规范

## 输出物规范

### JSON 配置文件结构（初步）

```json
{
  "version": "1.0.0",
  "metadata": {
    "psdPath": "/path/to/source.psd",
    "timestamp": "2026-06-24T10:30:00Z",
    "canvasSize": { "width": 1920, "height": 1080 }
  },
  "layers": [
    {
      "id": "layer_001",
      "name": "btn_close",
      "type": "group",
      "component": {
        "type": "Button",
        "confidence": 1.0,
        "source": "tag"
      },
      "bounds": { "x": 1800, "y": 50, "width": 80, "height": 80 },
      "children": ["layer_002", "layer_003"]
    },
    {
      "id": "layer_002",
      "name": "bg",
      "type": "image",
      "component": {
        "type": "Image",
        "imageType": "Sliced",
        "confidence": 0.95,
        "source": "cv"
      },
      "assetPath": "bg_layer_002.png",
      "slice9": {
        "left": 10,
        "right": 10,
        "top": 10,
        "bottom": 10,
        "confidence": 0.92,
        "review": false
      },
      "bounds": { "x": 0, "y": 0, "width": 80, "height": 80 }
    }
  ],
  "layouts": [
    {
      "parentId": "layer_010",
      "type": "VerticalLayoutGroup",
      "children": ["layer_011", "layer_012", "layer_013"],
      "spacing": 15,
      "templateMode": true,
      "templateId": "layer_011"
    }
  ]
}
```

详细 Schema 在 design 阶段完善。
