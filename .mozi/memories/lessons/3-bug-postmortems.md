# 3. Bug & Error Post-Mortems (技术踩坑与错误诊断)

*编译报错、运行时崩溃的具体代码坑*

- **受管路径必须限制在工程根：** manifest/transaction 中的相对路径若被损坏或篡改，直接 `join(projectPath, file)` 可能让恢复或旧文件清理触碰工程外文件 -> 所有来自 sidecar 的路径先用 `resolve` 做工程根边界校验，越界时阻断恢复和清理

- **代际签名覆盖实际导出依赖：** generation 漏掉复杂图层语义会错误复用旧目录，把整个包的无关资源都签入又会导致其他页面变化触发当前页换代 -> 签名覆盖所有影响当前页输出的解析字段和实际引用目标，但排除未被当前页消费的包状态

- **产物校验先确认 sidecar 合同：** 临时验收脚本误以为页面 manifest 包含 `managedFiles`，实际该列表位于结构化报告，导致校验脚本自身失败 -> 编写跨 sidecar 校验前先读取实际 schema/键名，页面身份和资源关系取 manifest，文件清单与诊断取报告

- **ID 容量保护需遍历唯一候选：** 把 `36 ** length` 直接当作加盐哈希的最大重试次数，哈希重复导致空间尚未占满也提前报耗尽 -> 保留有限次加盐兼容路径后，确定性枚举尚未尝试的完整 ID 空间，只有所有唯一候选都被占用才报错

- **稳定路径段用子节点名：** 构建 sourceId 回退路径时给每个 child 的路径段误用了当前 parent 名，不同父组下同构子树可能生成相同身份 -> sibling ordinal 路径段必须使用 child 自身规范名，再拼入祖先路径

- **历史源路径禁止模糊匹配：** 用绝对路径后缀匹配旧 manifest 的相对 `sourcePath`，会把不同根目录下的同名 PSD 误认成同一页面 -> 只有在显式 `sourceRoot` 下精确解析相对路径，且已记录旧根时两者一致，才允许迁移并写回绝对路径

- **重构补丁先读精确上下文：** 连续重构后仍按旧 import 排序和旧函数体打补丁，`apply_patch` 因上下文不匹配失败 -> 每轮较大补丁后先读取下一处目标的当前精确行，再做小块补丁

- **搜索反引号用单引号：** `rg` 模式含反引号却放在双引号中，zsh 把它当命令替换并在管道字符处报解析错误 -> 搜索模板字符串或 Markdown 反引号时使用单引号包裹模式

- **保序 AST 跳过注释：** `package.xml` preserve-order AST 校验把 `#comment` 节点当资源检查 ID，导致合法人工注释触发 `Resource is missing id` -> 遍历资源 AST 时显式跳过注释和非资源节点，再校验真实资源属性

- **碰撞测试不超容量：** 用 1 位 base36 ID 请求 40 个唯一结果，分配器在仅 36 个可用值的空间中无限重试 -> 压缩哈希碰撞测试的样本数必须小于 ID 空间容量，并同时保证确定性样本确有碰撞

- **搜索目录先验证：** 一条 `rg` 审计命令混入不存在的测试目录，导致有效搜索结果仍以非零码结束 -> 组合搜索前用 `rg --files` 确认目录，或只传已存在路径

- **FairyGUI 层序不可反转：** 将 PSD 图层数组统一 `reverse()` 后写入 FairyGUI `displayList`，背景落到覆盖层并遮挡全部元素 -> PSD 解析顺序直接保持写入 XML，背景先写、前景后写

- **真实 PSD 集成测试设超时：** ag-psd、canvas 与 sharp 首次加载使真实导出超过 Jest 默认 5 秒 -> 对真实 PSD 集成用例显式设置 30 秒超时，并保留全量回归

- **补丁上下文先核对：** 记录教训时使用了不存在的标题上下文，补丁被拒绝且没有落盘 -> 先读取目标文件的精确上下文，再用最小补丁修改

- **补丁依赖必须完整：** 大段控件补丁先加入了递归角色收集器调用，却漏写对应方法，导致 TypeScript 编译失败 -> 提交补丁前逐项核对新增符号的定义与调用，并先跑编译再写更多测试

