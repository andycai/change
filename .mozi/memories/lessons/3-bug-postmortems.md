# 3. Bug & Error Post-Mortems (技术踩坑与错误诊断)

*编译报错、运行时崩溃的具体代码坑*

- **ag-psd 原始像素模式仍需初始化 Canvas：** 使用 `readPsd(..., { useImageData: true })` 时省略 `initializeCanvas`，解码阶段仍报 `Canvas not initialized` -> 无论输出 canvas 还是 `imageData`，模块加载后都先注册 `createCanvas`
- **子代理参数只传有效值：** 调用 `spawn_agent` 时给可选字段传空串，且同时传 `message` 与 `items`，连续触发参数校验失败 -> 省略无须覆盖的可选字段，并在 `message`、`items` 中只传一种输入
- **多文件补丁分段完整：** 手写多文件 `apply_patch` 时漏掉文件段边界，导致补丁解析失败且整体未应用 -> 每个 `Update File` 段使用完整 hunk，复杂修改拆成多个独立补丁
- **补丁同路径操作拆分：** 一个 `apply_patch` 同时删除并新增同一路径文件，补丁校验失败且整体未应用 -> 删除和新增同一文件拆成两个独立补丁
- **联合效果类型需显式收窄：** HTML 预览生成器用普通 `filter` 筛选文本效果后，TypeScript 仍按基础 `TextEffect` 处理，访问阴影/发光字段导致构建失败 -> 对联合类型筛选使用显式类型守卫
- **PSDExporterProject ESLint 配置格式：** 项目使用 ESLint 10，但未提供 `eslint.config.js|mjs|cjs`，执行 `npm run lint` 会在加载配置阶段失败 -> 运行 lint 前先确认 flat config 已存在，或在单独变更中迁移旧配置
- **PSDExporterProject 测试前同步锁文件依赖：** `package.json` 已声明 `@types/color-convert`，但旧 `node_modules` 未安装该包，导致 Jest 在 TypeScript 加载阶段失败 -> 测试前先执行 `npm install`（或干净环境执行 `npm ci`）确保本地依赖与 `package-lock.json` 一致
- **ag-psd 测试 mock 需包含 initializeCanvas：** `PsdParser` 模块顶层会调用 `initializeCanvas`，测试只 mock `readPsd` 会导致 Jest 加载时报 `(0, initializeCanvas) is not a function` -> mock `ag-psd` 时同时提供 `initializeCanvas: jest.fn()`

<!-- 容量上限：15-20 条。超出时合并或归档旧条目 -->
