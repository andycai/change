# Game Flow Control Design (FSM + CQRS Orchestration)

## 1. Context and Goal

This design defines how to implement game flow control by reusing `Change.Framework.Fsm` in `UnityProject/Assets/Change/Framework/Fsm/`, while placing business implementation scripts under `UnityProject/Assets/GameScript/`.

Confirmed scope:

- Two-layer flow control:
  - Main flow FSM for macro game lifecycle
  - Battle sub-flow FSM for in-battle lifecycle
- Event source is CQRS-driven
- Battle sub-flow implementation stays in `GameScript` (business layer)
- Delivery depth is core code + minimum runnable Unity demo driver
- CQRS-to-FSM bridging uses a single `GameFlowOrchestrator`

## 2. Non-Goals

- No changes to `Change.Framework.Fsm` core implementation
- No migration of existing unrelated runtime startup pipeline
- No framework-level generic game-flow abstraction in `Change.Framework`

## 3. Architectural Decision

Recommended and approved approach: **Main/Sub FSM + Single Orchestrator Bridge**.

- Main FSM owns macro states: `Boot -> Login -> Lobby -> Match -> Battle -> Result`.
- Battle FSM owns local battle states: `Loading -> Ready -> Playing -> Paused -> Settlement -> Exit`.
- CQRS handlers do not call FSM directly.
- `GameFlowOrchestrator` is the only bridge from CQRS outputs to FSM events.

Rationale:

- Keeps framework/business boundary clean
- Limits coupling between CQRS handlers and state machine internals
- Provides a testable and explicit orchestration point
- Keeps implementation complexity within current delivery target

## 4. Component Boundaries

All new implementation scripts are under `UnityProject/Assets/GameScript/`.

Proposed structure:

- `GameScript/GameFlow/`
  - `GameFlowStateId.cs`
  - `GameFlowEvent.cs`
  - `States/` (main-flow states implementing `IFsmState<GameFlowStateId, GameFlowEvent>`)
  - `MainGameFlowMachineFactory.cs` (state registration and start bootstrap)
- `GameScript/GameFlow/BattleFlow/`
  - `BattleFlowStateId.cs`
  - `BattleFlowEvent.cs`
  - `States/` (battle-flow states)
  - `BattleFlowMachineFactory.cs`
- `GameScript/GameFlow/Orchestration/`
  - `GameFlowOrchestrator.cs` (single CQRS->FSM bridge)
  - `GameFlowContext.cs` (flow-scoped context data)
- `GameScript/GameFlow/Cqrs/`
  - Commands/queries and handlers returning business results
- `GameScript/GameFlow/Entry/`
  - `GameFlowDemoDriver.cs` (`MonoBehaviour`, minimum runnable demo)

Boundary rules:

- `Change.Framework.Fsm` is treated as kernel only.
- Battle states/events remain in `GameScript`, not in framework assemblies.
- Only `GameFlowOrchestrator` can translate CQRS results into FSM events.

## 5. Data Flow

One-way flow:

1. External input reaches CQRS command/query/use-case.
2. Handler returns business result DTO.
3. `GameFlowOrchestrator` maps DTO to `GameFlowEvent` or `BattleFlowEvent`.
4. Target FSM consumes event and may transition.
5. State changes are surfaced through `OnStateChanged` for logging/diagnostics/UI sync.

No direct handler-to-state-machine invocation is allowed.

## 6. Transition Rules

### 6.1 Main Flow FSM

- `Boot` --`BootstrapCompleted`--> `Login`
- `Login` --`LoginSucceeded`--> `Lobby`
- `Lobby` --`MatchRequested`--> `Match`
- `Match` --`MatchFound`--> `Battle`
- `Battle` --`BattleFinished`--> `Result`
- `Result` --`ConfirmResult`--> `Lobby`

### 6.2 Battle Sub-Flow FSM

- `Loading` --`SceneLoaded`--> `Ready`
- `Ready` --`CountdownFinished`--> `Playing`
- `Playing` --`PauseRequested`--> `Paused`
- `Paused` --`ResumeRequested`--> `Playing`
- `Playing` --`BattleTimeUp` or `WinLoseResolved`--> `Settlement`
- `Settlement` --`SettlementConfirmed`--> `Exit`

### 6.3 Main/Sub Coordination

- Battle events are accepted only when main flow is in `Battle`.
- Entering main `Battle` creates/starts battle FSM.
- Battle `Exit` is routed to orchestrator, then mapped to main `BattleFinished`.
- Sub-FSM must not directly mutate main FSM state.

## 7. Error Handling and Guardrails

- Preserve fail-fast behavior from `StateMachine<TStateId, TEvent>`.
- `GameFlowOrchestrator` validates `IsStarted`/`IsFaulted` before dispatch.
- When faulted:
  - stop event dispatch
  - record unified error log
  - enter a defined recovery branch (project-level policy, e.g. safe fallback)
- Duplicate CQRS outputs (e.g. repeated `MatchFound`) are deduplicated in orchestrator.
- Battle-event dispatch outside main `Battle` is rejected.

## 8. Testing Strategy

Primary target: EditMode tests.

- `GameFlowOrchestratorTests`
  - CQRS result -> FSM event mapping
  - invalid ordering rejection
  - deduplication behavior
- `MainGameFlowStateMachineTests`
  - main happy path transitions
  - duplicate event idempotency
  - no dispatch after faulted state
- `BattleFlowStateMachineTests`
  - battle happy path transitions
  - pause/resume loop stability
  - invalid event remains handled/ignored without transition
- `BattleFlowHostStateTests`
  - battle FSM lifecycle on enter/exit
  - no direct cross-FSM transition bypassing orchestrator

## 9. Minimum Runnable Demo

Implement `GameFlowDemoDriver : MonoBehaviour` for an empty scene:

- Build CQRS mock + orchestrator + main FSM in `Start()`
- Provide simple trigger surface (keys/buttons) for:
  - login success
  - match found
  - countdown finished
  - battle settlement
- Print current main and battle states to Unity console
- Demonstrate complete end-to-end transition chain

## 10. Definition of Done

- Main and battle flow FSM scripts implemented under `GameScript`.
- CQRS-driven orchestrator bridge implemented with single-entry mapping logic.
- Minimum demo driver runs and shows both flow layers transitioning.
- Added EditMode tests covering happy path, idempotency, and fault behavior.
- No direct CQRS handler -> state-machine dispatch calls.
