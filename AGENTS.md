# 代理初始化

在执行任何操作前，必须按顺序加载 `.mozi/memories/` 下的三层规则体系：

1. **GENERAL.md** — 通用行为准则与编码哲学
2. **PROJECT.md** — 技术栈、架构与项目约定
3. **LESSONS.md** — 经验教训、偏好与环境红线（动态知识库，含分类文件索引）

## 自我纠错（从错误中学习）

**每个错误都是成长的信号。** 遇到以下任一情况，立即执行记录：

| 触发信号 | 示例 |
|---------|------|
| 用户纠正输出 | "你错了"、"不对"、"改成..." |
| 命令执行失败 | 测试失败、编译报错、git 操作失败 |
| 中途意识到错误 | 方向错了、误解了需求 |
| 发现更优工作方式 | 换个顺序更快、换个工具更好 |

**过滤条件**：未来 3 个月内可能再次发生才记录。一次性拼写错误当场修正即可。

**执行方式**：
1. 读取 `.mozi/memories/lessons/` 下对应的分类文件
2. 按格式 `- **标题：** 发生了什么 -> 正确做法` 追加到文件顶部
3. 在当前任务中立即应用该教训
4. 回复 `已记录教训：<标题>`

详细协议见 `Skill: mozi-learn`。

## Agent Notes

本仓库采用 `.mozi/notes/` 记录非平凡变更中的决策、权衡、被否决方案和长期约束。除纯机械编辑和不涉及决策内容的局部编辑外，所有非平凡变更都必须在同一变更中新增或更新至少一份 Agent Note；这是代理行为约定，不接入自动门禁。[`.mozi/notes/README.md`](.mozi/notes/README.md) 是生命周期、分类、Markdown-only 格式和归档合同的唯一权威规范；[`.mozi/notes/AGENTS.md`](.mozi/notes/AGENTS.md) 及生命周期目录中的 `AGENTS.md` 只补充对应作用域的操作约束，不重新定义格式合同。

Agent Note 的唯一生命周期入口是 `mozi-agent-notes`。写入或迁移后运行其 `scripts/validate_agent_notes.py`；归档使用同一技能的 `scripts/archive_agent_note.py`，默认 dry-run，必须显式 `--apply` 才执行移动。

### 结束前检查

**每个会话结束前**，自查是否遗漏了应记录的教训。如有遗漏，立即补记。

<claude-mem-context>
# Memory Context

# [fun] recent context, 2026-04-27 12:19pm GMT+8

Legend: 🎯session 🔴bugfix 🟣feature 🔄refactor ✅change 🔵discovery ⚖️decision 🚨security_alert 🔐security_note
Format: ID TIME TYPE TITLE
Fetch details: get_observations([IDs]) | Search: mem-search skill

Stats: 50 obs (21,917t read) | 0t work

