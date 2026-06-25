# PSD Exporter - 验收测试指南

本文档说明如何准备和使用测试 PSD 文件进行验收测试。

## 测试 PSD 文件准备

### 方式 1: 使用 Photoshop 创建测试文件

创建一个简单的 UI 界面 PSD，包含以下图层（使用点号后缀标签语法，右到左解析）：

```
test-ui.psd (1920x1080)
├── close.bt (80x80)
│   ├── close.bt.bg (80x80, 红色矩形)
│   └── close.bt.icon (40x40, X 图标)
├── title.txt.tmp (400x60, 文本: "Game Title")
├── logo.img (200x200, Logo 图片)
├── start.bt (300x80)
│   ├── start.bt.bg (300x80, 绿色矩形)
│   └── start.bt.bttxt (文本: "Start Game")
├── menu.hbox (600x80)
│   ├── settings.bt (80x80)
│   ├── help.bt (80x80)
│   └── exit.bt (80x80)
└── content.sv (1600x800, 滚动区域背景)
```

### 方式 2: 下载示例 PSD

可以从以下来源获取测试用 UI PSD 文件：

1. **免费 UI Kit**:
   - https://www.freepik.com/free-photos-vectors/game-ui
   - https://www.behance.net/search/projects?search=game%20ui%20psd

2. **Unity Asset Store**:
   - 搜索 "UI PSD" 获取免费的 UI 设计模板

### 方式 3: 使用提供的 Mock 测试

如果没有 PSD 文件，可以使用项目中的集成测试来验证功能：

```bash
cd PSDExporterProject
npm test -- tests/integration/e2e.test.ts
```

## 验收测试步骤

### 1. 准备环境

```bash
cd PSDExporterProject

# 安装依赖
npm install

# 构建项目
npm run build

# 设置 API Key（如果需要 AI 识别）
export CLAUDE_API_KEY="your-api-key-here"
```

### 2. 创建配置文件

创建 `psd-exporter.config.json`：

```json
{
  "aiThreshold": 0.7,
  "cvConfidenceMin": 0.6,
  "enableAI": true,
  "debug": true
}
```

### 3. 运行解析

```bash
# 解析 PSD 文件
node dist/cli/index.js parse /path/to/your/test-ui.psd -o ./output -d
```

### 4. 验收标准

#### 4.1 基本功能验证

- [ ] PSD 文件成功解析，无错误
- [ ] 输出目录生成 JSON 文件
- [ ] JSON 文件格式正确，可以被解析

#### 4.2 图层树解析验证

检查生成的 JSON，验证：

- [ ] `metadata` 包含正确的 PSD 路径和画布尺寸
- [ ] `layers` 数组包含所有图层
- [ ] 每个图层的 `id`、`name`、`type`、`bounds` 正确
- [ ] 嵌套图层的父子关系正确（通过 ID 引用）

#### 4.3 标签识别验证

对于使用点号后缀标签语法命名的图层（`layerName.tag1.tag2`）：

- [ ] `.bt` 图层识别为 `Button`，confidence = 1.0，source = 'tag'
- [ ] `.txt` 图层识别为 `Text`，confidence = 1.0，source = 'tag'
- [ ] `.img` 图层识别为 `Image`，confidence = 1.0，source = 'tag'
- [ ] `.rimg` 图层识别为 `RawImage`，confidence = 1.0，source = 'tag'
- [ ] `.sv` 图层识别为 `ScrollView`，confidence = 1.0，source = 'tag'
- [ ] `.hbox`/`.vbox`/`.grid` 图层识别为对应的 LayoutGroup
- [ ] 多标签组合：`close.bt.bg` → Button，附带 role=Background
- [ ] textBackend 标签：`title.txt.tmp` → Text，附带 textBackend=TextMeshPro
- [ ] imageType 标签：`icon.img.sliced` → Image，附带 imageType=Sliced

#### 4.4 AI 识别验证（如果启用）

对于没有标签的图层：

- [ ] AI 识别返回 `ComponentType`
- [ ] 置信度在 0-1 范围内
- [ ] source = 'ai'
- [ ] 低置信度图层标记 `needsReview: true`

#### 4.5 资产导出验证

