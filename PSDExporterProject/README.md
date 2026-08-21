# PSD Exporter - PSD 转 Unity UGUI 导出工具

一个 Node.js 工具，用于解析 PSD 文件并通过标签解析和 AI 识别自动分析 UI 组件属性，输出用于 Unity UGUI 的 JSON 配置文件、图层切图、完整排版 HTML 预览，以及 FairyGUI Editor 6.x 可继续编辑的源工程。

## 功能特性

- **PSD 解析**：使用 ag-psd 库解析 PSD 文件，提取完整图层树
- **组件识别**：二级识别策略
  - 第一级：基于标签的识别（兼容 PSD2UGUI）
  - 第二级：使用 Claude Vision API 的 AI 识别
- **资产导出**：使用 sharp 将图像图层导出为 PNG 文件
- **JSON 生成**：通过 Zod schema 验证生成结构化 JSON 配置
- **HTML 交互预览**：按 PSD 图层坐标拼装 DOM，以切图作为视觉皮肤，并为 Button、Toggle、InputField、Slider、Dropdown、ScrollView 生成真实可操作控件
- **FairyGUI 源工程**：生成或安全接入 FairyGUI 6.x 工程；一个包可容纳多个 PSD 页面，受管清单保护人工资源和其他页面
- **CLI 接口**：易于使用的命令行界面
- **可配置**：支持配置文件和环境变量
- **结构化日志**：带详细日志的调试模式

## 安装

```bash
cd PSDExporterProject
npm install
```

## 配置

### 方式 1：配置文件

在项目根目录创建 `psd-exporter.config.json`：

```json
{
  "aiThreshold": 0.7,
  "cvConfidenceMin": 0.6,
  "enableAI": true,
  "debug": false,
  "fairyGui": {
    "projectPath": "../UIProject",
    "packageName": "PSDImport",
    "sourceRoot": "../UIArtifacts/psd",
    "defaultScale9": { "unit": "ratio", "left": 0.3, "top": 0.3, "right": 0.3, "bottom": 0.3 },
    "fontMappings": {},
    "references": { "local": {}, "external": {} },
    "pages": {}
  },
  "claudeApiKey": "YOUR_CLAUDE_API_KEY_HERE"
}
```

### 方式 2：环境变量

```bash
export CLAUDE_API_KEY="your-api-key-here"
```

### 配置选项

- `aiThreshold`：AI 识别的置信度阈值（0-1，默认：0.7）
- `cvConfidenceMin`：组件验证的最低置信度（0-1，默认：0.6）
- `enableAI`：启用 AI 识别（默认：true）
- `debug`：启用调试日志（默认：false）
- `claudeApiKey`：Claude API 密钥，用于 AI 识别（可选，可使用环境变量）

## 使用方法

### 基本用法

```bash
npm run build
node dist/cli/index.js parse <psd文件路径> -o <输出目录>
```

### CLI 选项

```bash
选项:
  -o, --output <path>   JSON 输出文件或目录 (默认: "output.json")
  -c, --config <path>   配置文件路径
  -d, --debug          启用调试模式
  -a, --assets <path>  切图输出目录
  --target <target>    导出目标：ugui、fairygui 或 all（默认 ugui）
  --fairygui-project <dir>    FairyGUI 工程目录（fairygui/all 必填，除非配置已提供）
  --fairygui-package <name>   FairyGUI 包名
  --fairygui-component <name> 根组件名
  --fairygui-page-id <id>     稳定页面 ID
  --fairygui-source-root <dir> 页面源路径根目录
  --fairygui-adopt-existing   显式接管同名人工根组件
  -h, --help           显示帮助信息
```

### 示例

```bash
# 使用默认设置解析 PSD
node dist/cli/index.js parse ./test.psd

# 使用自定义输出目录解析
node dist/cli/index.js parse ./test.psd -o ./my-output

# 同时导出切图；生成 my-output/test.json、my-output/test.html 和 assets 下的 PNG
node dist/cli/index.js parse ./test.psd -o ./my-output -a ./my-output/assets

# 使用调试模式解析
node dist/cli/index.js parse ./test.psd -d

# 使用自定义配置解析
node dist/cli/index.js parse ./test.psd -c ./my-config.json

# 只生成 FairyGUI Editor 6.x 源工程
node dist/cli/index.js parse ./test.psd \
  --target fairygui \
  --fairygui-project ../UIProject \
  --fairygui-package PSDImport \
  --fairygui-page-id main-menu

# 单次解析，同时生成旧 UGUI/HTML 产物和 FairyGUI 源工程
node dist/cli/index.js parse ./test.psd \
  --target all -o ./build -a ./build/assets \
  --fairygui-project ../UIProject
```

FairyGUI 导出不会自动调用发布器，也不会生成 Unity 运行时发布资源；在 FairyGUI Editor 中打开 `.fairy` 工程后继续人工检查和发布。受管页面以 PSD 为事实源，重导会重建该页面，但保留同包中的人工资源、其他 PSD 页面和未知 `package.xml` 属性。同一个 `pageId` 不能被不同 PSD 静默复用；历史相对 `sourcePath` 只有在当前显式 `sourceRoot` 下能精确解析时才允许迁移，若 manifest 已记录 `sourceRoot` 则两者还必须一致，否则在提交前阻断。

