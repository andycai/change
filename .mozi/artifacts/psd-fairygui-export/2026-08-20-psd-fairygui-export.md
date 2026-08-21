# PSDExporterProject 导出 FairyGUI 6 源工程实施计划

> 日期：2026-08-20  
> 计划模式：complex  
> 上游：本会话 `mozi-grill` Q1-Q39 全部按推荐项确认  
> 执行约束：用户明确要求继续使用当前工作区，不创建 worktree

## 目标

在 `PSDExporterProject` 中增加 FairyGUI 源工程导出能力，使同一份 PSD 解析结果可以按目标选择生成：

- 现有 Unity UGUI JSON、交互 HTML 与切图；
- 可被 FairyGUI Editor 6.x 直接打开、继续编辑和手工发布的源工程；
- 或两套产物同时生成。

FairyGUI 导出必须支持多 PSD 页面共存、确定性资源 ID、原生交互组件映射、受管资源精确更新、失败前不破坏现有工程，并为所有降级和推断生成可追踪报告。

## 当前基线与约束

### 已确认的代码事实

- CLI 当前只有 `parse` 命令，固定生成 JSON 和 HTML，入口是 `PSDExporterProject/src/cli/index.ts`。
- `PsdParser` 已产出图层树、文本内容、文本样式和可选 PNG 路径；`ComponentRecognizer` 已支持点号标签、旧标签别名和 AI 识别。
- `ag-psd` 的图层对象提供 Photoshop `layer.id`，可作为重排或插入兄弟图层时仍稳定的图层身份；无 `layer.id` 时才需要回退到稳定路径。
- `ag-psd` 使用 `useImageData: true` 时可同时为图片层和文本层提供 RGBA `imageData`，适合作为裁剪、复杂文本栅格化和报告缩略图的统一像素源；调用前仍必须执行 `initializeCanvas(createCanvas)`。
- 本机安装 FairyGUI Editor `6.0.5`；已验证的源工程格式为：
  - 根目录一个 `*.fairy`，内容使用 `<projectDescription type="Unity" version="3.0"/>`；
  - `assets/<Package>/package.xml` 登记组件和图片资源；
  - 组件使用独立 XML，图片放在包内子目录；
  - `settings/Common.json`、`settings/Adaptation.json`、`settings/Publish.json` 可构成基础工程设置。
- 本机旧仓库 `/Users/andy/Workspace/projects/unity/unity-psd-fgui/tools/psd-fgui` 已验证纯文件生成能被 FairyGUI Editor 6.x 打开，包含按钮、列表、九宫格、透明裁剪和预检示例；它是格式证据和测试思路来源，不直接复制为第二套解析器。
- FairyGUI Unity SDK 当前支持 Button 的 `up/down/over/selectedOver/disabled/selectedDisabled` 页面、Toggle 的 `Button mode="Check"`、Slider 的 `bar/bar_v/grip/title` 约定和组件 `overflow="scroll"`。

### 工作区保护

当前仓库存在多项未提交改动，覆盖 `PSDExporterProject`、Agent Notes 和 UIArtifacts。实施时必须遵守：

1. 不执行 `git reset`、`git checkout -- .`、`git clean` 或目录级删除。
2. 不回退、不重写与本功能无关的现有改动。
3. 每次补丁只修改计划文件地图中的目标文件；发现冲突时以当前工作区内容为基线重新整合。
4. 删除旧 FairyGUI 受管文件时只读取上一版 sidecar 清单并逐文件删除，禁止清空整个包目录。

## 范围

### 包含

- `parse --target ugui|fairygui|all`，默认保持 `ugui`。
- 创建或接入 FairyGUI 6.x 源工程。
- 一个工程内固定包、多 PSD 根页面共存。
- 原生图片、文本、Button、Toggle、Slider、ComboBox、Input、ScrollPane/GList、GGroup/流式布局、Loader、Graph、裁剪组件映射。
- `.comp`、`.s9-左-上-右-下` 新指令，以及现有 `ref/refp` 的实际消费。
- 隐藏状态层、透明像素裁剪、九宫格换算、复杂文本和复杂蒙版降级。
- 确定性包 ID、资源 ID、节点 ID、文件名和页面身份。
- `.psd-exporter/` 受管清单、事务状态与 JSON/Markdown 报告。
- 共享 `package.xml` 的保序语义合并和失败回滚。
- 单元、集成、黄金 PSD 和 FairyGUI Editor 6.0.5 人工验收。

### 不包含

- 自动调用 FairyGUI 发布器或生成 Unity 运行时发布资源。
- FairyGUI Editor 5.x 兼容。
- 受管页面 XML Diff-Merge；FairyGUI 中对受管页面做的手工修改会被下一次导出覆盖。
- 通用自定义 Controller、Transition 或业务数据生成。
- 一条命令处理多个 PSD；批量由外层重复调用单文件事务完成。
- Photoshop 插件、Unity Editor 窗口或新的桌面 GUI。
- 仅凭像素哈希自动跨页面去重；资源复用必须由 `ref/refp` 显式声明。

