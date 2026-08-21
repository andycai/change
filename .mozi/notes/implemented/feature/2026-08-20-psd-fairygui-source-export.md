# Agent Note: 从 PSD 生成受管的 FairyGUI 6 源工程

Status: implemented

## 问题

`PSDExporterProject` 原先只生成 Unity UGUI JSON、图层切图和交互 HTML。设计师仍需在 FairyGUI Editor 中重新创建工程、包、组件状态、滚动结构和资源引用，PSD 中已经明确的图层层级、角色标签与文本样式没有进入可继续编辑的 FairyGUI 源工程。

## 决策

`PSDExporterProject` 现在在一次 PSD 解析结果上提供独立 FairyGUI 6.x 导出后端。CLI 的 `parse` 命令支持 `--target ugui|fairygui|all`，默认仍为 `ugui`；`all` 复用同一份 `ParsedPsdDocument`，同时生成旧 UGUI/HTML 产物和 FairyGUI 工程。

FairyGUI 后端位于 `PSDExporterProject/src/fairygui/`，在目标目录创建或接入一个 `.fairy` 工程，并在固定包中为每个 PSD 页面生成受管根组件、组件 XML 和 PNG 资源。`scene-builder.ts` 建立 `LayerIntent` 与 `FairyScene` 快照，`component-xml-writer.ts` 统一生成和转义组件 XML。页面、组件、节点、图片、generation 和文件名由 pageId、源路径、Photoshop 图层 sourceId 及生成器 revision 确定性生成；generation 额外签入当前页面实际消费的本地引用解析结果和目标 XML 内容 hash，不受其他页面或无关人工资源变化影响。缺少 sourceId 时使用祖先名称与几何路径，截断哈希碰撞使用确定性 salt，并在短 ID 空间耗尽时确定性遍历剩余候选后给出明确错误。manifest、报告、事务和锁位于工程 `.psd-exporter/`。

导出通过 staging generation 和 `package.xml` 原子替换提交。恢复以 `package.xml` 内容哈希而非可篡改状态字段判断提交点；sidecar 中的 package、staging 和受管文件路径必须留在工程及当前页面受管目录内。重导前校验旧 manifest 的包归属、根组件、资源 ID、`path + name`、受管文件集合和内容 hash，任何不一致都在修改 `package.xml` 前阻断；同 generation 的目标文件若已存在但内容不同也阻断覆盖。只清理通过完整性校验的上一份 manifest 明确列出的旧受管文件；人工资源、其他 PSD 页面、未知 XML 属性、注释和显式接管前的人工文件不因导出而被删除。同一 pageId 不能被不同 PSD 静默复用，历史相对 `sourcePath` 只有在当前显式 `sourceRoot` 下能精确解析时才迁移并写回绝对路径；若 manifest 已记录旧根，两者还必须一致。`--fairygui-adopt-existing` 只复用同名人工根组件的既有资源 ID，避免已有 `ui://` URL 断裂，不扩大清理权限。报告在工程提交成功后使用临时文件原子替换；失败时返回 `reportStatus: failed` 与诊断，不把已提交工程误报为整体失败。

显式标签决定 FairyGUI 结构，AI 识别只进入结构化报告。导出器支持图片、Loader、Graph、可编辑文本、复杂文本栅格化、九宫格、mask、Button、Toggle、Slider、Input、Dropdown、ScrollView/GList、原生 ScrollBar、hbox/vbox 和稳定同构 Grid 的 `flow_hz` 映射。无显式状态角色的 Button 普通视觉绑定 `up`，状态角色由 group 承载时生成本地子组件以保留完整状态子树；Slider 结合 fill/handle 与容器几何推断方向，置信不足默认水平并报告；所有普通 image/group 与显式 Mask 都统一检查 vector mask、非 normal 混合模式和剪贴链，并对不可编辑语义明确报告降级。PSD 图层数组按原始上到下顺序直接写入 FairyGUI `displayList`，确保背景先绘制、前景后绘制。Dropdown 支持嵌套 template/item；ScrollView 消费 viewport/content，角色不完整的滚动条保留视觉并隐藏运行时滚动条；Grid 仅在单元尺寸、行列、间距和视觉签名稳定时生成 GList，否则保留绝对坐标并报告降级。

