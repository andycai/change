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

## Build & Tooling Notes

- HybridCLR menu: `HybridCLR/CompileDll`, `GenerateAOTReference`, `GenerateLinkXml`, etc.
- Obfuz menu: `HybridCLR/ObfuzExtension/GenerateAll`, `CompileAndObfuscateDll`, `GeneratePolymorphicCodes`
- YooAsset: Use YooAsset editor window for asset collection and bundle building
- FairyGUI: Use FairyGUI editor for UI editing; publish to Unity project
- Luban: Configuration generation (to be set up)

## Version Control

- `.gitignore` is scoped to `UnityProject/` subdirectory
- Excluded: `Library/`, `Temp/`, `Build/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`, `Samples/`, `Bundles/`, `yoo/`
- Embedded packages (HybridCLR, FairyGUI) are versioned in `Packages/`

## Design Documents

All design specs and implementation plans live under `docs/superpowers/`:
- `specs/` - Approved design baselines
- `plans/` - Detailed implementation plans

When implementing framework modules, refer to the corresponding spec first. Specs define contracts, constraints, and acceptance criteria. Plans translate specs into implementation steps.