每页的清单、报告和事务状态位于工程 `.psd-exporter/`。重导前会核对旧 manifest 中的资源 ID、包路径、文件路径和内容 hash，损坏或篡改的 sidecar 不能取得人工资源清理权；自动发现和显式配置的本地组件引用都必须存在对应 XML。generation 只签入当前页面实际消费的引用目标及其内容 hash，其他页面或无关人工资源不会触发换代。JSON/Markdown 报告包含逐层 FairyGUI 映射、结构化 AI 建议、字体回退、文本栅格化、九宫格推断、引用降级和受管文件；没有 Photoshop `sourceId` 的图层使用祖先名称与几何路径生成稳定身份并报告降级，截断哈希碰撞使用确定性 salt 重算。报告在工程提交后原子写入；若报告写入失败，API 返回 `reportStatus: "failed"` 和 `REPORT_WRITE_FAILED`，不会把已经提交成功的 FairyGUI 工程误报为整体失败。

每次成功导出都会在 JSON 旁生成同基础名的 HTML。例如 `-o ./build/menu.json` 会同时生成 `./build/menu.html`。HTML 使用 PSD 图层坐标、文本内容和导出的 PNG 切图拼装页面，因此需要保留 `-a` 指定的切图目录；直接用浏览器打开即可操作按钮、开关、输入框、Slider、Dropdown 和 ScrollView。

## PSD 图层命名规范（兼容 PSD2UGUI）

图层名称使用**点号后缀标签语法**：`layerName.tag1.tag2.tag3`，标签按右到左解析，同一分类下最右侧的标签生效。标签分为 4 个分类（family），均可在 `config/tag-config.json` 中配置：

### main（组件类型，14 个）

| 标签 | 组件类型 | 示例 |
|-----|----------|------|
| `img` | Image（图片） | `logo.img` |
| `rimg` | RawImage（原始图片） | `photo.rimg` |
| `txt` | Text（文本） | `title.txt` |
| `msk` | Mask（遮罩） | `clip.msk` |
| `col` | FillColor（填充色） | `overlay.col` |
| `bt` | Button（按钮） | `close.bt` |
| `dpd` | Dropdown（下拉框） | `lang.dpd` |
| `ipt` | InputField（输入框） | `username.ipt` |
| `tg` | Toggle（开关） | `sound.tg` |
| `sld` | Slider（滑动条） | `volume.sld` |
| `sv` | ScrollView（滚动视图） | `content.sv` |
| `vbox` | VerticalLayoutGroup（垂直布局组） | `menu.vbox` |
| `hbox` | HorizontalLayoutGroup（水平布局组） | `toolbar.hbox` |
| `grid` | GridLayoutGroup（网格布局组） | `inventory.grid` |

### textBackend（文本渲染后端，2 个）

| 标签 | 说明 |
|-----|------|
| `tmp` | TextMeshPro |
| `ugui` | 原生 UGUI Text |

### imageType（图片类型，4 个）

| 标签 | 说明 |
|-----|------|
| `simple` | Simple |
| `sliced` | Sliced（九宫格） |
| `tiled` | Tiled（平铺） |
| `filled` | Filled（填充） |

### role（语义角色，22 个）

| 标签 | 说明 | 标签 | 说明 |
|-----|------|-----|------|
| `bg` | Background | `mark` | Toggle_Checkmark |
| `onover` | Button_Highlight | `tglb` | Toggle_Label |
| `press` | Button_Press | `fill` | Slider_Fill |
| `select` | Button_Select | `handle` | Slider_Handle |
| `disable` | Button_Disable | `vpt` | ScrollView_Viewport |
| `bttxt` | Button_Text | `hbarbg` | ScrollView_HorizontalBarBG |
| `dpdlb` | Dropdown_Label | `hbar` | ScrollView_HorizontalBar |
| `dpdicon` | Dropdown_Arrow | `vbarbg` | ScrollView_VerticalBarBG |
| `placeholder` | InputField_Placeholder | `vbar` | ScrollView_VerticalBar |
| `ipttxt` | InputField_Text | `content` | ScrollView_Content |
| | | `item` | ScrollView_Item |
| | | `template` | Dropdown_Template |

### 组合示例

```
close.bt              → Button
close.bt.bg           → Button 的背景图层
title.txt.tmp         → 使用 TextMeshPro 的 Text
icon.img.sliced       → 使用九宫格的 Image
ref icon.img          → 引用预制体（prefix='ref'，baseName='icon'）
```

**前缀支持：** `ref` / `refp` 前缀（用空格分隔在标签最前）用于引用预制体或脚本，如 `ref icon.img` → `prefix='ref'`, `baseName='icon'`。

> 旧版 `btn_` / `txt_` / `img_` 等下划线前缀写法已被点号后缀语法取代。