`ref` 优先解析当前页面受管组件，再解析现有包内唯一逻辑名，最后使用配置映射或本地图像/文本/子树降级；自动发现和显式配置的本地组件引用都必须在 `package.xml` 登记且存在对应 XML。`refp` 只接受显式跨包配置。所有悬空资源、重复 ID、非法 XML 和生成资源 ID 冲突在提交前阻断。

## 曾考虑的替代方案

**直接移植旧的独立 `psd-fgui` CLI：** 旧工具能证明 FairyGUI XML 可由纯文件生成，但使用另一套标签、场景图、递增 ID、整包覆盖和全部文字栅格化；本实现只复用其格式样本与测试经验，避免解析器分叉。

**把现有 JSON 作为 FairyGUI 唯一输入：** JSON 缺少 RGBA、Photoshop 图层 ID、蒙版和完整标签意图，无法同时满足稳定 ID、透明裁剪、复杂文本降级和事务安全，因此后端直接消费单次 PSD 解析文档。

**每个 PSD 创建独立工程或独立包：** 隔离简单但会放大工程数量、削弱公共资源复用，也不符合一个工程固定包、多页面共存的工作方式。

**对受管页面 XML 做 Diff-Merge：** relation、transition、手工层级等合并规则复杂且容易产生不可见冲突；当前受管页面以 PSD 为事实源全量重建，sidecar 清单保护非受管资源。

**让 AI 高置信度结果直接生成交互控件：** 误识别会改变运行时语义；显式标签才是结构契约，AI 只生成可追踪建议。

## 后果

收益是旧 CLI 默认行为保持兼容，同一 PSD 可选择 UGUI、FairyGUI 或两者，多个页面可以安全共存，重复导出保持公开资源 ID 稳定，失败不会提交悬空包引用；FairyGUI 组件能在 Editor 6.x 中继续编辑。

代价是受管页面中的 FairyGUI 手工修改不承诺保留，发布仍由 FairyGUI Editor 和既有 Unity 工作流完成；XML 依赖 Editor 6.x 事实格式，首版不维护 5.x 双格式；无法无损表达的复杂效果、非规则布局和不完整滚动条会以可追踪 warning 降级。

## 验证

- `PSDExporterProject/tests/fairygui/` 覆盖 XML Writer、身份碰撞、sourceId 回退、相对 sourcePath 迁移与冲突、manifest 完整性、引用文件闭合与内容换代、事务、报告失败状态、Button 状态子树、Slider、Mask、普通复杂语义、Dropdown、ScrollView、ScrollBar、Grid、隐藏状态和跨页字节稳定性。
- `PSDExporterProject/tests/integration/fairygui-real-psd.test.ts` 使用真实 `UIArtifacts/psd/demo.psd` 验证像素、XML、报告、引用降级和两次导出稳定性。
- 串行执行 `npm run build` 通过；`npm test -- --runInBand` 通过，26 个测试套件、310 个测试全部通过。
- `UIArtifacts/psd/demo-fairygui-20260820` 已由真实 `demo.psd` 重生成；manifest 的 40 个受管文件内容 hash 与文件一致，所有 XML、`package.xml` 和本地引用全部闭合，背景位于 Dropdown 之前，报告包含 51 条图层映射、12 条 AI 建议和 23 条诊断。
- FairyGUI Editor 6.0.5 已打开生成工程及根文档 `ui://42dckgspydaptdiu`，组件 XML 确认 Button、Toggle、Slider、ComboBox、Input、ScrollPane 和 ScrollBar 结构；当前终端没有显示捕获权限，截图级视觉对照仍需交互会话补充。

## 延后事项

- 有显示权限的交互会话中，继续人工核对根页面的视觉、原生控件和编辑性，并补充截图证据。
- 根据真实生产 PSD 再决定是否增加更多 ScrollBar 视觉部件、Grid 参数配置或受管页面局部合并。
