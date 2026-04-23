# Framework FSM Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 `UnityProject/Assets/Fun/Framework/Fsm` 落地一套业务无关、纯事件驱动、可测试的简化生产级 FSM 基础库。

**Architecture:** 采用强类型泛型内核 `StateMachine<TStateId, TEvent>` + 状态接口 `IFsmState<TStateId, TEvent>`。状态机内部使用事件队列与切换队列串行化执行，确保重入安全与顺序一致性；通过 `FsmResult<TStateId>` 表达状态处理结果，避免状态直接耦合状态机实现。

**Tech Stack:** C#（Unity Runtime）、Unity Test Framework（EditMode + NUnit）、Unity asmdef。

---

## File Structure

- Create: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/Fun.Framework.Fsm.asmdef`
  - FSM Runtime 程序集定义，纯 C# 无 UnityEngine 依赖。
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/IFsmState.cs`
  - 状态接口契约。
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/FsmResult.cs`
  - 事件处理结果值对象。
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateChange.cs`
  - 状态切换上下文值对象。
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs`
  - 状态机核心运行时实现。
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/Fun.Framework.Fsm.Tests.asmdef`
  - EditMode 测试程序集定义。
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/FsmTestTypes.cs`
  - 测试专用状态 ID、事件和录制状态类。
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineStartTests.cs`
  - 启动与未启动行为测试。
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineRegistrationTests.cs`
  - 注册与非法启动路径测试。
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineTransitionTests.cs`
  - 事件处理、切换顺序、观测事件测试。
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineQueueTests.cs`
  - 重入事件与回调内切换串行化测试。
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineExceptionTests.cs`
  - 异常与诊断信息测试。

### Task 1: Assembly Scaffold + Start 行为最小闭环

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/Fun.Framework.Fsm.asmdef`
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/Fun.Framework.Fsm.Tests.asmdef`
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/FsmTestTypes.cs`
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineStartTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/IFsmState.cs`
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/FsmResult.cs`
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateChange.cs`
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs`
- Test: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineStartTests.cs`

- [ ] **Step 1: 创建 asmdef（Runtime + Tests）**

```json
// UnityProject/Assets/Fun/Framework/Fsm/Runtime/Fun.Framework.Fsm.asmdef
{
  "name": "Fun.Framework.Fsm",
  "rootNamespace": "Fun.Framework.Fsm",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": true
}
```

```json
// UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/Fun.Framework.Fsm.Tests.asmdef
{
  "name": "Fun.Framework.Fsm.Tests",
  "rootNamespace": "Fun.Framework.Fsm.Tests",
  "references": [
    "Fun.Framework.Fsm"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "optionalUnityReferences": [
    "TestAssemblies"
  ],
  "noEngineReferences": true
}
```

- [ ] **Step 2: 写失败测试（Start 进入初始状态 + 未启动 Fire 抛错）**

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/FsmTestTypes.cs
using System;
using Fun.Framework.Fsm;

namespace Fun.Framework.Fsm.Tests
{
    internal enum TestStateId
    {
        A,
        B,
        C,
        Missing
    }

