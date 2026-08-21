# 1. Local Environment Guardrails (环境禁忌与红线)

*绝对禁止的行为——会破坏环境或越权*

- **终端会话不可依赖屏幕截图：** 启动 FairyGUI Editor 后调用 `screencapture`，当前执行会话报 `could not create image from display` -> GUI 验收优先使用进程存活、应用窗口元数据和日志；需要视觉截图时由有显示权限的交互会话执行
- **禁止署名：** 提交时的 commit message 包含 `Co-Authored-By`、`Signed-off-by`、`Reviewed-by` 等自动署名行 -> 不要带自动署名行
- **避免递归删除：** 执行一次 PSD 导出前用 `rm -rf` 清理旧输出，被命令安全策略拒绝 -> 不删除用户目录内容，改用不存在的新输出目录或先请求明确确认

<!-- 容量上限：15-20 条。超出时合并或归档旧条目 -->
