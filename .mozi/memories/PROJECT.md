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