    internal readonly struct TestEvent
    {
        public TestEvent(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public override string ToString()
        {
            return Name;
        }

        public static TestEvent Named(string name)
        {
            return new TestEvent(name);
        }
    }

    internal sealed class RecordingState : IFsmState<TestStateId, TestEvent>
    {
        public RecordingState(TestStateId id)
        {
            Id = id;
            OnEventHandler = _ => FsmResult<TestStateId>.Ignored();
        }

        public TestStateId Id { get; }

        public int EnterCount { get; private set; }

        public int ExitCount { get; private set; }

        public Func<TestEvent, FsmResult<TestStateId>> OnEventHandler { get; set; }

        public Action<StateChange<TestStateId, TestEvent>> OnEnterAction { get; set; }

        public Action<StateChange<TestStateId, TestEvent>> OnExitAction { get; set; }

        public Action<TestEvent> OnEventAction { get; set; }

        public void OnEnter(in StateChange<TestStateId, TestEvent> change)
        {
            EnterCount++;
            OnEnterAction?.Invoke(change);
        }

        public void OnExit(in StateChange<TestStateId, TestEvent> change)
        {
            ExitCount++;
            OnExitAction?.Invoke(change);
        }

        public FsmResult<TestStateId> OnEvent(in TestEvent evt)
        {
            OnEventAction?.Invoke(evt);
            return OnEventHandler.Invoke(evt);
        }
    }
}
```

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineStartTests.cs
using System;
using NUnit.Framework;

namespace Fun.Framework.Fsm.Tests
{
    public class StateMachineStartTests
    {
        [Test]
        public void Start_EntersInitialState()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            var stateA = new RecordingState(TestStateId.A);

            fsm.Register(stateA);
            fsm.Start(TestStateId.A);

            Assert.That(fsm.IsStarted, Is.True);
            Assert.That(fsm.CurrentStateId, Is.EqualTo(TestStateId.A));
            Assert.That(stateA.EnterCount, Is.EqualTo(1));
        }

        [Test]
        public void Fire_BeforeStart_ThrowsInvalidOperationException()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            var stateA = new RecordingState(TestStateId.A);
            fsm.Register(stateA);

            Assert.Throws<InvalidOperationException>(() => fsm.Fire(TestEvent.Named("Tick")));
        }
    }
}
```

- [ ] **Step 3: 运行测试并确认失败**

Run: `Unity -batchmode -projectPath UnityProject -runTests -testPlatform EditMode -testFilter Fun.Framework.Fsm.Tests.StateMachineStartTests -testResults UnityProject/TestResults/fsm-start-fail.xml -quit`
Expected: 编译失败，提示 `StateMachine` / `IFsmState` / `FsmResult` / `StateChange` 未定义。

- [ ] **Step 4: 实现最小 Runtime 代码让测试通过**

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Runtime/IFsmState.cs
namespace Fun.Framework.Fsm
{
    public interface IFsmState<TStateId, TEvent>
    {
        TStateId Id { get; }
        void OnEnter(in StateChange<TStateId, TEvent> change);
        void OnExit(in StateChange<TStateId, TEvent> change);
        FsmResult<TStateId> OnEvent(in TEvent evt);
    }
}
```

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Runtime/FsmResult.cs
namespace Fun.Framework.Fsm
{
    public readonly struct FsmResult<TStateId>
    {
        private readonly TStateId _nextStateId;

        private FsmResult(bool isHandled, bool hasTransition, TStateId nextStateId)
        {
            IsHandled = isHandled;
            HasTransition = hasTransition;
            _nextStateId = nextStateId;
        }

        public bool IsHandled { get; }

        public bool HasTransition { get; }

        public TStateId NextStateId
        {
            get
            {
                return _nextStateId;
            }
        }

        public static FsmResult<TStateId> Handled()
        {
            return new FsmResult<TStateId>(true, false, default(TStateId));
        }

        public static FsmResult<TStateId> Ignored()
        {
            return new FsmResult<TStateId>(false, false, default(TStateId));
        }

        public static FsmResult<TStateId> TransitionTo(TStateId next)
        {
            return new FsmResult<TStateId>(true, true, next);
        }
    }
}
```

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateChange.cs
namespace Fun.Framework.Fsm
{
    public readonly struct StateChange<TStateId, TEvent>
    {
        private StateChange(bool hasFrom, TStateId from, TStateId to, bool hasCauseEvent, TEvent causeEvent, long sequence)
        {
            HasFrom = hasFrom;
            From = from;
            To = to;
            HasCauseEvent = hasCauseEvent;
            CauseEvent = causeEvent;
            Sequence = sequence;
        }

        public bool HasFrom { get; }

        public TStateId From { get; }

        public TStateId To { get; }

        public bool HasCauseEvent { get; }

        public TEvent CauseEvent { get; }

        public long Sequence { get; }

        public static StateChange<TStateId, TEvent> Initial(TStateId to, long sequence)
        {
            return new StateChange<TStateId, TEvent>(false, default(TStateId), to, false, default(TEvent), sequence);
        }

        public static StateChange<TStateId, TEvent> Create(TStateId from, TStateId to, TEvent causeEvent, long sequence)
        {
            return new StateChange<TStateId, TEvent>(true, from, to, true, causeEvent, sequence);
        }

        public static StateChange<TStateId, TEvent> CreateWithoutEvent(TStateId from, TStateId to, long sequence)
        {
            return new StateChange<TStateId, TEvent>(true, from, to, false, default(TEvent), sequence);
        }
    }
}
```

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs
using System;
using System.Collections.Generic;

namespace Fun.Framework.Fsm
{
    public sealed class StateMachine<TStateId, TEvent>
    {
        private readonly Dictionary<TStateId, IFsmState<TStateId, TEvent>> _states;
        private IFsmState<TStateId, TEvent> _currentState;
        private long _sequence;

        public StateMachine(IEqualityComparer<TStateId> comparer = null)
        {
            _states = new Dictionary<TStateId, IFsmState<TStateId, TEvent>>(comparer ?? EqualityComparer<TStateId>.Default);
        }

        public event Action<StateChange<TStateId, TEvent>> OnStateChanged;

        public bool IsStarted { get; private set; }

        public TStateId CurrentStateId { get; private set; }

        public void Register(IFsmState<TStateId, TEvent> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (_states.ContainsKey(state.Id))
            {
                throw new InvalidOperationException($"State '{state.Id}' is already registered.");
            }

            _states.Add(state.Id, state);
        }

        public void Start(TStateId initial)
        {
            if (!_states.TryGetValue(initial, out var initialState))
            {
                throw new InvalidOperationException($"Initial state '{initial}' is not registered.");
            }

            IsStarted = true;
            _currentState = initialState;
            CurrentStateId = initial;

            var change = StateChange<TStateId, TEvent>.Initial(initial, NextSequence());
            initialState.OnEnter(in change);
        }

        public void Fire(in TEvent evt)
        {
            EnsureStarted();
            _currentState.OnEvent(in evt);
        }

        public void ChangeState(TStateId next)
        {
            EnsureStarted();
        }

        private long NextSequence()
        {
            _sequence++;
            return _sequence;
        }

        private void EnsureStarted()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("State machine is not started.");
            }
        }
    }
}
```

- [ ] **Step 5: 运行测试并确认通过**

Run: `Unity -batchmode -projectPath UnityProject -runTests -testPlatform EditMode -testFilter Fun.Framework.Fsm.Tests.StateMachineStartTests -testResults UnityProject/TestResults/fsm-start-pass.xml -quit`
Expected: `StateMachineStartTests` 全部通过。

- [ ] **Step 6: 提交**

```bash
git add UnityProject/Assets/Fun/Framework/Fsm/Runtime/Fun.Framework.Fsm.asmdef \
  UnityProject/Assets/Fun/Framework/Fsm/Runtime/IFsmState.cs \
  UnityProject/Assets/Fun/Framework/Fsm/Runtime/FsmResult.cs \
  UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateChange.cs \
  UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs \
  UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/Fun.Framework.Fsm.Tests.asmdef \
  UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/FsmTestTypes.cs \
  UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineStartTests.cs

git commit -m "feat(fsm): scaffold runtime and start behavior baseline"
```

### Task 2: 注册规则与启动前置约束

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineRegistrationTests.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs`
- Test: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineRegistrationTests.cs`

- [ ] **Step 1: 写失败测试（重复注册、未知初始状态、重复 Start）**

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineRegistrationTests.cs
using System;
using NUnit.Framework;

namespace Fun.Framework.Fsm.Tests
{
    public class StateMachineRegistrationTests
    {
        [Test]
        public void Register_DuplicateId_ThrowsInvalidOperationException()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();

