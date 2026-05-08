# Game Flow Control With Main/Sub FSM Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build CQRS-driven game flow control in `GameScript` using a main FSM and battle sub-FSM, plus a minimum runnable Unity demo driver.

**Architecture:** Keep `Change.Framework.Fsm` as the reusable kernel and implement business flow models entirely in `GameScript`. Route CQRS results through a single `GameFlowOrchestrator` that is the only component allowed to dispatch FSM events. Use a host state in the main FSM to own battle sub-FSM lifecycle.

**Tech Stack:** Unity 2022.3, C#, Change.Framework.Fsm, Change.Framework.Cqrs, Unity Test Framework (EditMode)

---

## File Structure (create/modify map)

### Create

- `UnityProject/Assets/GameScript/GameFlow/GameFlowStateId.cs` - Main flow state IDs
- `UnityProject/Assets/GameScript/GameFlow/GameFlowEvent.cs` - Main flow event IDs
- `UnityProject/Assets/GameScript/GameFlow/BattleFlow/BattleFlowStateId.cs` - Battle flow state IDs
- `UnityProject/Assets/GameScript/GameFlow/BattleFlow/BattleFlowEvent.cs` - Battle flow event IDs
- `UnityProject/Assets/GameScript/GameFlow/Orchestration/GameFlowContext.cs` - Shared flow context data
- `UnityProject/Assets/GameScript/GameFlow/Orchestration/GameFlowOrchestrator.cs` - CQRS result to FSM event bridge
- `UnityProject/Assets/GameScript/GameFlow/MainGameFlowMachineFactory.cs` - Main FSM assembly/bootstrap
- `UnityProject/Assets/GameScript/GameFlow/BattleFlow/BattleFlowMachineFactory.cs` - Battle FSM assembly/bootstrap
- `UnityProject/Assets/GameScript/GameFlow/States/*.cs` - Main flow state implementations
- `UnityProject/Assets/GameScript/GameFlow/BattleFlow/States/*.cs` - Battle flow state implementations
- `UnityProject/Assets/GameScript/GameFlow/Entry/GameFlowDemoDriver.cs` - Minimal runnable driver
- `UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/GameFlowOrchestratorTests.cs`
- `UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/MainGameFlowStateMachineTests.cs`
- `UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/BattleFlowStateMachineTests.cs`
- `UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/BattleFlowHostStateTests.cs`

### Modify

- `UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef` (if needed, include new `GameScript` assembly reference strategy already used in project)
- `UnityProject/Assets/GameScript/` existing asmdef (if needed) to include new folders in compilation scope

---

### Task 1: Define flow contracts (state IDs, events, context)

**Files:**
- Create: `UnityProject/Assets/GameScript/GameFlow/GameFlowStateId.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/GameFlowEvent.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/BattleFlow/BattleFlowStateId.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/BattleFlow/BattleFlowEvent.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/Orchestration/GameFlowContext.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/MainGameFlowStateMachineTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;

namespace Change.Runtime.Tests.EditMode.GameFlow
{
    public class MainGameFlowStateMachineTests
    {
        [Test]
        public void GameFlowStateId_ShouldContainExpectedMainStates()
        {
            Assert.That((int)GameScript.GameFlow.GameFlowStateId.Boot, Is.EqualTo(0));
            Assert.That((int)GameScript.GameFlow.GameFlowStateId.Result, Is.GreaterThan(0));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:  
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -projectPath "UnityProject" -batchmode -runTests -testPlatform EditMode -testFilter "Change.Runtime.Tests.EditMode.GameFlow.MainGameFlowStateMachineTests.GameFlowStateId_ShouldContainExpectedMainStates" -testResults "UnityProject/TestResults/editmode-gameflow-contracts-20260507-213500.xml"`