- [ ] `assets` 目录生成图片文件
- [ ] 文件命名格式：`<图层名>_<图层ID>.png`
- [ ] 特殊字符被正确替换为下划线

#### 4.6 错误处理验证

测试异常情况：

```bash
# 测试不存在的文件
node dist/cli/index.js parse ./nonexistent.psd
# 预期：友好的错误消息

# 测试损坏的 PSD
node dist/cli/index.js parse ./corrupted.psd
# 预期：捕获错误，不崩溃

# 测试没有 API key 时的 AI 识别
unset CLAUDE_API_KEY
node dist/cli/index.js parse ./test.psd
# 预期：警告消息，降级到标签识别
```

#### 4.7 日志验证

启用 debug 模式，验证日志输出：

```bash
node dist/cli/index.js parse ./test.psd -d
```

检查日志是否包含：

- [ ] `[INFO]` 解析开始/完成消息
- [ ] `[DEBUG]` 标签识别尝试
- [ ] `[INFO]` AI 识别完成（如果使用）
- [ ] `[WARN]` 无法识别的图层
- [ ] 结构化日志格式：`[timestamp] [LEVEL] message {context}`

## 示例输出验证

### 预期的 JSON 输出示例

```json
{
  "metadata": {
    "psdPath": "/path/to/test-ui.psd",
    "canvasSize": {
      "x": 0,
      "y": 0,
      "width": 1920,
      "height": 1080
    },
    "timestamp": "2026-06-24T10:00:00.000Z",
    "exportedBy": "psd-exporter v1.0.0"
  },
  "layers": [
    {
      "id": "root_0",
      "name": "close.bt",
      "type": "group",
      "bounds": {
        "x": 100,
        "y": 50,
        "width": 80,
        "height": 80
      },
      "visible": true,
      "opacity": 1.0,
      "component": {
        "type": "Button",
        "confidence": 1.0,
        "source": "tag",
        "needsReview": false
      }
    },
    {
      "id": "root_1",
      "name": "title.txt",
      "type": "text",
      "bounds": {
        "x": 760,
        "y": 100,
        "width": 400,
        "height": 60
      },
      "visible": true,
      "opacity": 1.0,
      "component": {
        "type": "Text",
        "confidence": 1.0,
        "source": "tag",
        "needsReview": false
      }
    }
  ]
}
```

## 性能基准

Phase 1 MVP 预期性能：

- **小型 PSD** (< 50 图层): < 5 秒
- **中型 PSD** (50-200 图层): 10-30 秒
- **大型 PSD** (> 200 图层): 30-60 秒

性能主要受以下因素影响：
- 图层数量
- AI 识别调用次数（取决于未标记图层数）
- 图片导出大小和数量

## 故障排查

### 问题 1: "Module not found" 错误

```bash
# 确保已构建项目
npm run build
```

### 问题 2: AI 识别失败

```bash
# 检查 API key 是否正确设置
echo $CLAUDE_API_KEY

# 或在配置文件中检查
cat psd-exporter.config.json
```

### 问题 3: 测试失败

```bash
# 重新安装依赖
rm -rf node_modules package-lock.json
npm install

# 运行测试
npm test
```

### 问题 4: PSD 解析失败

- 确保 PSD 文件格式正确（Photoshop 保存，非 AI 或其他格式）
- 检查文件路径是否正确
- 查看 debug 日志获取详细错误信息

## 验收通过标准

所有以下条件必须满足：

- ✅ 所有单元测试通过（148/148）
- ✅ 能够成功解析真实的 PSD 文件
- ✅ 标签识别准确率 100%（对于正确命名的图层）
- ✅ AI 识别准确率 > 70%（Phase 1 目标）
- ✅ JSON 输出格式符合 schema
- ✅ 错误处理优雅，不崩溃
- ✅ 日志输出清晰，可追踪
- ✅ CLI 界面友好，帮助信息完整

## 下一步

验收通过后，可以：

1. 在 Unity 项目中集成，读取生成的 JSON 配置
2. 开发 Unity Editor 工具，自动根据 JSON 创建 UGUI 组件
3. 规划 Phase 2 功能（CV 启发式、九宫格检测、布局识别）

## 联系方式

如有问题或需要支持，请：

1. 查看 README.md 文档
2. 运行测试套件验证环境
3. 提交 GitHub Issue