            fsm.Register(new RecordingState(TestStateId.A));

            Assert.Throws<InvalidOperationException>(() => fsm.Register(new RecordingState(TestStateId.A)));
        }

        [Test]
        public void Start_UnknownInitialState_ThrowsInvalidOperationException()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            fsm.Register(new RecordingState(TestStateId.A));

            Assert.Throws<InvalidOperationException>(() => fsm.Start(TestStateId.Missing));
        }

        [Test]
        public void Start_Twice_ThrowsInvalidOperationException()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            fsm.Register(new RecordingState(TestStateId.A));

            fsm.Start(TestStateId.A);

            Assert.Throws<InvalidOperationException>(() => fsm.Start(TestStateId.A));
        }
    }
}
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `Unity -batchmode -projectPath UnityProject -runTests -testPlatform EditMode -testFilter Fun.Framework.Fsm.Tests.StateMachineRegistrationTests -testResults UnityProject/TestResults/fsm-registration-fail.xml -quit`
Expected: `Start_Twice_ThrowsInvalidOperationException` 失败（当前实现未限制重复启动）。

- [ ] **Step 3: 实现最小代码让测试通过**

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs
public void Start(TStateId initial)
{
    if (IsStarted)
    {
        throw new InvalidOperationException("State machine is already started.");
    }

    if (!_states.TryGetValue(initial, out var initialState))
    {
        throw new InvalidOperationException($"Initial state '{initial}' is not registered.");
    }

    IsStarted = true;
    _currentState = initialState;
    CurrentStateId = initial;

    var change = StateChange<TStateId, TEvent>.Initial(initial, NextSequence());
    initialState.OnEnter(in change);
}
```

- [ ] **Step 4: 运行测试并确认通过**

Run: `Unity -batchmode -projectPath UnityProject -runTests -testPlatform EditMode -testFilter Fun.Framework.Fsm.Tests.StateMachineRegistrationTests -testResults UnityProject/TestResults/fsm-registration-pass.xml -quit`
Expected: `StateMachineRegistrationTests` 全部通过。

- [ ] **Step 5: 提交**

```bash
git add UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineRegistrationTests.cs \
  UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs

git commit -m "test(fsm): enforce registration and start preconditions"
```

### Task 3: 事件处理、状态切换与可观测性

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineTransitionTests.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs`
- Test: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineTransitionTests.cs`

- [ ] **Step 1: 写失败测试（Handled/Ignored、Transition、回调顺序、OnStateChanged、同态 no-op）**

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineTransitionTests.cs
using System.Collections.Generic;
using NUnit.Framework;

namespace Fun.Framework.Fsm.Tests
{
    public class StateMachineTransitionTests
    {
        [Test]
        public void Fire_HandledOrIgnored_DoesNotTransition()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            var stateA = new RecordingState(TestStateId.A)
            {
                OnEventHandler = evt => evt.Name == "Handled"
                    ? FsmResult<TestStateId>.Handled()
                    : FsmResult<TestStateId>.Ignored()
            };

            fsm.Register(stateA);
            fsm.Start(TestStateId.A);

            fsm.Fire(TestEvent.Named("Handled"));
            fsm.Fire(TestEvent.Named("Ignored"));

            Assert.That(fsm.CurrentStateId, Is.EqualTo(TestStateId.A));
            Assert.That(stateA.ExitCount, Is.EqualTo(0));
        }

        [Test]
        public void Fire_Transition_ExecutesExitEnterThenPublishesStateChanged()
        {
            var trace = new List<string>();

            var fsm = new StateMachine<TestStateId, TestEvent>();
            var stateA = new RecordingState(TestStateId.A)
            {
                OnEventHandler = evt => evt.Name == "GoB"
                    ? FsmResult<TestStateId>.TransitionTo(TestStateId.B)
                    : FsmResult<TestStateId>.Ignored(),
                OnExitAction = _ => trace.Add("Exit:A")
            };
            var stateB = new RecordingState(TestStateId.B)
            {
                OnEnterAction = _ => trace.Add("Enter:B")
            };

            fsm.OnStateChanged += change =>
            {
                trace.Add($"Changed:{change.From}->{change.To}:{change.CauseEvent.Name}");
            };

            fsm.Register(stateA);
            fsm.Register(stateB);
            fsm.Start(TestStateId.A);
            trace.Clear();

            fsm.Fire(TestEvent.Named("GoB"));

            Assert.That(fsm.CurrentStateId, Is.EqualTo(TestStateId.B));
            Assert.That(trace, Is.EqualTo(new[]
            {
                "Exit:A",
                "Enter:B",
                "Changed:A->B:GoB"
            }));
        }

        [Test]
        public void ChangeState_ToCurrentState_IsNoOp()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            var stateA = new RecordingState(TestStateId.A);

            fsm.Register(stateA);
            fsm.Start(TestStateId.A);

            fsm.ChangeState(TestStateId.A);

            Assert.That(stateA.EnterCount, Is.EqualTo(1));
            Assert.That(stateA.ExitCount, Is.EqualTo(0));
        }
    }
}
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `Unity -batchmode -projectPath UnityProject -runTests -testPlatform EditMode -testFilter Fun.Framework.Fsm.Tests.StateMachineTransitionTests -testResults UnityProject/TestResults/fsm-transition-fail.xml -quit`
Expected: `Fire_Transition_ExecutesExitEnterThenPublishesStateChanged` 失败（当前 `Fire` 仅调用 `OnEvent`，未执行切换逻辑）。

- [ ] **Step 3: 实现事件处理与切换逻辑**

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs
using System;
using System.Collections.Generic;

namespace Fun.Framework.Fsm
{
    public sealed class StateMachine<TStateId, TEvent>
    {
        private readonly Dictionary<TStateId, IFsmState<TStateId, TEvent>> _states;
        private readonly Queue<TEvent> _eventQueue;
        private IFsmState<TStateId, TEvent> _currentState;
        private bool _isDrainingEvents;
        private long _sequence;

        public StateMachine(IEqualityComparer<TStateId> comparer = null)
        {
            _states = new Dictionary<TStateId, IFsmState<TStateId, TEvent>>(comparer ?? EqualityComparer<TStateId>.Default);
            _eventQueue = new Queue<TEvent>();
        }

        public event Action<StateChange<TStateId, TEvent>> OnStateChanged;

        public bool IsStarted { get; private set; }

        public TStateId CurrentStateId { get; private set; }

        public void Register(IFsmState<TStateId, TEvent> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (_states.ContainsKey(state.Id))
            {
                throw new InvalidOperationException($"State '{state.Id}' is already registered.");
            }

            _states.Add(state.Id, state);
        }

        public void Start(TStateId initial)
        {
            if (IsStarted)
            {
                throw new InvalidOperationException("State machine is already started.");
            }

            if (!_states.TryGetValue(initial, out var initialState))
            {
                throw new InvalidOperationException($"Initial state '{initial}' is not registered.");
            }

            IsStarted = true;
            _currentState = initialState;
            CurrentStateId = initial;

            var change = StateChange<TStateId, TEvent>.Initial(initial, NextSequence());
            initialState.OnEnter(in change);
        }

        public void Fire(in TEvent evt)
        {
            EnsureStarted();

            _eventQueue.Enqueue(evt);
            if (_isDrainingEvents)
            {
                return;
            }

            _isDrainingEvents = true;
            try
            {
                while (_eventQueue.Count > 0)
                {
                    var currentEvent = _eventQueue.Dequeue();
                    var result = _currentState.OnEvent(in currentEvent);
                    if (result.HasTransition)
                    {
                        ChangeStateInternal(result.NextStateId, currentEvent, true);
                    }
                }
            }
            finally
            {
                _isDrainingEvents = false;
            }
        }

        public void ChangeState(TStateId next)
        {
            EnsureStarted();
            ChangeStateInternal(next, default(TEvent), false);
        }

        private void ChangeStateInternal(TStateId next, TEvent causeEvent, bool hasCauseEvent)
        {
            if (!_states.TryGetValue(next, out var nextState))
            {
                throw new InvalidOperationException($"Target state '{next}' is not registered.");
            }

            if (EqualityComparer<TStateId>.Default.Equals(CurrentStateId, next))
            {
                return;
            }

            var fromState = _currentState;
            var fromId = CurrentStateId;
            var change = hasCauseEvent
                ? StateChange<TStateId, TEvent>.Create(fromId, next, causeEvent, NextSequence())
                : StateChange<TStateId, TEvent>.CreateWithoutEvent(fromId, next, NextSequence());

            fromState.OnExit(in change);
            _currentState = nextState;
            CurrentStateId = next;
            nextState.OnEnter(in change);
            OnStateChanged?.Invoke(change);
        }

        private long NextSequence()
        {
            _sequence++;
            return _sequence;
        }

        private void EnsureStarted()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("State machine is not started.");
            }
        }
    }
}
```

