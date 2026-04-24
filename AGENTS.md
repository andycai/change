# Change (嫦娥) - Unity Game Project

## Project Overview

Change is a Unity mobile game project built with a hot-update architecture centered on HybridCLR. The project follows a layered framework design pattern with business-agnostic reusable infrastructure under `Change/Framework`.

## Tech Stack

| Component | Version | Purpose |
|-----------|---------|---------|
| Unity | 2022.3.60f1 (LTS) | Engine |
| URP | Planned (currently Built-in RP) | Render pipeline |
| HybridCLR | 8.9.0 (embedded) | C# hot-update |
| YooAsset | 2.3.18 | Asset management & bundling |
| FairyGUI | embedded | UI framework |
| UniTask | 2.5.10 | Async/await for Unity |
| Obfuz | git | Code obfuscation |
| Obfuz4HybridCLR | git | Obfuz + HybridCLR integration |
| Nino | 4.0.0-preview.147 | High-performance serialization |
| Luban | Planned | Configuration/data generation |
| IngameDebugConsole | git | In-game debug console |

## Project Structure

```
fun/
├── AGENTS.md                    # This file - project instructions
├── docs/
│   └── superpowers/
│       ├── specs/               # Design specifications
│       │   └── *-change-directory-structure-design.md
│       └── plans/               # Implementation plans
│           └── *-change-directory-structure-plan.md
└── UnityProject/                # Unity project root
    ├── Assets/
    │   ├── Change/              # Main game code
    │   │   ├── Editor/          # Editor tools & extensions (Change.Editor)
    │   │   ├── Framework/       # Business-agnostic framework (Change.Framework)
    │   │   │   ├── Cqrs/        # CQRS module
    │   │   │   ├── Pooling/     # Object pooling module
    │   │   │   ├── Collections/ # High-performance collections module
    │   │   │   ├── Fsm/         # Finite state machine module
    │   │   │   ├── Logging/     # Logging abstractions (ILogSink, LogRouter)
    │   │   │   ├── AssemblyInfo.cs  # InternalsVisibleTo for test assemblies
    │   │   │   └── Tests/EditMode/  # Framework EditMode tests
    │   │   └── Runtime/         # Game runtime code (Change.Runtime)
    │   │       ├── Timer/       # Timer module
    │   │       ├── Logging/     # Logging implementations (UnitySink, FileSink)
    │   │       ├── AssemblyInfo.cs  # InternalsVisibleTo for test assemblies
    │   │       └── Tests/       # Runtime tests
    │   │           ├── EditMode/    # Runtime EditMode tests
    │   │           └── PlayMode/    # Runtime PlayMode tests
    │   ├── GameScript/          # Game scripts (hot-update assembly)
    │   ├── Resources/           # Unity Resources folder
    │   └── Samples/             # Package samples (gitignored)
    ├── Packages/
    │   ├── manifest.json        # Package dependencies
    │   ├── com.code-philosophy.hybridclr@8.9.0/  # HybridCLR (embedded)
    │   └── com.fairygui.unity/                    # FairyGUI (embedded)
    ├── Bundles/                 # AssetBundles output (gitignored)
    ├── yoo/                     # YooAsset cache (gitignored)
    └── ProjectSettings/
```

## Architecture & Design Conventions

### Framework Layer (`Change/Framework`)

