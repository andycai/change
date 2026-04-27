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
