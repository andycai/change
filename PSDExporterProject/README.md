# PSD Exporter - PSD 转 Unity UGUI JSON 转换工具

一个 Node.js 工具，用于解析 PSD 文件并通过标签解析和 AI 识别自动分析 UI 组件属性，输出用于 Unity UGUI 的 JSON 配置文件。

## 功能特性

- **PSD 解析**：使用 ag-psd 库解析 PSD 文件，提取完整图层树
- **组件识别**：二级识别策略
  - 第一级：基于标签的识别（兼容 PSD2UGUI）
  - 第二级：使用 Claude Vision API 的 AI 识别
- **资产导出**：使用 sharp 将图像图层导出为 PNG 文件
- **JSON 生成**：通过 Zod schema 验证生成结构化 JSON 配置
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
  -o, --output <path>   JSON 输出目录 (默认: "./output")
  -c, --config <path>   配置文件路径 (默认: "./psd-exporter.config.json")
  -d, --debug          启用调试模式
  --assets <path>      资产输出目录 (默认: "./output/assets")
  -h, --help           显示帮助信息
```

### 示例

```bash
# 使用默认设置解析 PSD
node dist/cli/index.js parse ./test.psd

# 使用自定义输出目录解析
node dist/cli/index.js parse ./test.psd -o ./my-output

# 使用调试模式解析
node dist/cli/index.js parse ./test.psd -d

# 使用自定义配置解析
node dist/cli/index.js parse ./test.psd -c ./my-config.json
```

## PSD 图层命名规范（兼容 PSD2UGUI）

在图层名称中使用标签前缀以实现自动组件类型识别：

| 标签 | 组件类型 | 示例 |
|-----|----------|------|
| `btn_` | Button（按钮） | `btn_close`, `btn_submit` |
| `txt_` | Text（文本） | `txt_title`, `txt_description` |
| `img_` | Image（图片） | `img_logo`, `img_avatar` |
| `sv_` | ScrollView（滚动视图） | `sv_content`, `sv_list` |
| `ipt_` | InputField（输入框） | `ipt_username`, `ipt_password` |
| `vbox_` | VerticalLayoutGroup（垂直布局组） | `vbox_menu`, `vbox_items` |
| `hbox_` | HorizontalLayoutGroup（水平布局组） | `hbox_toolbar`, `hbox_buttons` |
| `grid_` | GridLayoutGroup（网格布局组） | `grid_inventory`, `grid_icons` |

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
      "name": "btn_close",
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
PSD 文件 → 解析器 → 组件识别器 → JSON 生成器 → 输出
                        ↓
                   标签解析器
                        ↓
                   AI 识别器
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
- `src/generator/`：使用 Zod 验证的 JSON 生成
- `src/config/`：配置加载器
- `src/utils/`：日志工具
- `src/cli/`：命令行接口

## 测试

项目包含全面的测试覆盖：

- 所有模块的单元测试
- 端到端工作流的集成测试
- 总计：92 个测试通过

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

### v1.0.0 (2026-06-24)

- Phase 1 MVP 初始版本
- 使用 ag-psd 进行 PSD 解析
- 基于标签的组件识别
- 使用 Claude Vision 的 AI 识别
- 使用 Zod 验证的 JSON 输出
- 支持配置的 CLI 接口
- 全面的测试套件（92 个测试）