Business-agnostic reusable infrastructure (engine-agnostic, usable in any C# environment). Key principles:

1. **Zero external dependencies** - Framework code must not depend on third-party libraries.
2. **Business-agnostic** - No domain coupling; framework never depends on business assemblies.
3. **Hot-path 0GC** - Runtime dispatch paths must produce zero managed allocations after warm-up.
4. **Fail-fast** - Misconfiguration (missing registration, duplicate registration, post-freeze modification) throws immediately.
5. **Synchronous execution model** - MVP scope is sync-only; no async/await in framework core.
6. **Explicit registration** - No reflection-based auto-scan; handlers registered manually during bootstrap, then `Freeze()`.
7. **Struct messages** - Command/Query/Event types should be `readonly struct` to avoid boxing and allocation.

### Designed Framework Modules

- **FSM** (`Change/Framework/Fsm`) - Event-driven finite state machine with `IFsmState<TStateId, TEvent>`, single active state, FIFO event queue, serial processing.
- **CQRS** (`Change/Framework/Cqrs`) - Command/Query/Responsibility Segregation with struct messages, class handlers, generic strongly typed dispatch.
- **High-Performance Collections** (`Change/Framework/Collections`) - FastDictionary, FastList, FastHashSet, RingBuffer, FastPriorityQueue.
- **Pooling** (`Change/Framework/Pooling`) - Object pooling with zero-GC dispatch paths.
- **Logging** (`Change/Framework/Logging`) - Engine-agnostic logging abstractions (ILogger, ILogSink, LogRouter). Concrete sinks (UnitySink, FileSink) live in `Change/Runtime/Logging`.

### Assembly Definitions

6 assemblies total:

| Assembly | Platform | Purpose |
|----------|----------|---------|
| `Change.Framework` | Any | Engine-agnostic framework |
| `Change.Framework.EditModeTests` | Editor | Framework tests |
| `Change.Runtime` | Any | Runtime code (depends on Framework) |
| `Change.Runtime.EditModeTests` | Editor | Runtime EditMode tests |
| `Change.Runtime.PlayModeTests` | Standalone | Runtime PlayMode tests |
| `Change.Editor` | Editor | Editor tools & extensions |

### Hot-Update Architecture (HybridCLR)

- AOT assemblies: framework and engine-level code compiled into the player
- Hot-update assemblies: business logic loaded at runtime via HybridCLR
- Obfuz provides code obfuscation for both AOT and hot-update DLLs
- Menu paths: `HybridCLR/` for hot-update operations, `HybridCLR/ObfuzExtension/` for obfuscation

### Asset Management (YooAsset)

- Asset collection and bundling via YooAsset v2.3.18
- Bundle output: `UnityProject/Bundles/` (gitignored)
- Cache directory: `UnityProject/yoo/` (gitignored)
- Sample collector config references: Effect, Entity, Audio, Shader, Scene, UIPanel, UIFont, UISprite, UISpriteAtlas groups

### UI (FairyGUI)

- UI framework via embedded FairyGUI package
- Editor tools available under FairyGUI menu
- FairyGUI resources expected in Assets or loaded via YooAsset

## Coding Conventions

- **Language**: C# targeting .NET Standard 2.1 compatible with Unity 2022.3
- **Assembly definitions**: 6 assemblies (see table above); `InternalsVisibleTo` declared in `AssemblyInfo.cs` for test access
- **Namespace convention**: Single root namespace per assembly — `Change.Framework`, `Change.Runtime`, `Change.Editor`
- **Generic constraints**: Use `where T : struct` for message types to enforce value-type semantics
- **`in` keyword**: Use `in` parameters for struct passing to avoid copy overhead
- **No reflection on hot paths**: Generic strongly-typed dispatch only; no `object`-based or reflection-based dispatch at runtime
- **Exception policy**: Framework uses `InvalidOperationException` for programming errors; exceptions propagate to caller without swallowing

## Coding Behavior Guidelines

*These guidelines bias toward caution over speed. For trivial tasks, use judgment.*

### Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

### Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

### Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

*These guidelines are working if: fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.*

## Superpowers Worktree Rule

- When running superpowers `subagent-driven-development` or `executing-plans`, always create a git worktree under `.worktrees/` and execute the workflow there.
- Do not run these two superpowers workflows directly in the main workspace.

## Build & Tooling Notes

- HybridCLR menu: `HybridCLR/CompileDll`, `GenerateAOTReference`, `GenerateLinkXml`, etc.
- Obfuz menu: `HybridCLR/ObfuzExtension/GenerateAll`, `CompileAndObfuscateDll`, `GeneratePolymorphicCodes`
- YooAsset: Use YooAsset editor window for asset collection and bundle building
- FairyGUI: Use FairyGUI editor for UI editing; publish to Unity project
- Luban: Configuration generation (to be set up)
- Unity Test Framework (`com.unity.test-framework@1.1.33`) CLI runs: avoid `-quit` with `-runTests`, or command-line test args may not execute; run without `-quit` to generate XML results.

## Version Control

- `.gitignore` is scoped to `UnityProject/` subdirectory
- Excluded: `Library/`, `Temp/`, `Build/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`, `Samples/`, `Bundles/`, `yoo/`
- Embedded packages (HybridCLR, FairyGUI) are versioned in `Packages/`

## Design Documents

All design specs and implementation plans live under `docs/superpowers/`:
- `specs/` - Approved design baselines
- `plans/` - Detailed implementation plans

When implementing framework modules, refer to the corresponding spec first. Specs define contracts, constraints, and acceptance criteria. Plans translate specs into implementation steps.

## graphify

This project has a graphify knowledge graph at graphify-out/.

Rules:
- Before answering architecture or codebase questions, read graphify-out/GRAPH_REPORT.md for god nodes and community structure
- If graphify-out/wiki/index.md exists, navigate it instead of reading raw files
- After modifying code files in this session, run `graphify update .` to keep the graph current (AST-only, no API cost)

<claude-mem-context>
# Memory Context

# [fun] recent context, 2026-04-24 11:22pm GMT+8

Legend: 🎯session 🔴bugfix 🟣feature 🔄refactor ✅change 🔵discovery ⚖️decision 🚨security_alert 🔐security_note
Format: ID TIME TYPE TITLE
Fetch details: get_observations([IDs]) | Search: mem-search skill

Stats: 50 obs (10,008t read) | 545,658t work | 98% savings

### Apr 24, 2026
1336 6:13p ⚖️ Runtime Logging Required with Unity and File Sinks
S488 Editor Code Needs Emerging for Fun Framework (Apr 24 at 6:13 PM)
1337 6:15p 🔵 Editor Code Needs Emerging for Fun Framework
S489 Runtime Test Directory Structure Unification Decision (Apr 24 at 6:15 PM)
1338 6:20p ⚖️ Runtime Test Directory Structure Unification Decision
1339 " ⚖️ Unified Namespace Architecture Decision
S490 EditMode Test Assembly Naming Standardized (Apr 24 at 6:20 PM)
1340 6:22p ✅ EditMode Test Assembly Naming Standardized
S493 Change Directory Structure Implementation Plan Created (Apr 24 at 6:22 PM)
S559 Namespace reorganization Fun.* → Change.* for Unity framework (Apr 24 at 6:27 PM)
1345 6:29p 🟣 Change Namespace Directory Reorganization Initiated
1346 6:31p 🔄 Framework Modules Migrated to Change Namespace
1347 6:32p 🔄 Framework Source Files Moved, Tests Remaining
1348 " 🔄 Framework Modules Committed to Git (Commit 91b5cad)
1349 6:36p 🔄 Runtime Modules Moved to Change Namespace
1350 6:37p 🔄 Runtime Modules Committed (Commit 18b7679)
1351 6:38p 🔄 Runtime Module Migration Verified Complete
1353 6:39p 🔄 Test File Reorganization (Task 4) In Progress
1354 " 🔄 Test Files Reorganized to Centralized Structure
1355 6:40p 🔄 Test File Reorganization Completed (Commit b472d8a)
1356 " 🟣 Task 5: Create New Assembly Definition Files
1357 6:41p 🔄 New Assembly Definition Files Created (Commit 0ef7478)
1359 " 🔄 Namespace Replacement in Progress (Task 6)
1360 6:42p 🔄 Namespace Replacement Committed (Commit b9cf28a)
1361 " 🔵 String Literals with "Fun.Timer" Remain in Timer.cs
1362 6:43p 🔴 Timer.cs Namespace Not Replaced
1377 9:32p ✅ Namespace Reorganization Fun.* → Change.* Completed
S562 Fix Unity EditMode test assembly duplicate reference errors in Change.Runtime.EditModeTests.asmdef and Change.Framework.EditModeTests.asmdef (Apr 24 at 9:33 PM)
1379 9:42p 🔴 Unity EditMode Test ASMDEF Duplicate References Fixed
S565 PlayMode Test Assembly Platform Names Updated (Apr 24 at 9:42 PM)
1381 9:43p ✅ PlayMode Test Assembly Platform Names Updated
S566 PlayMode Test ASMDEF Duplicate Reference Removed (Apr 24 at 9:43 PM)
1382 9:44p 🔴 PlayMode Test ASMDEF Duplicate Reference Removed
S568 Fix Unity test assembly duplicate reference errors across all EditMode and PlayMode asmdef files (Apr 24 at 9:44 PM)
S571 Namespace Reorganization Exposed Internal Access Issues (Apr 24 at 9:46 PM)
1385 10:51p 🟣 代码审查启动：Change Framework Pooling 目录
1386 10:52p 🟣 Unity Framework 代码审查任务启动 - Pooling 目录
1388 10:53p ✅ Code Review Initiated for Unity FSM Framework
1389 10:54p 🟣 代码审查启动 - Unity Fsm 状态机框架
1390 " ✅ Unity Fsm Framework Code Review Initiated
1391 10:59p ✅ Code Review Initiated for Unity Pooling Framework
1392 11:00p 🔄 Pool.cs 代码简化：统一 ConditionalWeakTable 追踪机制
1394 " 🟣 新增安全测试：Clear 操作和构造函数异常处理
1395 " ✅ 清理无用代码：删除 ReferenceEqualityComparer
1396 11:01p 🔵 Unity 安装路径发现：/Applications/Unity/Unity.app
1397 " 🔵 Unity EditMode 测试执行失败：进程退出码 2，XML 结果文件未生成
1398 11:02p 🔴 Pool 测试修复：异常类型断言修正
1400 11:03p 🔴 Pool 测试修复：Assert.Throws 改为 Assert.Catch
1401 11:04p 🔵 Unity FSM 框架代码简洁且设计良好
1402 11:05p 🟣 FSM 框架代码审查已启动
1403 11:09p 🟣 Code Review Initiated for Unity Fsm Framework
1405 11:10p 🔴 FSM Callback Depth Tracking Prevents Premature Event Draining
1406 11:11p ✅ Changes Committed to Repository
1407 11:12p 🔵 FSM Framework Code Review Completed Successfully
1409 " 🔴 FSM Transition Drain Guard Fixed
1411 11:13p 🔴 Pooling Framework Lease Tracking Simplified and Hardened
1412 11:17p 🔴 StateMachine Re-entry Protection and FSM Test Updates
1414 " 🔴 Pool.cs Simplification Introduced Compiler Errors
1415 11:18p 🔴 Pool.cs Naming Conflict Resolved - Tests Passing
1416 " 🔄 Pool.cs Lease Tracking Simplified Using ConditionalWeakTable Marker Pattern

Access 546k tokens of past work via get_observations([IDs]) or mem-search skill.
</claude-mem-context>
