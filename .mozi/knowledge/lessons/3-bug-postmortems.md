# 3. Bug & Error Post-Mortems (技术踩坑与错误诊断)

*编译报错、运行时崩溃的具体代码坑*

## 通用教训

- **Git 不移动未跟踪内容：** 用 `git mv` 搬运未跟踪文件、只含未跟踪文件的目录或空目录时会被判定为不受版本控制或 source directory is empty -> 已跟踪内容使用 `git mv`，未跟踪文件和目录使用普通 `mv`，空目录在目标位置按合同重建
- **多步命令失败后继续执行：** 同一 shell 中前一步失败后仍执行依赖其结果的后续命令，制造额外失败或误判整体状态 -> 有依赖的多步命令用 `&&` 或显式 `set -e`；失败后先检查实际文件状态，再决定是否继续
- **stash 路径过滤：** 用 `git stash show -p stash@{0} -- <path>` 查看单文件时路径被当成额外 revision -> 使用 `git diff stash@{0}^1 stash@{0} -- <path>` 精确读取 stash 文件补丁
- **可选搜索根目录：** 裸 `rg` 零匹配退出 1，或直接 `find` 不存在的可选缓存根目录都会制造无意义失败，宽泛模式还会命中合法 schema -> 先用路径存在性或已知工具输出构造真实搜索根并限定语境，再区分问题、合法命中和预期零匹配
- **zsh 参数陷阱：** 把退出码赋给只读 `status`、用与 `PATH` 绑定的特殊数组 `path` 作循环变量，或未引用含 `?` 和可选 glob 的参数，会让 shell 自身或后续命令失败 -> 使用 `rc`/`exit_code`/`item`/`entry` 等安全变量名，URL 和模式单引号包裹，可选文件先由 `rg --files`/`find` 构造

## 当前项目教训

- **PSDExporterProject 测试前同步锁文件依赖：** `package.json` 已声明 `@types/color-convert`，但旧 `node_modules` 未安装该包，导致 Jest 在 TypeScript 加载阶段失败 -> 测试前先执行 `npm install`（或干净环境执行 `npm ci`）确保本地依赖与 `package-lock.json` 一致
- **ag-psd 测试 mock 需包含 initializeCanvas：** `PsdParser` 模块顶层会调用 `initializeCanvas`，测试只 mock `readPsd` 会导致 Jest 加载时报 `(0, initializeCanvas) is not a function` -> mock `ag-psd` 时同时提供 `initializeCanvas: jest.fn()`
- **ag-psd 原始像素模式仍需初始化 Canvas：** 使用 `readPsd(..., { useImageData: true })` 时省略 `initializeCanvas`，解码阶段仍报 `Canvas not initialized` -> 无论输出 canvas 还是 `imageData`，模块加载后都先注册 `createCanvas`
- **九宫格裁剪测试先独立验算：** 测试宣称裁剪后中心区越界，但实际换算仍完整落在纹理内，造成错误红灯 -> 测试数据先独立计算原中心区与裁剪窗口交集，只有中心区被截断时才断言禁用裁剪
- **联合效果类型需显式收窄：** HTML 预览生成器用普通 `filter` 筛选文本效果后，TypeScript 仍按基础 `TextEffect` 处理，访问阴影/发光字段导致构建失败 -> 对联合类型筛选使用显式类型守卫

<!-- 每个作用域分区以 15-20 条为整理触发线。超出时合并或归档旧条目。 -->