- **提交恢复必须识别真实提交点：** 若 `package.xml` rename 成功但 transaction 状态更新失败，恢复时只看旧 `staged` 状态会误删已被包引用的新文件 -> transaction 从一开始记录目标 package hash，恢复时比较当前 `package.xml` hash决定完成 manifest还是清理孤儿文件
- **TypedArray 校验需跨 Realm：** Jest VM 中 ag-psd 返回的 `Uint8ClampedArray` 与主 realm 构造器不同，`instanceof` 误判导致真实像素全部丢失 -> 用 `ArrayBuffer.isView`、元素宽度和长度校验，并在边界处规范化为本 realm 的 `Uint8ClampedArray`
- **tsx eval 避免顶层 await：** 在 CommonJS 项目用 `tsx -e` 顶层 `await`，esbuild 报不支持 CJS 输出格式 -> `tsx -e` 诊断脚本用 async IIFE 包裹异步逻辑
- **事务记录只在完成后删除：** 提交函数在 `finally` 无条件删除 transaction，若 `package.xml` 已切换而 manifest 写入失败就失去恢复依据 -> staging 可始终清理，但 transaction 只能在 manifest 与清理流程完成后删除
- **引用缺失先按合同降级：** 真实 PSD 中 `ref VbarBG` 未配置映射但图层自身仍有像素，导出器直接阻断了整个页面 -> 未解析 `ref/refp` 时先用本地图像、文本或子树降级，只有无本地视觉内容才报阻断错误
- **服务替身遵守结果合同：** CLI 测试让 FairyGUI 导出替身只返回空对象，日志读取 `diagnostics.length` 时失败 -> 跨层测试替身必须返回接口要求的完整最小结果
- **Commander 15 测试需转译 ESM：** 编译后的 CLI 可由 Node 24 正常运行，但 Jest 直接加载 TypeScript CLI 时无法解析 `commander` 15 的 ESM 入口 -> 在 Jest `transformIgnorePatterns` 白名单中加入 `commander`
- **引用测试保持文件闭合：** FairyGUI 引用测试只在 `package.xml` 登记人工组件却未创建对应 XML，预检正确报悬空资源 -> 引用 fixture 必须同时提供资源登记和实际文件
- **条件对象显式定型：** 三元表达式返回不同属性集合时，TypeScript 推断出带可选 `undefined` 的联合，无法赋给 `Record<string, string>` -> 先给结果声明目标对象类型，再构造各分支
- **签名哈希显式传参：** 给确定性 generation 补配置摘要时临时使用 `arguments[3]`，隐藏了参数合同且容易漏算配置 -> 代际函数显式接收强类型 options，并只序列化影响输出的字段
- **九宫格裁剪测试先独立验算：** 测试宣称裁剪后中心区越界，但实际换算仍完整落在纹理内，造成错误红灯 -> 测试数据先独立计算原中心区与裁剪窗口交集，只有中心区被截断时才断言禁用裁剪
- **外部配置先按 unknown 校验：** 把 JSON 配置入口声明为 `Partial<Config>`，嵌套配置在运行时收窄时反而触发 TypeScript 不兼容 -> 反序列化结果和校验入口使用 `unknown`，通过类型守卫后再构造强类型配置
- **Agent Note 链接不可逃逸 Notes 根：** proposed Note 用 Markdown 链接指向 `.mozi/artifacts/`，验证器报 `link escapes notes root` -> Notes 外部产物只写为反引号路径文本，不创建可点击的相对 Markdown 链接
- **ag-psd 原始像素模式仍需初始化 Canvas：** 使用 `readPsd(..., { useImageData: true })` 时省略 `initializeCanvas`，解码阶段仍报 `Canvas not initialized` -> 无论输出 canvas 还是 `imageData`，模块加载后都先注册 `createCanvas`
- **子代理参数只传有效值：** 调用 `spawn_agent` 时给可选字段传空串，且同时传 `message` 与 `items`，连续触发参数校验失败 -> 省略无须覆盖的可选字段，并在 `message`、`items` 中只传一种输入
- **多文件补丁分段完整：** 手写多文件 `apply_patch` 时漏掉文件段边界，导致补丁解析失败且整体未应用 -> 每个 `Update File` 段使用完整 hunk，复杂修改拆成多个独立补丁
- **补丁同路径操作拆分：** 一个 `apply_patch` 同时删除并新增同一路径文件，补丁校验失败且整体未应用 -> 删除和新增同一文件拆成两个独立补丁
- **联合效果类型需显式收窄：** HTML 预览生成器用普通 `filter` 筛选文本效果后，TypeScript 仍按基础 `TextEffect` 处理，访问阴影/发光字段导致构建失败 -> 对联合类型筛选使用显式类型守卫
- **PSDExporterProject ESLint 配置格式：** 项目使用 ESLint 10，但未提供 `eslint.config.js|mjs|cjs`，执行 `npm run lint` 会在加载配置阶段失败 -> 运行 lint 前先确认 flat config 已存在，或在单独变更中迁移旧配置
- **PSDExporterProject 测试前同步锁文件依赖：** `package.json` 已声明 `@types/color-convert`，但旧 `node_modules` 未安装该包，导致 Jest 在 TypeScript 加载阶段失败 -> 测试前先执行 `npm install`（或干净环境执行 `npm ci`）确保本地依赖与 `package-lock.json` 一致
- **ag-psd 测试 mock 需包含 initializeCanvas：** `PsdParser` 模块顶层会调用 `initializeCanvas`，测试只 mock `readPsd` 会导致 Jest 加载时报 `(0, initializeCanvas) is not a function` -> mock `ag-psd` 时同时提供 `initializeCanvas: jest.fn()`

<!-- 容量上限：15-20 条。超出时合并或归档旧条目 -->