- [ ] **Step 4: 运行测试并确认通过**

Run: `Unity -batchmode -projectPath UnityProject -runTests -testPlatform EditMode -testFilter Fun.Framework.Fsm.Tests.StateMachineTransitionTests -testResults UnityProject/TestResults/fsm-transition-pass.xml -quit`
Expected: `StateMachineTransitionTests` 全部通过。

- [ ] **Step 5: 提交**

```bash
git add UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs \
  UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineTransitionTests.cs

git commit -m "feat(fsm): add event-driven transition flow and state-changed hook"
```

### Task 4: 重入安全（事件队列 + 切换串行化）

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineQueueTests.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs`
- Test: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineQueueTests.cs`

- [ ] **Step 1: 写失败测试（OnEvent 内 Fire 入队 + OnEnter 内 ChangeState 串行）**

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineQueueTests.cs
using System.Collections.Generic;
using NUnit.Framework;

namespace Fun.Framework.Fsm.Tests
{
    public class StateMachineQueueTests
    {
        [Test]
        public void Fire_InsideOnEvent_IsQueuedAndProcessedInOrder()
        {
            var trace = new List<string>();
            var fsm = new StateMachine<TestStateId, TestEvent>();

            var stateA = new RecordingState(TestStateId.A);
            var stateB = new RecordingState(TestStateId.B)
            {
                OnEnterAction = _ => trace.Add("Enter:B")
            };

            stateA.OnEventHandler = evt =>
            {
                trace.Add($"OnEvent:{evt.Name}");
                if (evt.Name == "First")
                {
                    fsm.Fire(TestEvent.Named("Second"));
                    return FsmResult<TestStateId>.Handled();
                }

                if (evt.Name == "Second")
                {
                    return FsmResult<TestStateId>.TransitionTo(TestStateId.B);
                }

                return FsmResult<TestStateId>.Ignored();
            };
            stateA.OnExitAction = _ => trace.Add("Exit:A");

            fsm.Register(stateA);
            fsm.Register(stateB);
            fsm.Start(TestStateId.A);

            fsm.Fire(TestEvent.Named("First"));

            Assert.That(trace, Is.EqualTo(new[]
            {
                "OnEvent:First",
                "OnEvent:Second",
                "Exit:A",
                "Enter:B"
            }));
            Assert.That(fsm.CurrentStateId, Is.EqualTo(TestStateId.B));
        }