### Apr 26, 2026
S819 Unity skill system architecture consultation for 5V5 card battle game with semi-auto combat (Apr 26 at 10:30 PM)
S815 Unity skill system architecture consultation for 5V5 card battle game with semi-auto combat (Apr 26 at 10:31 PM)
S818 Unity skill system architecture consultation for 5V5 card battle game with semi-auto combat (Apr 26 at 10:31 PM)
S820 Unity skill system architecture consultation for 5V5 card battle game with semi-auto combat (Apr 26 at 10:33 PM)
S821 Skill system design spec review and refinement (Apr 26 at 10:35 PM)
S822 Skill system design specification completed and committed (Apr 26 at 10:36 PM)
S823 Skill system implementation plan created and committed. Task dependency graph established in worktree. Ready to begin implementation. (Apr 26 at 10:40 PM)
S825 Unity C# compilation error: ITrigger interface missing TryFire method definition (Apr 26 at 10:41 PM)
### Apr 27, 2026
S824 Unity skill system compilation error fix and completion (Apr 27 at 7:17 AM)
1930 7:54a 🔴 Task 3 release-path exception leak fixed with aggregate failure handling
1927 " 🔵 Final spec compliance review initiated for Task 3 after error handling fix
1932 7:59a 🔵 Task 3 spec compliance verified and approved after release-path fix
1934 8:02a 🔵 Code quality review found High-severity asset leak risk in FairyGUI adapter
1931 " 🔵 Final spec compliance review initiated for Task 3 after release-path fix
1933 8:05a 🔵 Code quality review initiated for Task 3 after spec compliance approved
1935 8:10a 🔵 AssemblyInfo.cs reveals InternalsVisibleTo attributes for test assemblies
1936 " 🔵 Fix requested for Task 3 High and Low severity code quality issues
1937 " 🔴 Task 3 High and Low severity quality issues fixed
1938 8:13a 🔵 Agent pool capacity exhausted when attempting re-review after quality fix
1939 " 🔵 Final quality check initiated for Task 3 after commit cc9cd4b
1940 " 🔵 Task 3 quality gate passed after verification of High and Low severity fixes
1941 8:16a 🔵 Task 4 implementation initiated for GameScript sample presenter/use-case flow and architecture guards
1942 " 🟣 Task 4 GameScript sample presenter/use-case flow implemented with architecture guardrails
1944 " 🔵 Task 4 spec compliance verified and approved for GameScript sample and guardrails
1945 8:22a 🔵 Code quality review initiated for Task 4 GameScript sample and architecture guardrails
1946 " 🔵 Task 4 code quality review identified multiple severity issues in architecture guardrails
1943 " 🔵 Code review initiated for Task 4 GameScript sample and architecture guardrails
1947 8:24a 🔵 Fix requested for Task 4 architecture guardrails and test robustness issues
1948 " 🔴 Task 4 architecture guardrails strengthened with structural dependency checks and improved test robustness
1949 8:27a 🔵 Final quality re-review initiated for Task 4 after architecture guardrail fixes
1950 " 🔵 Task 4 quality gate passed after architecture guardrail fixes verified
1951 8:32a 🔵 Task 5 final validation and documentation sync initiated
1953 " 🟣 Task 5 final validation and documentation sync completed for UI shell MVP
1955 " 🔵 Unity PlayMode tests require HybridCLR infrastructure
1956 " 🔵 Task 5 compliance blocked by vacuous PlayMode test execution
1957 " 🔵 Unity test filter syntax differs between EditMode and PlayMode platforms
1958 " ⚖️ Task 5 compliance accepted with infrastructure limitation documented
1954 8:36a 🔵 Git log reveals complete UI shell MVP implementation commit history across 14 commits
1959 9:35a 🔴 Fixed WindowManager late caller cancellation bug in inflight entry reuse
1960 9:42a 🔴 Added test coverage for WindowManager late caller cancellation bug fix
1961 " 🔴 WindowManager late caller cancellation bug fix validated by tests
1962 " 🔴 WindowManager bug fix validated across full Runtime UI test suite
1963 9:43a 🔴 WindowManager late caller bug fix committed after test validation
1965 " ✅ Created clean git worktree for UI shell MVP integration
1966 10:31a ✅ Created clean worktree and enumerated UI shell MVP commits for integration
1967 10:36a 🔵 Cherry-pick merge conflict encountered in Change.Runtime.asmdef during clean branch integration
1968 " 🔵 Identified asmdef merge conflict: YooAsset reference missing in WindowManager branch
1969 10:37a 🔴 Resolved asmdef merge conflict by preserving YooAsset reference from main branch
1970 " 🔵 Cherry-pick continuation failed - no cherry-pick in progress after conflict resolution
1971 " 🔵 Cherry-pick state validation revealed staged files without CHERRY_PICK_HEAD
1972 " ✅ Cherry-pick integration succeeded: WindowManager commits consolidated into single feature commit
1973 " 🔴 Resolved second asmdef merge conflict by adding FairyGUI reference from YooAsset/FairyGUI adapter commits
1974 " 🟣 Cherry-pick integration succeeded: YooAsset and FairyGUI adapters consolidated into feature commit
1975 " 🔵 Skill system framework exists uncommitted on main branch
1976 10:49a 🟣 UI shell MVP implementation merged into main branch
1977 " 🔵 Clean worktree removal blocked by untracked Skill system files
1978 " ✅ UI shell MVP development worktrees and branches cleaned up after integration
1980 10:51a 🔴 Removed type cast in TickTriggers after adding TickCooldown to ITrigger interface
S826 Fix CS1061 compilation error: ITrigger interface missing TryFire method definition (Apr 27 at 10:51 AM)
1981 10:52a 🔴 Fixed CS1061 compilation error by adding missing methods to ITrigger interface
</claude-mem-context>
