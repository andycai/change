# Agent Note: 按图层生成可交互 HTML 页面

Status: implemented

## 问题

仅把 PSD 合成图嵌入 HTML 只能查看静态截图，无法验证 Button、Toggle、InputField、Slider、Dropdown 和 ScrollView 的交互区域与组件识别结果。用户需要的是按 PSD 排版拼装的可操作页面，而不是整图查看器。

本决策完全取代[使用 PSD 合成画布生成独立 HTML 预览](2026-08-20-psd-html-preview.md)。旧方案不再存在于数据模型、生成器或测试中。

## 决策

`PsdParser` 在 `Layer.text` 中保存 PSD 文本内容，并继续为 image/shape 图层导出 PNG。`HtmlPreviewGenerator` 根据图层绝对坐标、尺寸、可见性、不透明度和文本样式生成 DOM，以导出的切图作为每个图层的视觉皮肤。

组件识别结果决定语义元素和脚本行为：Button 使用 `<button>`，Toggle 使用 checkbox，InputField 使用文本输入框，Slider 使用 range，Dropdown 生成触发按钮与选项菜单，ScrollView 使用浏览器原生滚动容器。HTML 不嵌入 PSD 合成图，也不把合成图作为页面底图。

已有 `demo.psd` 使用 `tmpbtn`、`tmptxt`、`fillcolor`、`tips`、`iptlb` 等旧版点号标签，因此 `TagParser` 将这些标签归一化到当前组件 family，保证 JSON 和 HTML 使用一致的组件类型。

## 曾考虑的替代方案

**继续保留整图并覆盖透明控件热区：** 该方案交互区域可以点击，但页面视觉仍来自单张图，无法验证图层切图、文本导出和组件拼装结果，因此不满足导出页面预览的目的。

**使用 Canvas 重绘所有图层并自行实现命中测试：** 该方案能控制绘制顺序，但输入框、下拉框、滚动和无障碍行为都需要重复实现浏览器能力，复杂度高于使用语义 DOM。

**完全复制 Photoshop 渲染效果：** 当前图层模型不包含全部变换、蒙版和混合模式语义。实现选择优先验证 UI 组件排版和交互，不承诺与 Photoshop 像素级一致；合成图可由用户另行打开 PSD 对照，但不进入交互 HTML。

## 验证

单元测试覆盖文本内容进入 JSON、图层坐标、图片路径、HTML 转义和六类交互组件的语义元素与脚本标记。真实 `UIArtifacts/psd/demo.psd` 导出用于确认识别出 3 个 Button、2 个 Toggle、1 个 InputField、1 个 Slider、1 个 Dropdown 和 2 个 ScrollView，并在 HTML 中生成对应控件。

## 后果

收益是浏览器页面可以真实操作主要 Unity UGUI 控件，并能直接检查切图、文本与组件边界。代价是 HTML 依赖旁边的切图目录，移动或分享时必须同时携带资源；未完整建模的 Photoshop 特效可能与原稿存在视觉差异。旧版标签别名继续作为输入兼容层，但 JSON 始终输出归一化后的组件类型。