        [Test]
        public void ChangeState_InsideOnEnter_IsSerializedWithoutReentrancyCrash()
        {
            var trace = new List<string>();
            var fsm = new StateMachine<TestStateId, TestEvent>();

            var stateA = new RecordingState(TestStateId.A)
            {
                OnEnterAction = _ =>
                {
                    trace.Add("Enter:A");
                    fsm.ChangeState(TestStateId.B);
                },
                OnExitAction = _ => trace.Add("Exit:A")
            };
            var stateB = new RecordingState(TestStateId.B)
            {
                OnEnterAction = _ => trace.Add("Enter:B")
            };

            fsm.Register(stateA);
            fsm.Register(stateB);

            fsm.Start(TestStateId.A);

            Assert.That(trace, Is.EqualTo(new[]
            {
                "Enter:A",
                "Exit:A",
                "Enter:B"
            }));
            Assert.That(fsm.CurrentStateId, Is.EqualTo(TestStateId.B));
        }
    }
}
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `Unity -batchmode -projectPath UnityProject -runTests -testPlatform EditMode -testFilter Fun.Framework.Fsm.Tests.StateMachineQueueTests -testResults UnityProject/TestResults/fsm-queue-fail.xml -quit`
Expected: `ChangeState_InsideOnEnter_IsSerializedWithoutReentrancyCrash` 失败（当前切换逻辑未串行化，存在重入风险）。

- [ ] **Step 3: 实现串行切换队列**

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs
using System;
using System.Collections.Generic;

namespace Fun.Framework.Fsm
{
    public sealed class StateMachine<TStateId, TEvent>
    {
        private readonly struct TransitionRequest
        {
            public TransitionRequest(TStateId nextStateId, TEvent causeEvent, bool hasCauseEvent)
            {
                NextStateId = nextStateId;
                CauseEvent = causeEvent;
                HasCauseEvent = hasCauseEvent;
            }

            public TStateId NextStateId { get; }

            public TEvent CauseEvent { get; }

            public bool HasCauseEvent { get; }
        }

        private readonly Dictionary<TStateId, IFsmState<TStateId, TEvent>> _states;
        private readonly Queue<TEvent> _eventQueue;
        private readonly Queue<TransitionRequest> _transitionQueue;
        private IFsmState<TStateId, TEvent> _currentState;
        private bool _isDrainingEvents;
        private bool _isDrainingTransitions;
        private long _sequence;

        public StateMachine(IEqualityComparer<TStateId> comparer = null)
        {
            _states = new Dictionary<TStateId, IFsmState<TStateId, TEvent>>(comparer ?? EqualityComparer<TStateId>.Default);
            _eventQueue = new Queue<TEvent>();
            _transitionQueue = new Queue<TransitionRequest>();
        }

        public event Action<StateChange<TStateId, TEvent>> OnStateChanged;

        public bool IsStarted { get; private set; }

        public TStateId CurrentStateId { get; private set; }

        public void Register(IFsmState<TStateId, TEvent> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (_states.ContainsKey(state.Id))
            {
                throw new InvalidOperationException($"State '{state.Id}' is already registered.");
            }

            _states.Add(state.Id, state);
        }

        public void Start(TStateId initial)
        {
            if (IsStarted)
            {
                throw new InvalidOperationException("State machine is already started.");
            }

            if (!_states.TryGetValue(initial, out var initialState))
            {
                throw new InvalidOperationException($"Initial state '{initial}' is not registered.");
            }

            IsStarted = true;
            _currentState = initialState;
            CurrentStateId = initial;

            var change = StateChange<TStateId, TEvent>.Initial(initial, NextSequence());
            initialState.OnEnter(in change);
        }

