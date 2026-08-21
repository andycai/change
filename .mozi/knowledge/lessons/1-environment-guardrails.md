# 1. Local Environment Guardrails (环境禁忌与红线)

*绝对禁止的行为——会破坏环境或越权*

## 通用教训

- **既有未跟踪产物不可清理：** 收尾时把任务前已存在的未跟踪 `__pycache__/` 当成本次测试产物删除 -> 开工状态中的未跟踪文件和生成目录也视为用户改动；只清理明确由本次命令新建且路径已预先记录的产物
- **markdown auto-fix 破坏中文标点：** pi 的 edit 工具对中文 .md 写文件时自动运行 markdownlint fix，把全角冒号/分号（：；）改成半角（:;）、直引号改弯引号 -> 改中文 .md 后用 git diff 检查标点是否被破坏，被破坏的全角标点用 Python 脚本恢复；需精确控制标点的 .md 改动优先用脚本而非 edit
- **临时与生成物清理：** 验证命令用 `rm -rf` 清理 `mktemp` 目录，或用 `rm -f` 删除字节码，都会被环境策略拒绝 -> 临时目录使用 Python `TemporaryDirectory` 自动清理；仅清理本轮预先记录的单个产物时使用 `Path.unlink()` 和 `Path.rmdir()`
- **禁止署名：** 提交时的 commit message 包含 `Co-Authored-By`、`Signed-off-by`、`Reviewed-by` 等自动署名行 -> 不要带自动署名行

## 当前项目教训

_暂无。_

<!-- 每个作用域分区以 15-20 条为整理触发线。超出时合并或归档旧条目。 -->