Expected: FAIL with missing namespace/type errors for `GameScript.GameFlow.*`.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace GameScript.GameFlow
{
    public enum GameFlowStateId
    {
        Boot = 0,
        Login = 1,
        Lobby = 2,
        Match = 3,
        Battle = 4,
        Result = 5
    }

    public enum GameFlowEvent
    {
        BootstrapCompleted = 0,
        LoginSucceeded = 1,
        MatchRequested = 2,
        MatchFound = 3,
        BattleFinished = 4,
        ConfirmResult = 5
    }
}
```

```csharp
namespace GameScript.GameFlow.BattleFlow
{
    public enum BattleFlowStateId
    {
        Loading = 0,
        Ready = 1,
        Playing = 2,
        Paused = 3,
        Settlement = 4,
        Exit = 5
    }

    public enum BattleFlowEvent
    {
        SceneLoaded = 0,
        CountdownFinished = 1,
        PauseRequested = 2,
        ResumeRequested = 3,
        BattleTimeUp = 4,
        WinLoseResolved = 5,
        SettlementConfirmed = 6
    }
}
```

```csharp
namespace GameScript.GameFlow.Orchestration
{
    public sealed class GameFlowContext
    {
        public int PlayerId { get; set; }
        public int MatchId { get; set; }
        public bool IsBattleActive { get; set; }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run same command as Step 2.  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add "UnityProject/Assets/GameScript/GameFlow/GameFlowStateId.cs" \
        "UnityProject/Assets/GameScript/GameFlow/GameFlowEvent.cs" \
        "UnityProject/Assets/GameScript/GameFlow/BattleFlow/BattleFlowStateId.cs" \
        "UnityProject/Assets/GameScript/GameFlow/BattleFlow/BattleFlowEvent.cs" \
        "UnityProject/Assets/GameScript/GameFlow/Orchestration/GameFlowContext.cs" \
        "UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/MainGameFlowStateMachineTests.cs"
git commit -m "feat(game-flow): add main and battle flow contracts"
```

### Task 2: Implement battle sub-FSM states and factory

**Files:**
- Create: `UnityProject/Assets/GameScript/GameFlow/BattleFlow/States/BattleLoadingState.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/BattleFlow/States/BattleReadyState.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/BattleFlow/States/BattlePlayingState.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/BattleFlow/States/BattlePausedState.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/BattleFlow/States/BattleSettlementState.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/BattleFlow/States/BattleExitState.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/BattleFlow/BattleFlowMachineFactory.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/BattleFlowStateMachineTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Change.Framework.Fsm;
using NUnit.Framework;

namespace Change.Runtime.Tests.EditMode.GameFlow
{
    public class BattleFlowStateMachineTests
    {
        [Test]
        public void BattleFlow_ShouldReachExit_FromSettlementConfirmed()
        {
            var machine = GameScript.GameFlow.BattleFlow.BattleFlowMachineFactory.Create();
            machine.Start(GameScript.GameFlow.BattleFlow.BattleFlowStateId.Loading);
            machine.Fire(GameScript.GameFlow.BattleFlow.BattleFlowEvent.SceneLoaded);
            machine.Fire(GameScript.GameFlow.BattleFlow.BattleFlowEvent.CountdownFinished);
            machine.Fire(GameScript.GameFlow.BattleFlow.BattleFlowEvent.WinLoseResolved);
            machine.Fire(GameScript.GameFlow.BattleFlow.BattleFlowEvent.SettlementConfirmed);
            Assert.That(machine.CurrentStateId, Is.EqualTo(GameScript.GameFlow.BattleFlow.BattleFlowStateId.Exit));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:  
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -projectPath "UnityProject" -batchmode -runTests -testPlatform EditMode -testFilter "Change.Runtime.Tests.EditMode.GameFlow.BattleFlowStateMachineTests.BattleFlow_ShouldReachExit_FromSettlementConfirmed" -testResults "UnityProject/TestResults/editmode-battleflow-20260507-214000.xml"`

Expected: FAIL because `BattleFlowMachineFactory` and states do not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using Change.Framework.Fsm;

namespace GameScript.GameFlow.BattleFlow
{
    public static class BattleFlowMachineFactory
    {
        public static StateMachine<BattleFlowStateId, BattleFlowEvent> Create()
        {
            var machine = new StateMachine<BattleFlowStateId, BattleFlowEvent>();
            machine.Register(new States.BattleLoadingState());
            machine.Register(new States.BattleReadyState());
            machine.Register(new States.BattlePlayingState());
            machine.Register(new States.BattlePausedState());
            machine.Register(new States.BattleSettlementState());
            machine.Register(new States.BattleExitState());
            return machine;
        }
    }
}
```

```csharp
using Change.Framework.Fsm;

namespace GameScript.GameFlow.BattleFlow.States
{
    public sealed class BattleLoadingState : IFsmState<BattleFlowStateId, BattleFlowEvent>
    {
        public BattleFlowStateId Id => BattleFlowStateId.Loading;
        public void OnEnter(in StateChange<BattleFlowStateId, BattleFlowEvent> change) { }
        public void OnExit(in StateChange<BattleFlowStateId, BattleFlowEvent> change) { }
        public FsmResult<BattleFlowStateId> OnEvent(in BattleFlowEvent evt) =>
            evt == BattleFlowEvent.SceneLoaded
                ? FsmResult<BattleFlowStateId>.TransitionTo(BattleFlowStateId.Ready)
                : FsmResult<BattleFlowStateId>.Ignored();
    }
}
```

```csharp
// Implement remaining states with explicit transition mapping:
// Ready -> Playing on CountdownFinished
// Playing -> Paused on PauseRequested
// Paused -> Playing on ResumeRequested
// Playing -> Settlement on BattleTimeUp or WinLoseResolved
// Settlement -> Exit on SettlementConfirmed
// Exit ignores all events
```

- [ ] **Step 4: Run test to verify it passes**

Run command from Step 2.  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add "UnityProject/Assets/GameScript/GameFlow/BattleFlow/" \
        "UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/BattleFlowStateMachineTests.cs"
git commit -m "feat(game-flow): add battle sub-flow state machine"
```

### Task 3: Implement main FSM states with battle host state

**Files:**
- Create: `UnityProject/Assets/GameScript/GameFlow/States/BootState.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/States/LoginState.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/States/LobbyState.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/States/MatchState.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/States/BattleHostState.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/States/ResultState.cs`
- Create: `UnityProject/Assets/GameScript/GameFlow/MainGameFlowMachineFactory.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/MainGameFlowStateMachineTests.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/BattleFlowHostStateTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Test]
public void MainFlow_ShouldReachResult_AfterBattleFinished()
{
    var machine = GameScript.GameFlow.MainGameFlowMachineFactory.Create(new GameScript.GameFlow.Orchestration.GameFlowContext());
    machine.Start(GameScript.GameFlow.GameFlowStateId.Boot);
    machine.Fire(GameScript.GameFlow.GameFlowEvent.BootstrapCompleted);
    machine.Fire(GameScript.GameFlow.GameFlowEvent.LoginSucceeded);
    machine.Fire(GameScript.GameFlow.GameFlowEvent.MatchRequested);
    machine.Fire(GameScript.GameFlow.GameFlowEvent.MatchFound);
    machine.Fire(GameScript.GameFlow.GameFlowEvent.BattleFinished);
    Assert.That(machine.CurrentStateId, Is.EqualTo(GameScript.GameFlow.GameFlowStateId.Result));
}
```

```csharp
[Test]
public void BattleHostState_ShouldStartBattleMachine_OnEnter()
{
    var context = new GameScript.GameFlow.Orchestration.GameFlowContext();
    var state = new GameScript.GameFlow.States.BattleHostState(context);
    // constructing and calling OnEnter should initialize battle flag
    state.OnEnter(in Change.Framework.Fsm.StateChange<GameScript.GameFlow.GameFlowStateId, GameScript.GameFlow.GameFlowEvent>.Initial(GameScript.GameFlow.GameFlowStateId.Battle, 1));
    Assert.That(context.IsBattleActive, Is.True);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:  
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -projectPath "UnityProject" -batchmode -runTests -testPlatform EditMode -testFilter "Change.Runtime.Tests.EditMode.GameFlow.MainGameFlowStateMachineTests.MainFlow_ShouldReachResult_AfterBattleFinished|Change.Runtime.Tests.EditMode.GameFlow.BattleFlowHostStateTests.BattleHostState_ShouldStartBattleMachine_OnEnter" -testResults "UnityProject/TestResults/editmode-mainflow-20260507-214500.xml"`

Expected: FAIL with missing factory/state types.

- [ ] **Step 3: Write minimal implementation**

```csharp
using Change.Framework.Fsm;
using GameScript.GameFlow.Orchestration;

namespace GameScript.GameFlow
{
    public static class MainGameFlowMachineFactory
    {
        public static StateMachine<GameFlowStateId, GameFlowEvent> Create(GameFlowContext context)
        {
            var machine = new StateMachine<GameFlowStateId, GameFlowEvent>();
            machine.Register(new States.BootState());
            machine.Register(new States.LoginState());
            machine.Register(new States.LobbyState());
            machine.Register(new States.MatchState());
            machine.Register(new States.BattleHostState(context));
            machine.Register(new States.ResultState());
            return machine;
        }
    }
}
```

```csharp
// Boot/Login/Lobby/Match/Result states:
// map exactly one happy-path event each to next state, otherwise Ignored.
```

```csharp
using Change.Framework.Fsm;
using GameScript.GameFlow.BattleFlow;
using GameScript.GameFlow.Orchestration;

namespace GameScript.GameFlow.States
{
    public sealed class BattleHostState : IFsmState<GameFlowStateId, GameFlowEvent>
    {
        private readonly GameFlowContext _context;
        private StateMachine<BattleFlowStateId, BattleFlowEvent> _battleMachine;

        public BattleHostState(GameFlowContext context) => _context = context;
        public GameFlowStateId Id => GameFlowStateId.Battle;

        public void OnEnter(in StateChange<GameFlowStateId, GameFlowEvent> change)
        {
            _battleMachine = BattleFlowMachineFactory.Create();
            _battleMachine.Start(BattleFlowStateId.Loading);
            _context.IsBattleActive = true;
        }

        public void OnExit(in StateChange<GameFlowStateId, GameFlowEvent> change)
        {
            _context.IsBattleActive = false;
        }

        public FsmResult<GameFlowStateId> OnEvent(in GameFlowEvent evt) =>
            evt == GameFlowEvent.BattleFinished
                ? FsmResult<GameFlowStateId>.TransitionTo(GameFlowStateId.Result)
                : FsmResult<GameFlowStateId>.Ignored();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run command from Step 2.  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add "UnityProject/Assets/GameScript/GameFlow/States/" \
        "UnityProject/Assets/GameScript/GameFlow/MainGameFlowMachineFactory.cs" \
        "UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/MainGameFlowStateMachineTests.cs" \
        "UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/BattleFlowHostStateTests.cs"
git commit -m "feat(game-flow): add main flow states and battle host state"
```

### Task 4: Implement CQRS -> FSM orchestrator and dedup guards

**Files:**
- Create: `UnityProject/Assets/GameScript/GameFlow/Orchestration/GameFlowOrchestrator.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/GameFlowOrchestratorTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;

namespace Change.Runtime.Tests.EditMode.GameFlow
{
    public class GameFlowOrchestratorTests
    {
        [Test]
        public void Orchestrator_ShouldIgnoreDuplicateMatchFound()
        {
            var sut = GameScript.GameFlow.Orchestration.GameFlowOrchestrator.CreateForTests();
            sut.OnMatchFound(101);
            sut.OnMatchFound(101);
            Assert.That(sut.MainStateId, Is.EqualTo(GameScript.GameFlow.GameFlowStateId.Battle));
            Assert.That(sut.ProcessedMatchFoundCount, Is.EqualTo(1));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:  
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -projectPath "UnityProject" -batchmode -runTests -testPlatform EditMode -testFilter "Change.Runtime.Tests.EditMode.GameFlow.GameFlowOrchestratorTests.Orchestrator_ShouldIgnoreDuplicateMatchFound" -testResults "UnityProject/TestResults/editmode-orchestrator-20260507-215000.xml"`

Expected: FAIL with missing orchestrator APIs.

- [ ] **Step 3: Write minimal implementation**

```csharp
using Change.Framework.Fsm;
using GameScript.GameFlow.BattleFlow;

namespace GameScript.GameFlow.Orchestration
{
    public sealed class GameFlowOrchestrator
    {
        private readonly GameFlowContext _context;
        private readonly StateMachine<GameFlowStateId, GameFlowEvent> _mainMachine;
        private int _lastMatchId = -1;
        private int _processedMatchFoundCount;

        public static GameFlowOrchestrator CreateForTests()
        {
            var context = new GameFlowContext();
            var orchestrator = new GameFlowOrchestrator(context);
            orchestrator.Start();
            orchestrator.OnBootstrapCompleted();
            orchestrator.OnLoginSucceeded(playerId: 1);
            orchestrator.OnMatchRequested();
            return orchestrator;
        }

        public GameFlowOrchestrator(GameFlowContext context)
        {
            _context = context;
            _mainMachine = MainGameFlowMachineFactory.Create(context);
        }

        public GameFlowStateId MainStateId => _mainMachine.CurrentStateId;
        public int ProcessedMatchFoundCount => _processedMatchFoundCount;

        public void Start() => _mainMachine.Start(GameFlowStateId.Boot);
        public void OnBootstrapCompleted() => FireMain(GameFlowEvent.BootstrapCompleted);
        public void OnLoginSucceeded(int playerId) { _context.PlayerId = playerId; FireMain(GameFlowEvent.LoginSucceeded); }
        public void OnMatchRequested() => FireMain(GameFlowEvent.MatchRequested);

        public void OnMatchFound(int matchId)
        {
            if (_lastMatchId == matchId) return;
            _lastMatchId = matchId;
            _context.MatchId = matchId;
            _processedMatchFoundCount++;
            FireMain(GameFlowEvent.MatchFound);
        }

        public void OnBattleSettlementConfirmed() => FireMain(GameFlowEvent.BattleFinished);
        public void OnResultConfirmed() => FireMain(GameFlowEvent.ConfirmResult);

        private void FireMain(GameFlowEvent evt)
        {
            if (!_mainMachine.IsStarted || _mainMachine.IsFaulted) return;
            _mainMachine.Fire(evt);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run command from Step 2.  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add "UnityProject/Assets/GameScript/GameFlow/Orchestration/GameFlowOrchestrator.cs" \
        "UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/GameFlowOrchestratorTests.cs"
git commit -m "feat(game-flow): add cqrs to fsm orchestrator with dedup guards"
```

### Task 5: Add minimum runnable Unity demo driver

**Files:**
- Create: `UnityProject/Assets/GameScript/GameFlow/Entry/GameFlowDemoDriver.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/MainGameFlowStateMachineTests.cs` (smoke-level behavior assertions for driver methods)

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;

namespace Change.Runtime.Tests.EditMode.GameFlow
{
    public class GameFlowDemoDriverTests
    {
        [Test]
        public void DemoDriver_ShouldAdvanceToBattle_WhenCallingDemoSequence()
        {
            var driver = new GameScript.GameFlow.Entry.GameFlowDemoDriver();
            driver.InitializeForTests();
            driver.SimulateToBattle();
            Assert.That(driver.CurrentMainState, Is.EqualTo(GameScript.GameFlow.GameFlowStateId.Battle));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:  
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -projectPath "UnityProject" -batchmode -runTests -testPlatform EditMode -testFilter "Change.Runtime.Tests.EditMode.GameFlow.GameFlowDemoDriverTests.DemoDriver_ShouldAdvanceToBattle_WhenCallingDemoSequence" -testResults "UnityProject/TestResults/editmode-demo-driver-20260507-215500.xml"`

Expected: FAIL due to missing driver class/methods.

- [ ] **Step 3: Write minimal implementation**

```csharp
using UnityEngine;
using GameScript.GameFlow.Orchestration;

namespace GameScript.GameFlow.Entry
{
    public sealed class GameFlowDemoDriver : MonoBehaviour
    {
        private GameFlowOrchestrator _orchestrator;

        public GameFlowStateId CurrentMainState => _orchestrator.MainStateId;

        private void Start() => Initialize();

        public void InitializeForTests() => Initialize();

        public void SimulateToBattle()
        {
            _orchestrator.OnBootstrapCompleted();
            _orchestrator.OnLoginSucceeded(1);
            _orchestrator.OnMatchRequested();
            _orchestrator.OnMatchFound(101);
            Debug.Log($"MainFlow={_orchestrator.MainStateId}");
        }

        private void Initialize()
        {
            _orchestrator = new GameFlowOrchestrator(new GameFlowContext());
            _orchestrator.Start();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) _orchestrator.OnLoginSucceeded(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) _orchestrator.OnMatchRequested();
            if (Input.GetKeyDown(KeyCode.Alpha3)) _orchestrator.OnMatchFound(101);
            if (Input.GetKeyDown(KeyCode.Alpha4)) _orchestrator.OnBattleSettlementConfirmed();
            if (Input.GetKeyDown(KeyCode.Alpha5)) _orchestrator.OnResultConfirmed();
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run command from Step 2.  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add "UnityProject/Assets/GameScript/GameFlow/Entry/GameFlowDemoDriver.cs" \
        "UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/GameFlowDemoDriverTests.cs"
git commit -m "feat(game-flow): add runnable game flow demo driver"
```

### Task 6: Full verification and documentation sync

**Files:**
- Modify: `docs/superpowers/plans/2026-05-07-game-flow-control-with-fsm-implementation-plan.md` (checklist status updates only during execution)
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow/*.cs`

- [ ] **Step 1: Run full GameFlow EditMode test suite**

Run:  
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -projectPath "UnityProject" -batchmode -runTests -testPlatform EditMode -testFilter "Change.Runtime.Tests.EditMode.GameFlow" -testResults "UnityProject/TestResults/editmode-gameflow-full-20260507-220000.xml"`

Expected: PASS all game flow tests.

- [ ] **Step 2: Run broader Runtime EditMode regression**

Run:  
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -projectPath "UnityProject" -batchmode -runTests -testPlatform EditMode -testFilter "Change.Runtime.Tests.EditMode" -testResults "UnityProject/TestResults/editmode-runtime-regression-20260507-220500.xml"`

Expected: PASS or only pre-existing unrelated failures.

- [ ] **Step 3: Validate no handler directly dispatches FSM**

Run:  
`rg "Fire\\(" "UnityProject/Assets/GameScript/GameFlow" -n`

Expected: `Fire(` appears only in orchestrator/factory/test/demo paths, not CQRS handlers.

- [ ] **Step 4: Final quality checks**

Run:  
`git status --short && git diff --stat`

Expected: clean understanding of final diff, no accidental unrelated edits.

- [ ] **Step 5: Commit verification artifacts**

```bash
git add "UnityProject/Assets/GameScript/GameFlow" \
        "UnityProject/Assets/Change/Runtime/Tests/EditMode/GameFlow"
git commit -m "test(game-flow): verify orchestrated main and battle flow integration"
```

---

## Self-Review

- Spec coverage check: main/sub FSM, orchestrator-only dispatch, dedup/fault guards, EditMode tests, and runnable demo are all represented by Tasks 1-6.
- Placeholder scan: no TODO/TBD markers; each task contains concrete file paths, commands, and expected outcomes.
- Type consistency: uses consistent type names across tasks (`GameFlowStateId`, `GameFlowEvent`, `BattleFlowStateId`, `BattleFlowEvent`, `GameFlowOrchestrator`, `GameFlowContext`).

