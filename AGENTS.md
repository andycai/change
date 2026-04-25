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

- Unity executable path (local machine): `/Applications/Unity/Unity.app/Contents/MacOS/Unity`
- HybridCLR menu: `HybridCLR/CompileDll`, `GenerateAOTReference`, `GenerateLinkXml`, etc.
- Obfuz menu: `HybridCLR/ObfuzExtension/GenerateAll`, `CompileAndObfuscateDll`, `GeneratePolymorphicCodes`
- YooAsset: Use YooAsset editor window for asset collection and bundle building
- FairyGUI: Use FairyGUI editor for UI editing; publish to Unity project
- Luban: Configuration generation (to be set up)
- Unity Test Framework (`com.unity.test-framework@1.1.33`) CLI runs: avoid `-quit` with `-runTests`, or command-line test args may not execute; run without `-quit` to generate XML results.
- Unit test results path rule: always write `-testResults` under `UnityProject/TestResults/` (for example `UnityProject/TestResults/editmode-results.xml`), not `/tmp` or other directories.
- Unit test results naming rule: use `<suite>-<yyyyMMdd-HHmmss>.xml` (for example `UnityProject/TestResults/editmode-cqrs-20260425-233000.xml`, `UnityProject/TestResults/playmode-runtime-20260425-233500.xml`).

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

# [fun] recent context, 2026-04-26 12:00am GMT+8

Legend: 🎯session 🔴bugfix 🟣feature 🔄refactor ✅change 🔵discovery ⚖️decision 🚨security_alert 🔐security_note
Format: ID TIME TYPE TITLE
Fetch details: get_observations([IDs]) | Search: mem-search skill

Stats: 50 obs (11,468t read) | 839,559t work | 99% savings

### Apr 25, 2026
1526 10:02p 🔵 Collections 模块目录结构与文件组织分析
1527 " 🔵 FastDictionary 自定义哈希表实现分析
1528 " 🔵 FastList O(1) 移除与枚举器版本安全机制
1529 " 🔵 FastPriorityQueue 二叉堆实现与自定义比较器支持
1530 " 🔵 ObjectPool 泛型对象池与 IResettable 自动重置机制
1534 10:06p 🔵 Collections 模块五大核心数据结构代码审查完成
S664 CQRS 目录代码审查启动 (Apr 25 at 10:06 PM)
1535 10:07p ✅ CQRS 目录代码审查启动
S665 Collections Module Code Review Completed (Apr 25 at 10:07 PM)
1536 " ✅ Collections Module Code Review Completed
S666 CQRS Framework Exception Handling Deep Dive - Reviewing CqrsBus error handling, QueryKey hash stability, and NullLogger availability (Apr 25 at 10:07 PM)
S669 Continue Unity Framework code review - commit CQRS Bus optimizations (Apr 25 at 10:08 PM)
1537 10:08p 🔴 CQRS _isFrozen Lacks Memory Barrier (P1 High)
1538 " 🔵 CQRS Framework Code Review Complete - APPROVED with 3 Issues
1541 10:11p 🔵 Collections Module Five Core Data Structures and Pooling Patterns Analyzed
1542 " 🔴 Collections Module ArraySegmentList Clear Method Not Implemented
1543 " 🔴 CQRS Bus Thread Safety Bug - Missing Volatile Modifier on _isFrozen
1544 " 🔄 CQRS Publish Method Exception Handling Refactored to Aggregate Exception Pattern
1545 " ✅ CQRS Namespace Migration from Fun to Change
1546 " 🔵 Timer Module Five Core Design Patterns Analyzed
1547 " ✅ Unity Framework Code Review Session Completed with Findings
1548 " ⚖️ Test Directory Structure Standardization Required
S678 Collections Module Enhancement Sprint Completed (Apr 25 at 10:15 PM)
1549 10:28p 🔵 Fun Framework Collections Module Task Planning Session
1550 10:29p ✅ ObjectPool.Return Method Now Returns bool
1551 " ✅ FastPriorityQueue Growth Metrics Instrumentation Added
1552 10:30p ✅ CollectionMetrics Extended and ClearMode Documented
1553 " 🔴 FastList.RemoveAtSwapBack Self-Assignment Guard Added
1554 10:31p 🔵 Unity FSM 框架代码审核启动
1555 10:32p 🔵 Unity FSM 框架代码审核完成
1556 " ✅ Collections Module Enhancement Sprint Completed
S681 Collections Module Enhancement Sprint - All 8 Tasks Completed and Verified (Apr 25 at 10:32 PM)
S683 FastDictionary and FastHashSet Zero-Allocation ForEach Iteration Added (Apr 25 at 10:33 PM)
1557 11:14p 🟣 Collections Module Enhancement Sprint Complete - 8 Tasks Done
1559 " 🔴 FSM Framework FsmResult.NextStateId Safety Fix
1558 " ✅ Collections Module Enhancement Sprint Committed to Git
1562 " 🟣 FastDictionary and FastHashSet Zero-Allocation ForEach Iteration Added
1563 " 🔴 FastList.RemoveAtSwapBack Self-Assignment Guard Fixed
1564 " 🟣 ObjectPool Now Implements IClearable and Returns Boolean from Return
1565 " 🟣 FastPriorityQueue Growth Metrics Instrumentation Added
1566 " ✅ ClearMode Documentation Enhanced with GC Behavior Notes
S684 Commit Collections module enhancement sprint to git (Apr 25 at 11:14 PM)
1560 11:15p 🔄 FSM Framework StateMachine Major Refactoring - All Code Review Issues Addressed
1561 " 🔴 FSM Framework Compilation Error - Missing System using Directive
S686 Evaluate if ObjectPool in Collections can be replaced by Pool in Pooling module to eliminate duplicate code (Apr 25 at 11:15 PM)
1568 11:23p 🔵 ObjectPool vs Pool Consolidation Analysis Complete
1569 " ⚖️ Unify Reset Interfaces Rather Than Replace Implementation
1570 11:24p 🔵 ObjectPool vs Pool API Comparison
1572 " 🔵 Pool Module Has Richer Safety Features Than ObjectPool
1573 " 🔵 Dual-Track Pooling Design Was Intentional
1574 11:25p ⚖️ ObjectPool Consolidation - Dual-Track Design is Intentional
1576 11:32p ✅ Commit Requested for Unity Framework Session
S694 Commit FSM module improvements: RingBuffer integration, self-transition support, and new tests (Apr 25 at 11:32 PM)
1577 " 🔄 FSM Module Refactored with RingBuffer and Self-Transition Support
1578 11:53p 🔵 ObjectPool Consolidation Brainstorming Initiated
1580 11:54p 🔵 Pool<T> Static Generic Object Pool Architecture Analyzed
1581 " 🔵 Collections ObjectPool<T> Instance Pool Design Analyzed
1582 " ⚖️ Dual-Track Pooling Architecture Decision
1583 11:55p 🔵 ObjectPool vs Pool Usage Analysis
1584 " 🔵 ObjectPool Consolidation Investigation Resumed

Access 840k tokens of past work via get_observations([IDs]) or mem-search skill.
</claude-mem-context>
