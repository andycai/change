# Fun - Unity Game Project

## Project Overview

Fun is a Unity mobile game project built with a hot-update architecture centered on HybridCLR. The project follows a layered framework design pattern with business-agnostic reusable infrastructure under `Fun/Framework`.

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
│       │   ├── *-fsm-design.md
│       │   ├── *-cqrs-design.md
│       │   └── *-high-performance-collections-design.md
│       └── plans/               # Implementation plans
│           ├── *-fsm-implementation.md
│           └── *-cqrs.md
└── UnityProject/                # Unity project root
    ├── Assets/
    │   ├── Fun/                 # Main game code
    │   │   ├── Framework/       # Business-agnostic framework (FSM, CQRS, etc.)
    │   │   ├── Runtime/         # Game runtime code
    │   │   └── Editor/          # Editor tools & extensions
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

### Framework Layer (`Fun/Framework`)

Business-agnostic reusable infrastructure. Key principles:

1. **Zero external dependencies** - Framework code must not depend on third-party libraries.
2. **Business-agnostic** - No domain coupling; framework never depends on business assemblies.
3. **Hot-path 0GC** - Runtime dispatch paths must produce zero managed allocations after warm-up.
4. **Fail-fast** - Misconfiguration (missing registration, duplicate registration, post-freeze modification) throws immediately.
5. **Synchronous execution model** - MVP scope is sync-only; no async/await in framework core.
6. **Explicit registration** - No reflection-based auto-scan; handlers registered manually during bootstrap, then `Freeze()`.
7. **Struct messages** - Command/Query/Event types should be `readonly struct` to avoid boxing and allocation.

### Designed Framework Modules

- **FSM** (`Fun/Framework/Fsm`) - Event-driven finite state machine with `IFsmState<TStateId, TEvent>`, single active state, FIFO event queue, serial processing. Spec: `docs/superpowers/specs/*-fsm-design.md`
- **CQRS** (`Fun/Framework/Cqrs`) - Command/Query/Responsibility Segregation with struct messages, class handlers, generic strongly typed dispatch. Spec: `docs/superpowers/specs/*-cqrs-design.md`
- **High-Performance Collections** - Spec: `docs/superpowers/specs/*-high-performance-collections-design.md`

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
- **Assembly definitions**: Use `.asmdef` files; framework assemblies under `Fun/Framework/`
- **Namespace convention**: `Fun.Framework.{Module}` for framework code
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

# [fun] recent context, 2026-04-24 5:30pm GMT+8

Legend: 🎯session 🔴bugfix 🟣feature 🔄refactor ✅change 🔵discovery ⚖️decision 🚨security_alert 🔐security_note
Format: ID TIME TYPE TITLE
Fetch details: get_observations([IDs]) | Search: mem-search skill

Stats: 50 obs (11,040t read) | 915,054t work | 99% savings