## 验收条件

### CLI 与兼容性

1. 不传 `--target` 时，现有 JSON、HTML 和 `-a` 切图行为保持不变。
2. `--target fairygui` 只生成 FairyGUI 源工程和报告，不要求 `-o/-a`。
3. `--target all` 对 PSD 只解析一次，同时生成现有产物和 FairyGUI 产物。
4. FairyGUI 高频 CLI 参数覆盖配置文件，缺少工程路径或发生命名冲突时给出明确错误和非零退出码。

### 工程与资源

5. 目标目录不存在 FairyGUI 工程时创建可被 Editor 6.0.5 打开的最小工程；存在唯一 `.fairy` 时接入；存在多个 `.fairy` 时拒绝猜测。
6. 新工程首次导入 PSD 时写入设计分辨率，后续页面尺寸不修改工程级分辨率。
7. 同一 PSD 重复导出后，包 ID、根组件 ID、子组件 ID、图片 ID、显示节点 ID 和 `ui://` URL 保持一致。
8. 在同一包导入两个 PSD 后，两页和各自资源同时存在；重导其中一页不修改另一页的文件、ID 或清单。
9. 包中已有人工资源和未知属性时，合并后语义和相对顺序保留；未显式接管的同名组件不被覆盖。
10. 导出失败或预检出现阻断错误时，提交点之前的 `package.xml`、受管清单和已引用资源保持不变。

### 视觉与组件结构

11. 图层堆叠顺序与 PSD 一致；普通图层保持绝对坐标，透明裁剪后补偿位置。
12. 普通组以 FairyGUI 内联 GGroup 表达；`.comp` 和交互控件生成独立资源组件。
13. 原生文本保留内容、字号、颜色、粗斜体、对齐、描边和可表达的基础阴影；复杂效果使用原始 RGBA 栅格化。
14. Button、Toggle、Slider、Dropdown、InputField、ScrollView、布局组均覆盖成功映射和降级场景。
15. `.sliced` 与 `.s9-*` 生成合法 `scale9grid`；裁剪后无法合法换算时保留未裁剪图片并报告。
16. 普通隐藏图层不输出；被状态、模板或结构角色引用的隐藏图层仍参与组件生成。

### 报告与验证

17. 每次导出生成 JSON 和 Markdown 报告，包含映射、AI 建议、字体回退、文本栅格化、九宫格推断、降级、未解析引用和受管文件。
18. 自动测试验证 XML 可解析、ID 唯一、引用闭合、文件存在、事务回滚、重复导出稳定和其他页面不受影响。
19. 黄金 PSD 在 FairyGUI Editor 6.0.5 中能打开根页面，原生组件结构可编辑，视觉层级与 PSD 对照合理。

## 关键方案比较

### 方案 A：直接移植旧 `psd-fgui` 工具

优点是已经验证 FairyGUI 基础 XML 可打开，按钮、列表和九宫格有现成实现。缺点是它使用另一套 `@tag` 语法、另一套 PSD 场景图、递增不稳定 ID、整包删除策略和全量文字栅格化；直接移植会形成第二套解析器，并与当前点号标签、文本样式、AI 识别和 HTML 导出持续分叉。

### 方案 B：在当前 JSON 后再转换为 FairyGUI

优点是不用改解析器接口。缺点是 JSON 有意不包含像素数据、Photoshop 图层 ID、蒙版数据和完整标签意图，无法可靠完成透明裁剪、复杂文本降级、稳定资源身份和九宫格换算；生成器还会被已经扁平化的数据限制。

### 方案 C：复用当前解析器，增加运行时解析文档和独立 FairyGUI 后端

`PsdParser` 单次解析产生可序列化 `LayerTree` 与非序列化 RGBA 源表；现有 JSON/HTML 后端和新 FairyGUI 后端消费同一文档。FairyGUI 后端拥有独立场景模型、资源注册表、XML 写入器、事务管理器和报告器。

**推荐方案：C。** 它复用当前已验证的标签、文本和图层模型，不让 FairyGUI 格式污染 JSON Schema，同时保留旧工具中已经证明有效的 XML 结构和测试方法。

## 架构决策

### 1. 单次 PSD 解析，双层结果模型

新增运行时 `ParsedPsdDocument`：

```ts
interface ParsedPsdDocument {
  tree: LayerTree;
  rasterSources: Map<string, RasterSource>;
}

interface RasterSource {
  layerId: string;
  photoshopLayerId?: number;
  width: number;
  height: number;
  rgba: Uint8ClampedArray;
  mask?: RasterMaskSource;
}
```

- `LayerTree` 继续只包含可写入 JSON 的数据。
- `Layer` 增加可序列化的 `sourceId?: number`，保存 Photoshop 图层 ID，但不放 RGBA 字节。
- `PsdParser.parseDocument()` 使用 `readPsd(..., { useImageData: true })` 构建两层结果。
- 现有 `parse(psdPath, assetsDir?)` 保留为兼容包装，内部调用 `parseDocument()` 并按需导出现有切图。
- CLI 的 `all` 路径只调用一次 `parseDocument()`，避免重复解码大型 PSD。