        public void Fire(in TEvent evt)
        {
            EnsureStarted();

            _eventQueue.Enqueue(evt);
            if (_isDrainingEvents)
            {
                return;
            }

            _isDrainingEvents = true;
            try
            {
                while (_eventQueue.Count > 0)
                {
                    var currentEvent = _eventQueue.Dequeue();
                    var result = _currentState.OnEvent(in currentEvent);
                    if (result.HasTransition)
                    {
                        EnqueueTransition(result.NextStateId, currentEvent, true);
                    }
                }
            }
            finally
            {
                _isDrainingEvents = false;
            }
        }

        public void ChangeState(TStateId next)
        {
            EnsureStarted();
            EnqueueTransition(next, default(TEvent), false);
        }

        private void EnqueueTransition(TStateId next, TEvent causeEvent, bool hasCauseEvent)
        {
            _transitionQueue.Enqueue(new TransitionRequest(next, causeEvent, hasCauseEvent));
            if (_isDrainingTransitions)
            {
                return;
            }

            _isDrainingTransitions = true;
            try
            {
                while (_transitionQueue.Count > 0)
                {
                    var request = _transitionQueue.Dequeue();
                    ExecuteTransition(request);
                }
            }
            finally
            {
                _isDrainingTransitions = false;
            }
        }

        private void ExecuteTransition(TransitionRequest request)
        {
            if (!_states.TryGetValue(request.NextStateId, out var nextState))
            {
                throw new InvalidOperationException($"Target state '{request.NextStateId}' is not registered.");
            }

            if (EqualityComparer<TStateId>.Default.Equals(CurrentStateId, request.NextStateId))
            {
                return;
            }

            var fromState = _currentState;
            var fromId = CurrentStateId;
            var change = request.HasCauseEvent
                ? StateChange<TStateId, TEvent>.Create(fromId, request.NextStateId, request.CauseEvent, NextSequence())
                : StateChange<TStateId, TEvent>.CreateWithoutEvent(fromId, request.NextStateId, NextSequence());

            fromState.OnExit(in change);
            _currentState = nextState;
            CurrentStateId = request.NextStateId;
            nextState.OnEnter(in change);
            OnStateChanged?.Invoke(change);
        }

        private long NextSequence()
        {
            _sequence++;
            return _sequence;
        }

        private void EnsureStarted()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("State machine is not started.");
            }
        }
    }
}
```

- [ ] **Step 4: 运行测试并确认通过**

Run: `Unity -batchmode -projectPath UnityProject -runTests -testPlatform EditMode -testFilter Fun.Framework.Fsm.Tests.StateMachineQueueTests -testResults UnityProject/TestResults/fsm-queue-pass.xml -quit`
Expected: `StateMachineQueueTests` 全部通过。

- [ ] **Step 5: 提交**

```bash
git add UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs \
  UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineQueueTests.cs

git commit -m "feat(fsm): serialize reentrant events and transitions"
```

### Task 5: Fail-fast 异常语义与最终验收

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineExceptionTests.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs`
- Test: `UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineExceptionTests.cs`

- [ ] **Step 1: 写失败测试（未知目标状态 + OnExit/OnEnter 异常上下文）**

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineExceptionTests.cs
using System;
using NUnit.Framework;

namespace Fun.Framework.Fsm.Tests
{
    public class StateMachineExceptionTests
    {
        [Test]
        public void ChangeState_UnknownTarget_ThrowsInvalidOperationException()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            fsm.Register(new RecordingState(TestStateId.A));
            fsm.Start(TestStateId.A);

            var ex = Assert.Throws<InvalidOperationException>(() => fsm.ChangeState(TestStateId.Missing));
            Assert.That(ex.Message, Does.Contain("Target state"));
            Assert.That(ex.Message, Does.Contain("Missing"));
        }

        [Test]
        public void Fire_WhenOnExitThrows_WrapsExceptionWithTransitionContext()
        {
            var stateA = new RecordingState(TestStateId.A)
            {
                OnEventHandler = _ => FsmResult<TestStateId>.TransitionTo(TestStateId.B),
                OnExitAction = _ => throw new Exception("exit crash")
            };
            var stateB = new RecordingState(TestStateId.B);

            var fsm = new StateMachine<TestStateId, TestEvent>();
            fsm.Register(stateA);
            fsm.Register(stateB);
            fsm.Start(TestStateId.A);

            var ex = Assert.Throws<InvalidOperationException>(() => fsm.Fire(TestEvent.Named("GoB")));
            Assert.That(ex.Message, Does.Contain("OnExit"));
            Assert.That(ex.Message, Does.Contain("A"));
            Assert.That(ex.Message, Does.Contain("B"));
            Assert.That(ex.InnerException, Is.Not.Null);
            Assert.That(ex.InnerException.Message, Is.EqualTo("exit crash"));
        }