为兼容已有 PSD 资源，解析器也接受以下点号别名：`btn`/`tmpbtn` → Button、`tmptxt` → Text、`fillcolor` → FillColor、`tips` → InputField placeholder、`iptlb` → InputField text、`label` → Toggle/Dropdown label。

## 输出格式

该工具生成如下结构的 JSON 文件：

```json
{
  "metadata": {
    "psdPath": "/path/to/source.psd",
    "canvasSize": { "x": 0, "y": 0, "width": 1920, "height": 1080 },
    "timestamp": "2026-06-24T10:00:00.000Z",
    "exportedBy": "psd-exporter v1.0.0"
  },
  "layers": [
    {
      "id": "root_0",
      "name": "close.bt",
      "type": "group",
      "bounds": { "x": 100, "y": 50, "width": 80, "height": 80 },
      "visible": true,
      "opacity": 1.0,
      "component": {
        "type": "Button",
        "confidence": 1.0,
        "source": "tag",
        "needsReview": false
      }
    }
  ]
}
```

## 开发
      "type": "group",
      "bounds": { "x": 100, "y": 50, "width": 80, "height": 80 },
      "visible": true,
      "opacity": 1.0,
      "component": {
        "type": "Button",
        "confidence": 1.0,
        "source": "tag",
        "needsReview": false
      }
    }
  ]
}
```

## 开发

### 构建

```bash
npm run build
```

### 测试

```bash
npm test
```

### 代码检查

```bash
npm run lint
```

### 代码格式化

```bash
npm run format
```

## 架构

### 管道架构

```
PSD 文件 → PsdParser → ParsedPsdDocument（图层树 + RGBA + mask）
                              │
                        ExportService
                       ╱             ╲
              UGUI / HTML             FairyGUI
           组件识别 → JSON/DOM    组件识别 → FairyScene
                                      ↓
                           XML Writer + 事务提交
                                      ↓
                         工程资源 + manifest + 报告
```

### 二级识别策略

1. **标签解析**（第一级）
   - 解析图层名称中的 PSD2UGUI 标签
   - 置信度：1.0（100%）
   - 快速且确定

2. **AI 识别**（第二级）
   - 使用 Claude Vision API 进行图像分析
   - 置信度：0.0-1.0（可变）
   - 标签解析失败时的后备方案

### 模块

- `src/parser/`：PSD 解析和资产导出
- `src/recognizer/`：组件识别（标签 + AI）
- `src/export/`：复用单次解析结果并编排 UGUI、HTML 与 FairyGUI 导出目标
- `src/generator/`：使用 Zod 验证的 JSON 生成和基于图层 DOM 的 HTML 交互预览生成
- `src/fairygui/`：场景构建、稳定身份、组件 XML、资源处理、事务提交与结构化报告
- `src/config/`：配置加载器
- `src/utils/`：日志工具
- `src/cli/`：命令行接口

## 测试

项目包含全面的测试覆盖：

- 所有模块的单元测试
- 端到端工作流的集成测试
- 当前全量回归：26 个测试套件、310 个测试通过

```bash
npm test
```

## Phase 1 MVP 限制

这是 Phase 1 MVP 版本。以下功能计划在未来阶段实现：

- **Phase 2**：增强识别的计算机视觉启发式
- **Phase 2**：九宫格检测用于精灵切片
- **Phase 2**：自动布局组的布局分析

## 系统要求

- Node.js 18.x 或更高版本
- TypeScript 5.x
- Claude API 密钥（用于 AI 识别）

## 依赖

- `ag-psd`：PSD 文件解析
- `sharp`：图像处理和导出
- `@anthropic-ai/sdk`：Claude API 客户端
- `commander`：CLI 框架
- `zod`：Schema 验证
- `jest`：测试框架

## 许可证

MIT

## 贡献

本项目遵循 TDD（测试驱动开发）原则。所有代码更改必须包含测试。

1. 先编写测试
2. 实现功能
3. 确保所有测试通过
4. 提交 Pull Request

## 支持

如有问题或功能请求，请在代码仓库中提交 Issue。

## 更新日志

### v1.1.0 (2026-06-25)

- 标签解析器升级为点号后缀语法：`layerName.tag1.tag2.tag3`
- 标签按右到左解析，同一分类下最右侧标签生效
- 标签分类扩展为 4 个 family：main / textBackend / imageType / role
- 新增 `config/tag-config.json` 可配置标签表，通过 `TagConfigLoader` + Zod 校验加载
- 新增组件类型：RawImage、Dropdown、Toggle、Slider、Mask、FillColor
- `ref` / `refp` 前缀支持，用于引用预制体或脚本
- 测试套件扩展至 148 个测试

### v1.0.0 (2026-06-24)

- Phase 1 MVP 初始版本
- 使用 ag-psd 进行 PSD 解析
- 基于标签的组件识别
- 使用 Claude Vision 的 AI 识别
- 使用 Zod 验证的 JSON 输出
- 支持配置的 CLI 接口
- 全面的测试套件（92 个测试）