### 2. 显式标签决定 FairyGUI 结构

新建 `LayerIntent`，合并原始 `TagParseResult`、标签来源组件信息和 FairyGUI 指令：

```ts
interface LayerIntent {
  displayName: string;
  component: ComponentInfo;
  explicitlyTagged: boolean;
  prefix?: 'ref' | 'refp';
  reusableComponent: boolean;
  scale9?: ExplicitScale9;
}
```

- `TagParser` 增加 `.comp` 和动态 `.s9-L-T-R-B` 解析，但 `ComponentRecognizer` 不把 `.comp` 当作 UGUI 组件类型。
- 只有 `component.source === 'tag'` 且存在明确 main/role/directive 时允许塑造 FairyGUI 交互结构。
- AI 结果只进入诊断报告；无标签图层仍按物理类型导出为图片、文本或普通组。
- `ref/refp` 和 `.comp/.s9-*` 必须保留在 intent 中，不能只存进 `ComponentInfo`。

### 3. FairyGUI 中间场景与 XML 解耦

FairyGUI 后端先生成格式无关的 `FairyScene`，再写 XML：

```ts
interface FairyScene {
  project: FairyProjectIdentity;
  page: FairyPageIdentity;
  components: FairyComponentResource[];
  images: FairyImageResource[];
  diagnostics: Diagnostic[];
}
```

场景构建阶段负责：

- PSD 图层顺序直接保持写入 FairyGUI `displayList`；FairyGUI 中后写节点覆盖先写节点，因此背景必须先写、前景后写；
- 组包围盒、局部坐标和普通 GGroup membership；
- 原生组件角色校验与降级；
- 文本原生映射或栅格化决策；
- 图片裁剪、九宫格、纯色和蒙版策略；
- 资源引用和稳定 ID 注册。

XML 写入阶段只负责确定性序列化和转义，不承担组件推断。

### 4. 确定性身份与冲突处理

身份来源按优先级确定：

1. CLI `--fairygui-page-id`；
2. `fairyGui.pages[相对源路径].pageId`；
3. `sourceRoot` 下规范化相对路径的 SHA-256 短标识；
4. 未配置 `sourceRoot` 时使用绝对路径并报告可移植性 warning。

资源键使用：

```text
<pageId>|<resourceKind>|<photoshopLayerId 或稳定祖先路径>|<role>
```

- 资源 ID 和显示节点 ID 使用 SHA-256 转 base36 的固定长度字符串。
- 新包 ID 同样确定性生成；已有包永远保留 `package.xml` 中的 ID。
- 发生哈希碰撞时使用可复现 salt 重算并写入报告，禁止改用遍历序号。
- 显示名称用于编辑器可读性；重名时追加稳定短哈希，不追加随顺序变化的 `_1/_2`。
- Photoshop `layer.id` 缺失时才使用祖先名称、同名稳定序号和几何摘要组成回退路径，并明确报告稳定性降低。

### 5. 页面版本目录与 `package.xml` 作为提交点

每次页面导出写入新的内容版本目录：

```text
assets/<Package>/psd/<page-id>/<generation>/components/
assets/<Package>/psd/<page-id>/<generation>/images/
```

- `generation` 由场景和资源内容摘要确定。
- 新版本文件全部写完并验证前，不修改当前 `package.xml`。
- `package.xml` 只在所有新文件存在且引用闭合后，通过同目录临时文件 + rename 替换，作为页面版本切换的提交点。
- 提交成功后再写新 manifest，并逐文件清理上一 manifest 中已失效的版本文件。
- 中途失败时新版本目录仍未被包引用，可安全按清单逐文件清理；旧页面继续有效。
- 如果进程在提交点后、manifest 更新前异常退出，`.psd-exporter/transactions/` 的事务记录用于下次启动恢复或完成清理。

该布局避免在提交前覆盖仍被旧 `package.xml` 引用的同名图片，满足“失败时旧工程可继续使用”的目标。

### 6. 保序合并共享 `package.xml`

增加 `fast-xml-parser` 作为唯一 XML 依赖，使用 preserve-order 模式读取已有 `package.xml`：

- 保留根属性、未知资源类型、注释和人工资源顺序；
- 只移除上一 manifest 记录的受管 resource ID；
- 在原资源序列中插入本页新资源；
- 保留或补齐 `<publish><atlas name="Default" index="0"/></publish>`；
- 写回后再次解析，并执行资源 ID 唯一和引用闭合校验。

组件 XML 通过 `component-xml-writer.ts` 的统一 Builder 生成并集中转义；`scene-builder.ts` 提供 `LayerIntent`/`FairyScene` 快照，将报告映射与 XML 序列化边界分离。

### 7. 受管元数据

工程根目录新增：

```text
.psd-exporter/
├── pages/<page-id>.json
├── reports/<page-id>.json
├── reports/<page-id>.md
├── transactions/<transaction-id>.json
└── staging/<transaction-id>/...
```

