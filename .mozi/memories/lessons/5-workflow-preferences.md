# 5. Workflow & Preferences (工作流与个性化偏好)

*个人研发习惯和工具偏好*

- **代码提交风格：** 提交时没使用前缀 -> 始终使用 `feat:`、`fix:`、`chore:`、`docs:`、`refactor:` 等前缀
- **Worktree 首次 Unity 导入有冷库代价：** 从 main 切 worktree 后，UnityProject/Library 为空，首次 Unity batchmode 运行触发全量导入（5-10 分钟）。后续运行正常。需要在 plan 中考虑此开销，或将测试运行批量化为每个检查点运行而非每步骤运行。

<!-- 容量上限：15-20 条。超出时合并或归档旧条目 -->