        [Test]
        public void Fire_WhenOnEnterThrows_WrapsExceptionWithTransitionContext()
        {
            var stateA = new RecordingState(TestStateId.A)
            {
                OnEventHandler = _ => FsmResult<TestStateId>.TransitionTo(TestStateId.B)
            };
            var stateB = new RecordingState(TestStateId.B)
            {
                OnEnterAction = _ => throw new Exception("enter crash")
            };

            var fsm = new StateMachine<TestStateId, TestEvent>();
            fsm.Register(stateA);
            fsm.Register(stateB);
            fsm.Start(TestStateId.A);

            var ex = Assert.Throws<InvalidOperationException>(() => fsm.Fire(TestEvent.Named("GoB")));
            Assert.That(ex.Message, Does.Contain("OnEnter"));
            Assert.That(ex.Message, Does.Contain("A"));
            Assert.That(ex.Message, Does.Contain("B"));
            Assert.That(ex.InnerException, Is.Not.Null);
            Assert.That(ex.InnerException.Message, Is.EqualTo("enter crash"));
        }
    }
}
```

- [ ] **Step 2: 运行测试并确认失败**

Run: `Unity -batchmode -projectPath UnityProject -runTests -testPlatform EditMode -testFilter Fun.Framework.Fsm.Tests.StateMachineExceptionTests -testResults UnityProject/TestResults/fsm-exception-fail.xml -quit`
Expected: `Fire_WhenOnExitThrows_WrapsExceptionWithTransitionContext` 与 `Fire_WhenOnEnterThrows_WrapsExceptionWithTransitionContext` 失败（当前实现未做异常包装）。

- [ ] **Step 3: 实现异常包装与上下文信息**

```csharp
// UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs
private void ExecuteTransition(TransitionRequest request)
{
    if (!_states.TryGetValue(request.NextStateId, out var nextState))
    {
        throw new InvalidOperationException($"Target state '{request.NextStateId}' is not registered.");
    }

    if (EqualityComparer<TStateId>.Default.Equals(CurrentStateId, request.NextStateId))
    {
        return;
    }

    var fromState = _currentState;
    var fromId = CurrentStateId;
    var change = request.HasCauseEvent
        ? StateChange<TStateId, TEvent>.Create(fromId, request.NextStateId, request.CauseEvent, NextSequence())
        : StateChange<TStateId, TEvent>.CreateWithoutEvent(fromId, request.NextStateId, NextSequence());

    try
    {
        fromState.OnExit(in change);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            $"OnExit failed during transition '{fromId}' -> '{request.NextStateId}'.",
            ex);
    }

    _currentState = nextState;
    CurrentStateId = request.NextStateId;

    try
    {
        nextState.OnEnter(in change);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            $"OnEnter failed during transition '{fromId}' -> '{request.NextStateId}'.",
            ex);
    }

    OnStateChanged?.Invoke(change);
}
```

- [ ] **Step 4: 跑完整 EditMode 测试集并验收**

Run: `Unity -batchmode -projectPath UnityProject -runTests -testPlatform EditMode -testFilter Fun.Framework.Fsm.Tests -testResults UnityProject/TestResults/fsm-all-pass.xml -quit`
Expected: `Fun.Framework.Fsm.Tests` 全量通过，失败数为 0。

- [ ] **Step 5: 提交**

```bash
git add UnityProject/Assets/Fun/Framework/Fsm/Runtime/StateMachine.cs \
  UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/StateMachineExceptionTests.cs

git commit -m "feat(fsm): add fail-fast exception diagnostics and pass full test suite"
```

## Plan Self-Review

- Spec coverage:
  - 纯事件驱动、接口类状态、基础能力（切换/Enter/Exit/事件处理）由 Task 1-3 覆盖。
  - 串行化与重入处理由 Task 4 覆盖。
  - 异常策略与可诊断性由 Task 5 覆盖。
  - 测试与验收标准在 Task 5 的全量测试步骤闭环。
- Placeholder scan:
  - 未发现占位词或延后实现描述。
- Type consistency:
  - `IFsmState<TStateId, TEvent>`、`FsmResult<TStateId>`、`StateChange<TStateId, TEvent>` 与 `StateMachine<TStateId, TEvent>` 命名和签名在所有任务保持一致。