页面 manifest 至少记录：

- schema 版本；
- 源路径键、pageId、包名和包 ID；
- 根组件名和资源 ID；
- generation；
- 所有受管资源、目标文件、内容 hash 和引用关系；
- 上次成功导出时间与生成器版本。

manifest 和报告纳入版本控制；`staging/`、运行中的 transactions 和 lock 文件加入项目级忽略模板或导出时清理，不污染 FairyGUI `assets/` 资源树。

## CLI 与配置合同

### CLI

扩展现有 `parse`：

```text
--target <ugui|fairygui|all>          默认 ugui
--fairygui-project <dir>              FairyGUI 工程目录
--fairygui-package <name>             默认 PSDImport
--fairygui-component <name>           默认净化后的 PSD 文件名
--fairygui-page-id <id>               显式稳定页面身份
--fairygui-source-root <dir>           计算相对源路径的根目录
--fairygui-adopt-existing              显式接管同名人工组件
```

解析后先构造统一 `ExportRequest`，再由编排服务决定调用现有后端和 FairyGUI 后端，避免 CLI 继续膨胀。

### 配置文件

现有 `psd-exporter.config.json` 增加：

```json
{
  "fairyGui": {
    "projectPath": "../UIProject",
    "packageName": "PSDImport",
    "sourceRoot": "../UIArtifacts/psd",
    "defaultScale9": {
      "unit": "ratio",
      "left": 0.3,
      "top": 0.3,
      "right": 0.3,
      "bottom": 0.3
    },
    "fontMappings": {
      "PingFang SC": {
        "default": "PingFang SC",
        "tmp": "ui://packageIdfontId",
        "ugui": "PingFang SC"
      }
    },
    "references": {
      "local": {
        "ButtonBlue": { "resourceId": "abcd1234" }
      },
      "external": {
        "CommonClose": { "packageId": "common01", "resourceId": "close001" }
      }
    },
    "pages": {
      "menu/main.psd": {
        "pageId": "main-menu",
        "componentName": "MainMenu"
      }
    }
  }
}
```

优先级：默认值 < 配置文件 < 页面配置 < CLI。

配置校验保持当前“无效字段回退并报告”的兼容行为，但 FairyGUI 导出所需的工程路径、比例范围、引用 ID 和 pageId 冲突属于阻断错误。

## 组件映射合同

### 普通组和 `.comp`

- 普通 PSD 组展开为同一组件 `displayList` 中的子节点和一个 GGroup 节点；子节点通过 `group="<id>"` 关联，保留编辑器分组和绝对坐标。
- `.comp`、Button、Toggle、Slider、Dropdown 和需要独立滚动容器的组生成单独组件 XML，父级使用 `<component src="..."/>`。
- 普通组自身 bounds 为零时使用可参与导出的子节点包围盒。

### 图片

- `Image/simple`：`<image>` + 普通 image resource。
- `sliced`：resource 写 `scale="9grid" scale9grid="x,y,w,h"`。
- `tiled`：resource 写 `scale="tile"`。
- `filled`：没有显式方向信息时使用水平填充默认值并报告推断；Slider 的 fill 由 Slider 规则接管。
- `RawImage`：生成 `<loader url="ui://...">`，使运行时可替换 URL。
- `FillColor`：对非透明像素执行颜色一致性检测；一致时生成 `<graph type="rect" fillColor="...">`，否则降级为图片。
- 所有图片默认裁剪透明边缘并补偿节点坐标；完全透明图片报告并跳过或生成透明 Graph，不能写零尺寸纹理。

### 九宫格

- `.s9-L-T-R-B` 使用原图像素边距。
- `.sliced` 未带显式边距时使用配置比例，默认四边 30%。
- 裁剪时将边距换算到裁剪后坐标；中心区域宽高必须大于零且落在纹理内。
- 换算非法时禁用该图片裁剪，使用原纹理和原始九宫格，并记录 warning。

### 文本

- 基础属性映射到 `<text>`：text、font、fontSize、color、bold、italic、align、vAlign、autoSize、singleLine。
- 可表达效果：stroke → `strokeColor/strokeSize`；基础 dropShadow → `shadowColor/shadowOffset`。
- innerShadow、gradient、outerGlow、bevel，或无法无损表达的多重效果，使用文本层 RGBA 栅格化为 image resource。
- 字体映射按 PSD 字体名和 `.tmp/.ugui` 后端选择；未命中时保留 PSD 字体名并报告。
- 所有 XML 文本、名称和 prompt 必须转义换行和 XML 特殊字符。

### Button

- 组件：`extention="Button"`，固定 controller 名 `button`。
- 页面至少包含 `up/down/over/selectedOver`；存在 `.disable` 时增加 `disabled/selectedDisabled`。
- 角色映射：`bg→up`、`press→down`、`onover→over`、`select→selectedOver`、`disable→disabled/selectedDisabled`、`bttxt→title`。
- 未提供任何状态角色时，所有普通子节点绑定 `up`，其余页面为空但组件仍可用。