### Apr 23, 2026
S60 Unity Timer System: Simplicity-First Architecture (Apr 23 at 10:55 PM)
S63 Timer Implementation Plan Created with 5 Tasks (Apr 23 at 10:56 PM)
S66 Unity Timer System: Implementation Planning (Apr 23 at 10:56 PM)
S77 Unity Timer System: All 5 Tasks Complete (Apr 23 at 10:57 PM)
S79 Unity 生产级定时器系统实现 - Simplicity-First 架构 (Apr 23 at 11:07 PM)
S340 Fun.Framework Cqrs 代码审查启动 (Apr 23 at 11:07 PM)
### Apr 24, 2026
S341 Fun.Framework Cqrs 模块代码审查完成 (Apr 24 at 1:25 PM)
S385 Unity .meta Files Git Commit Initiated (Apr 24 at 1:25 PM)
S386 Fix graphify-out/ directory being generated inside UnityProject instead of only at project root (Apr 24 at 3:41 PM)
S387 Commit missing Unity .meta files for Framework and Timer modules (Apr 24 at 3:42 PM)
1233 4:17p 🟣 Fun Framework Logging System MVP Implemented via TDD
1234 4:18p ✅ Fun Framework Logging MVP Task 3 Complete, Task 4 In Progress
1238 4:20p ✅ Fun Framework README Documented Logging Module
1239 " 🔵 Logging Regression Tests All Pass
1241 4:22p ✅ Graphify Code Graph Refreshed with 35094 Nodes
1242 " ✅ Fun Framework Logging MVP Documentation Committed
1244 4:23p 🟣 Fun Framework Logging MVP Task 4 Documentation Complete
1245 4:24p 🟣 Fun Framework Logging System MVP Implemented for Engine-Agnostic Use
1246 " 🟣 Fun Framework Logging MVP Fully Verified - All 4 Tasks Complete
1247 " ✅ Fun Framework Logging MVP Complete - Task 4 Documentation and Verification
1251 4:28p 🔵 Graphify Manifest Contains Machine-Local PackageCache Entries
1252 " ✅ Graphify Manifest Artifact Removed from Source Control
1253 4:29p ✅ Graphify manifest.json Removed from Repository
1258 4:30p ✅ Graphify Manifest Artifact Removed as Unstable
1264 4:32p 🟣 Fun Framework Logging System MVP Implemented via TDD
1265 " ⚖️ Fun Framework Logging CQRS Architecture Decision
1266 " 🔵 Unity Batchmode Test Execution Requires EditorLock Workaround
1267 " 🔵 Graphify Manifest.json Missing from Task 4 Deliverables
1269 4:34p ✅ Task 4 Verification Passed After Manifest Fix
1270 " 🟣 Fun Framework Logging MVP Complete - All Tasks Verified
1271 4:35p 🔵 CQRS Migration Fail-First Compile Error Proof Captured
1272 " 🟣 CQRS Logger Abstraction Migration Approved
1273 4:36p 🔵 Graphify Manifest Structure Verified as Aligned with Graph Scope
1274 4:37p 🟣 Engine-Agnostic Logging System Brainstorming Initiated
1275 " ⚖️ Fun Framework Logging MVP Design Finalized with CQRS Infrastructure
1276 " ⚖️ LogRouter Uses Register-Then-Freeze Lifecycle
1277 " 🟣 Sink Exception Isolation Ensures Resilient Logging
1278 " 🔵 Unity Batchmode Tests Require EditorLock Workaround
1279 " 🔄 CqrsBus Migrated to Shared Framework Logger
1280 " 🟣 Fun Framework Logging MVP Implemented via TDD
1281 " ✅ Framework README Documented with Logging Module
1282 4:38p 🟣 Fun Framework Logging MVP Final Verification Passed
1283 " 🔵 Graph Manifest Verified with Consistent Metadata
1284 " 🔵 CQRS Logger Types Fully Removed from Codebase
1291 4:39p 🔵 All Framework Tests Pass - 73 Total Test Coverage
1292 4:40p 🟣 Fun Framework Logging MVP Final Review READY
1293 " 🟣 Fun Framework Logging Implementation Plan Complete
1300 4:44p 🔄 Graphify Commits Squashed into Single Commit
1304 4:45p 🟣 Fun Framework Logging Branch Merged into Main
1305 4:46p 🔴 Orphaned .meta Files Remain After CQRS Logger Removal
1307 4:59p ✅ feat/framework-logging Branch and Worktree Cleaned Up
1309 5:01p ✅ Timer Module Committed: Runtime Hardening + PlayMode Test Suite
1311 5:12p ⚖️ Fun Framework Logging MVP Design Finalized
1313 5:14p 🟣 Fun.Runtime.Logging Sinks Implemented
1314 5:15p 🟣 Fun.Runtime.Logging Module Created with Unity and File Sinks
1315 " 🔵 Fun.Runtime.Logging Files Created on Main Branch
1317 5:16p 🔵 Unity Editor Running with Current Project Open
1318 5:17p ⚖️ Fun Framework Logging Uses Runtime Sink Injection Pattern
1326 5:20p 🔵 Unity Batchmode Test Results XML Not Generated
1327 5:21p 🔴 Fun.Runtime.Logging Tests All Passed

Access 915k tokens of past work via get_observations([IDs]) or mem-search skill.
</claude-mem-context>