### Toggle

- 使用 `extention="Button"` + `<Button mode="Check"/>`。
- `.tglb` 生成 `title`；`.mark` 绑定 controller 的 `down/selectedOver` 页面；背景保留在未选中和选中页面。
- 首版不自动创建互斥 Radio 组。

### Slider

- 使用 `extention="Slider"`。
- 水平时 `.fill` 节点命名为 `bar`，垂直时命名为 `bar_v`；`.handle` 必须生成名为 `grip` 的可交互子组件。
- 方向根据 fill/handle 与容器长宽关系推断；置信不足时默认水平并报告。
- 缺少 fill 或 handle 时降级为普通组件，不能生成运行时不可操作的 Slider。

### Dropdown

- 使用 `extention="ComboBox"`；`.dpdlb→title`，`.dpdicon` 保留为箭头外观。
- `.template` 生成 popup 组件；`.item` 生成独立 item 组件；popup 内 list 的 `defaultItem` 指向 item URL。
- ComboBox 初始 items 为空，不从 PSD 文本猜测业务选项。
- 缺少 template 或 item 时降级为 Button/普通组件并报告，不生成悬空 dropdown URL。

### InputField

- `.ipttxt` 生成 `input="true"` 的 `<text>`；缺少时按输入框 bounds 创建空 input text。
- `.placeholder` 写入 input 节点 `prompt`，不猜测密码、数字或多行语义。
- 背景和装饰按普通节点保留。

### ScrollView 和列表

- 存在 `.item` 时生成 GList，item 作为独立组件并设置 `defaultItem`。
- 不存在 `.item` 时生成独立组件并设置 `overflow="scroll"`，保留自由坐标内容。
- `.vpt` 决定视口 bounds，`.content` 决定内容 bounds；缺失时使用容器和子节点包围盒。
- `.hbar/.vbar` 及背景优先生成 FairyGUI ScrollBar 组件；角色不完整时保留视觉节点并使用默认/隐藏滚动条配置，报告降级。

### 布局组

- `.hbox` 和 `.vbox` 使用 GGroup 的 Horizontal/Vertical layout，间距由相邻子节点推导。
- 子节点间距不一致时保留绝对位置，GGroup layout 设为 None 并报告。
- FairyGUI GGroup 不支持 Grid；`.grid` 仅在子项同构且行列间距可稳定推导时生成非滚动 GList `layout="flow_hz"`，否则保留普通组绝对位置并报告。

### Mask

- 显式 `.msk` 容器优先生成带 `mask="<node-id>"` 或 `overflow="hidden"` 的组件。
- PSD raster mask 可在 RGBA 阶段合并 alpha 后输出。
- 复杂 vector mask、混合模式或无法表达的剪贴链降级为栅格图片并报告，不静默丢失视觉。

### `ref/refp`

- `ref <name>` 先在本次页面和现有包中做精确逻辑名匹配，再查 `references.local`；匹配必须唯一。
- `refp <alias>` 只查 `references.external`，配置必须给出 packageId 和 resourceId。
- 禁止模糊名称搜索和自动像素去重。
- 未解析引用产生 warning，并降级为本地图像或组件；如果源层没有可本地导出的像素/子树，则升级为阻断错误。

## 诊断级别

### 阻断错误

- FairyGUI 工程发现多个 `.fairy` 文件。
- 目标包 ID、页面 ID 或资源 ID 与非受管资源冲突。
- 未授权接管同名人工根组件。
- 组件 XML、package.xml 无法解析或存在重复 ID、悬空 `src/defaultItem/dropdown/url`。
- 必须引用的本地/跨包资源既无法解析也无法降级。
- 临时文件、提交点或回滚写入失败。

### Warning 后继续

- 字体映射缺失。
- AI 识别建议与显式标签不一致。
- 文本、蒙版、布局、滚动条或控件结构发生视觉降级。
- 九宫格使用默认 30% 或因裁剪换算失败而禁用裁剪。
- Slider 方向置信不足。
- sourceRoot 缺失导致页面身份依赖绝对路径。
- 旧版本目录清理失败但当前引用闭合。

## 文件地图

| 路径 | 操作 | 职责 |
|---|---|---|
| `PSDExporterProject/src/parser/layer-tree.ts` | 修改 | 增加 `sourceId`，保持 JSON 友好的图层模型 |
| `PSDExporterProject/src/parser/psd-document.ts` | 新增 | 定义 `ParsedPsdDocument`、RGBA 与 mask 源 |
| `PSDExporterProject/src/parser/psd-parser.ts` | 修改 | 单次 `useImageData` 解析、构建 tree + rasterSources |
| `PSDExporterProject/src/parser/asset-exporter.ts` | 修改 | 从统一 RGBA 源输出现有切图，继续兼容 HTML/UGUI |
| `PSDExporterProject/src/recognizer/tag-parse-result.ts` | 修改 | 增加 FairyGUI 指令结果 |
| `PSDExporterProject/src/recognizer/tag-parser.ts` | 修改 | 解析 `.comp`、`.s9-*`，保留 `ref/refp` |
| `PSDExporterProject/config/tag-config.json` | 修改 | 登记静态 `.comp` 指令或结构 family（动态 s9 由解析器处理） |
| `PSDExporterProject/src/config/config-loader.ts` | 修改 | 增加 `fairyGui` 配置、页面覆盖和引用校验 |
| `PSDExporterProject/src/export/export-service.ts` | 新增 | 统一 `ugui/fairygui/all` 编排，保证单次解析 |
| `PSDExporterProject/src/fairygui/model.ts` | 新增 | FairyScene、资源、页面、诊断和 manifest 类型 |
| `PSDExporterProject/src/fairygui/identity.ts` | 新增 | page/package/resource/node ID 与稳定命名 |
| `PSDExporterProject/src/fairygui/layer-intent.ts` | 新增 | 合并标签、物理图层类型和 AI 报告信息 |
| `PSDExporterProject/src/fairygui/raster-processor.ts` | 新增 | trim、mask alpha、纯色检测、九宫格换算和 PNG 编码 |
| `PSDExporterProject/src/fairygui/text-mapper.ts` | 新增 | 原生文本属性、字体映射和栅格降级决策 |
| `PSDExporterProject/src/fairygui/scene-builder.ts` | 新增 | 图层层级、组件角色、局部坐标和资源注册 |
| `PSDExporterProject/src/fairygui/xml-codec.ts` | 新增 | preserve-order package 合并、组件 XML AST 与转义 |
| `PSDExporterProject/src/fairygui/validator.ts` | 新增 | XML、ID、文件和引用闭合预检 |
| `PSDExporterProject/src/fairygui/project-manager.ts` | 新增 | 工程创建、包发现、manifest、stage、提交和恢复 |
| `PSDExporterProject/src/fairygui/report-writer.ts` | 新增 | JSON 与 Markdown 报告 |
| `PSDExporterProject/src/fairygui/fairygui-exporter.ts` | 新增 | FairyGUI 后端总入口 |
| `PSDExporterProject/src/cli/index.ts` | 修改 | 新 CLI 参数并委托 export service |
| `PSDExporterProject/package.json` | 修改 | 增加 XML 依赖和黄金 fixture 生成脚本 |
| `PSDExporterProject/package-lock.json` | 修改 | 锁定 XML 依赖 |
| `PSDExporterProject/README.md` | 修改 | 文档化 target、配置、标签、目录和安全语义 |
| `PSDExporterProject/ACCEPTANCE_TEST.md` | 修改 | 增加 FairyGUI 自动与人工验收流程 |
| `PSDExporterProject/tests/fairygui/**` | 新增 | 各深模块单元测试 |
| `PSDExporterProject/tests/integration/fairygui-export.test.ts` | 新增 | 多页面、重导、事务和真实 PSD 集成测试 |
| `PSDExporterProject/tests/fixtures/fairygui-golden.psd` | 新增 | 覆盖全部原生组件和降级场景的黄金 PSD |
| `PSDExporterProject/tests/fixtures/fairygui-project/**` | 新增 | 含人工包资源的已有工程测试 fixture |
| `.mozi/notes/proposed/feature/2026-08-20-psd-fairygui-source-export.md` | 新增/后续迁移 | 记录长期功能决策与替代方案 |

## 实施步骤

### 切片 0：建立基线并保护当前工作区

1. 记录 `git status --short` 和 `git diff -- PSDExporterProject`，确认哪些改动是用户当前未提交工作。
2. 在 `PSDExporterProject` 运行当前 TypeScript build 和 Jest 全量测试，保存基线；若失败，只记录与本功能无关的既有失败，不在本变更中修复。
3. 不运行当前已知缺少 flat config 的 ESLint，除非本任务单独补齐 ESLint 配置得到授权。

**完成证据：** 基线结果可追溯，且没有任何现有改动被回退。

### 切片 1：单次解析文档与稳定源身份

1. 添加 `psd-document.ts` 和 `Layer.sourceId`。
2. 让 `PsdParser.parseDocument()` 使用 `useImageData`，收集 layer/mask RGBA 和 Photoshop layer ID。
3. 保留 `parse()` 兼容包装；重写 `AssetExporter` 从 RasterSource 导出 PNG。
4. 扩展 parser/asset tests，覆盖图片层、文本层、mask、sourceId 和旧 `assetPath` 行为。

**完成证据：** UGUI/HTML 仍可使用旧接口；同一 PSD 只解析一次即可同时获得 tree 与像素源。

### 切片 2：标签、配置和导出请求合同

1. 扩展 TagParser 支持 `.comp` 与严格 `.s9-L-T-R-B`；非法 s9 产生可定位诊断。
2. 增加 FairyGUI 配置类型、默认值、页面覆盖、字体和引用映射校验。
3. 定义 `ExportRequest` 和 target 枚举，先不接入完整 FairyGUI 实现。
4. 更新 CLI 参数解析测试，锁定默认 `ugui` 和覆盖优先级。

**完成证据：** 新参数不改变旧命令默认行为；配置错误区分回退 warning 和阻断 error。

### 切片 3：稳定 ID、命名和受管 manifest

1. 实现 page/package/resource/node ID 算法和碰撞检测。
2. 实现 FairyGUI 文件名净化、保留扩展名、Windows 保留名和稳定短后缀。
3. 定义 manifest schema、版本和 sourceRoot-relative 页面匹配。
4. 测试图层插入、兄弟重排、同名资源、PSD 移动 + 固定 pageId 等场景。

**完成证据：** 重复构建和无关图层变化不会改变已有资源 ID；冲突不会静默覆盖。

### 切片 4：像素和文本深模块

1. 实现 alpha bbox、PNG 输出、坐标补偿、完全透明检测和 raster mask 合并。
2. 实现纯色检测、tile/filled metadata 和九宫格裁剪换算。
3. 实现原生文本属性映射、字体后端选择和复杂效果栅格降级。
4. 对 pixel buffer、裁剪边缘、九宫格非法中心、XML 字符、中文字体和复杂文本效果添加单测。

**完成证据：** 像素模块不依赖 FairyGUI XML；文本 mapper 能明确返回 native 或 rasterized 决策和原因。

### 切片 5：工程、包合并和事务提交

1. 添加 `fast-xml-parser`，实现 FairyGUI project/package 发现和最小工程模板。
2. 实现 preserve-order `package.xml` 读取、受管资源替换和未知资源保留。
3. 实现版本目录 stage、package 原子替换、manifest、事务日志、恢复和旧文件逐项清理。
4. 通过可注入 FileOps 故障点测试“提交前失败”“package 替换失败”“提交后清理失败”。

**完成证据：** 失败注入后旧 package hash 和旧受管引用保持有效；人工资源不丢失。

### 切片 6：基础场景与静态视觉映射

1. 实现 LayerIntent、普通 GGroup、`.comp`、局部坐标和绘制顺序。
2. 实现 Image、Loader、Graph、native Text、raster Text 和显式 Mask。
3. 生成组件 XML AST 和 package resource registry。
4. 增加引用闭合 validator，禁止写出零尺寸资源和重复节点 ID。

**完成证据：** 最小 PSD 和普通复杂页面可以生成 FairyGUI 工程并通过自动预检。

### 切片 7：原生交互组件映射

按依赖由简单到复杂实现并分别测试：

1. Button + 状态 controller + disabled 页面。
2. Toggle Check + mark/title。
3. InputField + prompt。
4. Slider + grip/bar/bar_v + 方向推断和降级。
5. Dropdown + popup + item + 空业务数据。
6. ScrollView/GList + content/viewport/scrollbar 降级。
7. hbox/vbox GGroup 与 grid flow list 条件映射。

**完成证据：** 每种组件均有成功结构测试、缺失角色降级测试和 XML 可解析测试。

### 切片 8：引用、AI 审查和报告

1. 实现 `ref` 的精确包内解析和本地降级。
2. 实现 `refp` 配置解析，验证目标 package/resource ID 格式。
3. 为 AI 识别提供按层 RasterSource 临时 PNG resolver；结果只进入报告，不传入结构 mapper。
4. 输出 JSON/Markdown 报告和诊断统计。

**完成证据：** AI 返回 Button 但图层无显式标签时，FairyGUI 仍是普通资源，报告记录建议；未解析 ref 有明确降级或阻断结论。

### 切片 9：CLI 编排与旧功能回归

1. 把当前 CLI action 抽到 `ExportService`。
2. 接入 `ugui/fairygui/all`，保证 all 复用同一 ParsedPsdDocument。
3. 保持现有 output path、HTML 命名和 assets path 规则。
4. 更新 CLI mock 测试，增加目标矩阵和失败退出码。

**完成证据：** 旧命令产物与路径不变；fairygui-only 不生成 JSON/HTML；all 同时成功。

### 切片 10：黄金 PSD、多页面与安全验收

1. 使用仓库既有确定性 `UIArtifacts/psd/demo.psd` 作为真实黄金输入，覆盖原生控件、复杂文本、九宫格、ref/refp 和降级场景；隐藏状态等合成边界使用程序化 fixture 覆盖。
2. 端到端导出到临时新工程，验证全部 XML、文件和报告。
3. 导入第二 PSD，再重导第一 PSD，比较另一页文件 hash 和 package 资源。
4. 在含人工资源的 fixture 工程测试拒绝覆盖与显式 adopt。
5. 对同一 PSD 连续导出两次，比较 manifest、resource ID 和 `ui://` URL。

**完成证据：** 自动验收条件 1-18 全部覆盖。

### 切片 11：文档、Agent Note 和人工 Editor 验收

1. 更新 README、配置示例、标签表、目录结构、报告说明和安全边界。
2. 更新 `ACCEPTANCE_TEST.md`，记录 FairyGUI Editor 6.0.5 打开步骤和检查表。
3. 在 Editor 中打开黄金工程，确认包和根页面可解析；通过组件 XML 与 Editor workspace 元数据确认 Button/Toggle/Slider/ComboBox/Input/GList 结构和图层顺序。截图级视觉对照受当前显示权限限制，保留为人工补充证据。
4. 将 proposed Agent Note 迁移到 implemented，改写为实际交付事实并运行验证器。

**完成证据：** build、Jest、真实 PSD 导出、Editor 人工检查和 Agent Note 验证均通过。

## 验证命令

在 `PSDExporterProject` 目录执行 npm 命令：

```bash
npm run build
npm test -- --runInBand
```

针对本功能优先运行：

```bash
npm test -- --runInBand tests/fairygui
npm test -- --runInBand tests/integration/fairygui-export.test.ts
```

真实导出示例：

```bash
node dist/cli/index.js parse ../UIArtifacts/psd/demo.psd \
  --target fairygui \
  --fairygui-project ../UIArtifacts/fairygui-demo \
  --fairygui-package PSDImport \
  --fairygui-page-id demo
```

Agent Note 验证：

```bash
python3 .agents/skills/mozi-agent-notes/scripts/validate_agent_notes.py --project-root .
```

## 风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| FairyGUI XML 是事实格式而非公开稳定 schema | Editor 可能拒绝某些组合 | 以 6.0.5 样本和 Unity SDK 运行时命名约定为基准；每类组件都加 Editor 人工验收 |
| 多文件无法获得真正的文件系统级原子事务 | 进程崩溃可能留下未引用文件或 pending 事务 | 使用版本目录和 `package.xml` 单提交点；事务 sidecar 支持下次恢复；未引用文件可安全清理 |
| Photoshop 图层 ID 在复制粘贴或另存时变化 | 资源 ID 可能变化 | manifest 记录 sourceId；支持固定 pageId；无 sourceId 时使用稳定路径回退并报告 |
| 普通 GGroup 不是严格的嵌套容器 | 某些局部坐标/裁剪语义不能直接表达 | 普通组用 membership 保持编辑分组；需要局部容器语义的 `.comp`、Mask、ScrollView 生成独立组件 |
| 复杂 PSD 文本效果与 FairyGUI 字段不对等 | 视觉失真 | 明确 native/raster 决策矩阵，使用文本层 RGBA 作为保真降级 |
| 蒙版、剪贴链和混合模式不完整 | 视觉差异 | 能合并 raster mask 时合并；复杂场景栅格化并报告；不宣称像素级一致 |
| package.xml 重写影响人工格式 | 无语义变化但 diff 变大 | 使用 preserve-order 模式，只替换受管资源节点；测试未知属性、注释和资源顺序 |
| 当前工作区已有未提交改动 | 补丁可能覆盖用户工作 | 不创建 worktree但严格按当前内容整合；每切片前查看目标文件 diff；禁止 reset/clean |
| AI 调用耗时和不稳定 | 导出变慢或报告缺失 | AI 仅报告、可禁用；失败返回 warning，不阻断 FairyGUI 结构生成 |
| 大型 PSD 同时保留 RGBA | 峰值内存上升 | 单次解析、按资源写出后释放 buffer；后续可引入按需解码，但首版不重复解析 PSD |

## 回滚策略

### 代码回滚

- `--target` 默认值始终为 `ugui`，FairyGUI 后端可通过不传目标完全旁路。
- 新后端集中在 `src/fairygui/` 和 `ExportService`，若需撤回可删除该后端并移除 CLI 参数，不必改变 JSON Schema 或 Unity C# 消费方。
- `parse()` 兼容包装必须保留，避免回滚 FairyGUI 时破坏既有调用。

### 工程数据回滚

- 每页 manifest 记录上一 generation；提交后如发现问题，可将 `package.xml` 中该页资源条目恢复到上一 generation。
- 旧 generation 只在新提交成功并写入 manifest 后清理；清理失败不影响运行，可手工按 manifest 恢复。
- 未显式 adopt 的人工资源永不进入受管清单，因此不会被回滚或清理逻辑触碰。

## 当前状态与下一动作

计划中的核心导出、安全、组件、报告、测试、文档和 Editor 打开验收已经实施。真实 `UIArtifacts/psd/demo.psd` 已重生成到 `UIArtifacts/psd/demo-fairygui-20260820`；串行构建和全量测试通过（26 个测试套件、310 个测试），事务恢复以 package 内容哈希确认提交点并拒绝 sidecar 路径逃逸，旧 manifest 的包归属、根组件、资源路径、generation 文件集合和内容 hash 在取得清理权前全部验证，generation 签入当前页面实际引用与最终输出摘要。manifest 的 40 个受管文件、全部 XML、`package.xml` 与本地引用均通过闭合校验，背景位于 Dropdown 之前，报告包含 51 条图层映射、12 条 AI 建议和 23 条诊断。报告写入失败会返回已提交状态而非误报整个导出失败。剩余人工动作仅是在具有屏幕捕获权限的交互会话补充截图级视觉对照证据，然后进入提交阶段。
