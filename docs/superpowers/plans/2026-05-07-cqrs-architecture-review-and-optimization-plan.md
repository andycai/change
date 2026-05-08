# CQRS Architecture Review and Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor `Change.Framework.Cqrs` into explicit in-process CQRS + Domain Events semantics with explicit bootstrap/runtime lifecycle and context-isolated runtime instances, while preserving hot-path 0GC behavior.

**Architecture:** Keep the existing `CqrsBus` dispatch internals as the performance core, but split public contracts into bootstrap-only and runtime-only surfaces. Add an explicit context provider for multi-instance access, migrate from exposed `Freeze()` to `Build()`, and enforce semantic boundaries through dedicated architecture tests.

**Tech Stack:** Unity 2022.3, C# (.NET Standard 2.1), NUnit (Unity EditMode tests), Change.Framework collections/logging.

---

## File Structure and Responsibilities

- `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntime.cs`
  - Runtime-only CQRS dispatch contract (`Send`, `Ask`, `Publish`).
- `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBootstrap.cs`
  - Startup-only registration contract and `Build()`.
- `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntimeProvider.cs`
  - Context-id based runtime lookup.
- `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEvent.cs`
  - Domain event marker interface (past-fact semantics).
- `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEventHandler.cs`
  - Domain event handler contract.
- `UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBootstrap.cs`
  - Concrete bootstrap implementation wrapping existing bus registration APIs.
- `UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsRuntime.cs`
  - Concrete runtime implementation exposing dispatch only.
- `UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsContextRuntimeProvider.cs`
  - Context-isolated runtime registry/provider.
- `UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
  - Internal compatibility and dispatch core updates (keep hot-path behavior).
- `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsBootstrapLifecycleTests.cs`
  - Tests for `Build()` semantics replacing external freeze usage.
- `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsContextRuntimeProviderTests.cs`
  - Tests for explicit context creation and lookup.
- `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DomainEventSemanticsTests.cs`
  - Domain event naming/contract behavior and publish semantics tests.
- `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsArchitectureGuardTests.cs`
  - Architecture tests for semantic boundaries.
- `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ZeroAllocationDispatchTests.cs`
  - Updated to cover runtime facade path for no-allocation guarantees.
- `docs/superpowers/specs/2026-05-07-cqrs-architecture-review-and-optimization-design.md`
  - Optional doc patch if implementation naming diverges from spec language.

### Task 1: Introduce Runtime/Bootstrap Contracts

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntime.cs`
- Create: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBootstrap.cs`
- Create: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntimeProvider.cs`
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBus.cs`
- Test: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsBootstrapLifecycleTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class CqrsBootstrapLifecycleTests
    {
        [Test]
        public void Build_ReturnsRuntime_ThatCanDispatchCommand()
        {
            var bootstrap = new CqrsBootstrap();
            var handler = new IncrementCommandHandler();
            bootstrap.RegisterCommand(handler);

            var runtime = bootstrap.Build();
            runtime.Send(new IncrementCommand(1));

            Assert.AreEqual(1, handler.Value);
        }

        private readonly struct IncrementCommand : ICommand
        {
            public IncrementCommand(int delta) { Delta = delta; }
            public int Delta { get; }
        }

        private sealed class IncrementCommandHandler : ICommandHandler<IncrementCommand>
        {
            public int Value;
            public void Handle(in IncrementCommand command) { Value += command.Delta; }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.CqrsBootstrapLifecycleTests.Build_ReturnsRuntime_ThatCanDispatchCommand" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-cqrs-bootstrap-20260507-200500.xml"`

Expected: FAIL with compile error for missing `CqrsBootstrap`/`ICqrsRuntime` APIs.

- [ ] **Step 3: Write minimal implementation**

```csharp
// ICqrsRuntime.cs
namespace Change.Framework.Cqrs
{
    public interface ICqrsRuntime
    {
        void Send<TCommand>(in TCommand command) where TCommand : struct, ICommand;
        TResult Ask<TQuery, TResult>(in TQuery query) where TQuery : struct, IQuery<TResult>;
        void Publish<TEvent>(in TEvent @event) where TEvent : struct, IEvent;
    }
}

// ICqrsBootstrap.cs
namespace Change.Framework.Cqrs
{
    public interface ICqrsBootstrap
    {
        void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler) where TCommand : struct, ICommand;
        void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler) where TQuery : struct, IQuery<TResult>;
        void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : struct, IEvent;
        ICqrsRuntime Build();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run the same Unity command as Step 2.

Expected: PASS for `Build_ReturnsRuntime_ThatCanDispatchCommand`.

- [ ] **Step 5: Commit**

```bash
git add "UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntime.cs" \
        "UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBootstrap.cs" \
        "UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntimeProvider.cs" \
        "UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBus.cs" \
        "UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsBootstrapLifecycleTests.cs"
git commit -m "feat(cqrs): add bootstrap/runtime contract split"
```

### Task 2: Implement Build-Based Lifecycle and Runtime Facade

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBootstrap.cs`
- Create: `UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsRuntime.cs`
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- Test: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsBootstrapLifecycleTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void Build_PreventsFurtherRegistration()
{
    var bootstrap = new CqrsBootstrap();
    bootstrap.RegisterCommand(new IncrementCommandHandler());
    var runtime = bootstrap.Build();

    Assert.Throws<RegistryFrozenException>(() => bootstrap.RegisterCommand(new IncrementCommandHandler()));
}

[Test]
public void Dispatch_BeforeBuild_IsNotPossibleFromRuntime()
{
    var bootstrap = new CqrsBootstrap();
    // compile-time guarantee: no runtime object is available before Build.
    Assert.Pass();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.CqrsBootstrapLifecycleTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-cqrs-lifecycle-20260507-201000.xml"`

Expected: FAIL because `CqrsBootstrap` does not enforce post-build registration lock yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
// CqrsBootstrap.cs
namespace Change.Framework.Cqrs
{
    public sealed class CqrsBootstrap : ICqrsBootstrap
    {
        private readonly CqrsBus _bus = new();
        private bool _built;

        public void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler) where TCommand : struct, ICommand
        {
            if (_built) throw new RegistryFrozenException("Registry is frozen.");
            _bus.RegisterCommand(handler);
        }

        public ICqrsRuntime Build()
        {
            if (_built) throw new InvalidOperationException("Build can only be called once.");
            _bus.Freeze();
            _built = true;
            return new CqrsRuntime(_bus);
        }
    }

    internal sealed class CqrsRuntime : ICqrsRuntime
    {
        private readonly CqrsBus _bus;
        public CqrsRuntime(CqrsBus bus) { _bus = bus; }
        public void Send<TCommand>(in TCommand command) where TCommand : struct, ICommand => _bus.Send(in command);
        public TResult Ask<TQuery, TResult>(in TQuery query) where TQuery : struct, IQuery<TResult> => _bus.Query<TQuery, TResult>(in query);
        public void Publish<TEvent>(in TEvent @event) where TEvent : struct, IEvent => _bus.Publish(in @event);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run the same Unity command as Step 2.

Expected: PASS for all tests in `CqrsBootstrapLifecycleTests`.

- [ ] **Step 5: Commit**

```bash
git add "UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBootstrap.cs" \
        "UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsRuntime.cs" \
        "UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs" \
        "UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsBootstrapLifecycleTests.cs"
git commit -m "refactor(cqrs): replace external freeze usage with build lifecycle"
```

### Task 3: Add Explicit Context-Isolated Runtime Provider

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsContextRuntimeProvider.cs`
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntimeProvider.cs`
- Test: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsContextRuntimeProviderTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class CqrsContextRuntimeProviderTests
    {
        [Test]
        public void Get_WithUnknownContext_Throws()
        {
            var provider = new CqrsContextRuntimeProvider();
            Assert.Throws<HandlerNotRegisteredException>(() => provider.Get("unknown"));
        }

        [Test]
        public void RegisterContext_ThenGet_ReturnsSameRuntime()
        {
            var provider = new CqrsContextRuntimeProvider();
            var runtime = new CqrsBootstrap().Build();
            provider.Register("battle", runtime);

            Assert.AreSame(runtime, provider.Get("battle"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.CqrsContextRuntimeProviderTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-cqrs-context-20260507-201500.xml"`

Expected: FAIL because provider implementation does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace Change.Framework.Cqrs
{
    public interface ICqrsRuntimeProvider
    {
        void Register(string contextId, ICqrsRuntime runtime);
        ICqrsRuntime Get(string contextId);
    }

    public sealed class CqrsContextRuntimeProvider : ICqrsRuntimeProvider
    {
        private readonly FastDictionary<string, ICqrsRuntime> _runtimes = new();

        public void Register(string contextId, ICqrsRuntime runtime)
        {
            if (string.IsNullOrWhiteSpace(contextId)) throw new ArgumentException("Context id is required.", nameof(contextId));
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (!_runtimes.TryAdd(contextId, runtime))
                throw new DuplicateRegistrationException($"CQRS runtime already exists for context: {contextId}");
        }

        public ICqrsRuntime Get(string contextId)
        {
            if (!_runtimes.TryGetValue(contextId, out var runtime))
                throw new HandlerNotRegisteredException($"CQRS runtime not found for context: {contextId}");
            return runtime;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run the same Unity command as Step 2.

Expected: PASS for all tests in `CqrsContextRuntimeProviderTests`.

- [ ] **Step 5: Commit**

```bash
git add "UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsContextRuntimeProvider.cs" \
        "UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsRuntimeProvider.cs" \
        "UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsContextRuntimeProviderTests.cs"
git commit -m "feat(cqrs): add explicit context runtime provider"
```

### Task 4: Introduce Domain Event Contracts and Compatibility Path

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEvent.cs`
- Create: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEventHandler.cs`
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IEvent.cs`
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IEventHandler.cs`
- Test: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DomainEventSemanticsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class DomainEventSemanticsTests
    {
        private readonly struct UnitCreatedEvent : IDomainEvent {}

        [Test]
        public void DomainEvent_CanBePublishedThroughRuntime()
        {
            var bootstrap = new CqrsBootstrap();
            var handler = new UnitCreatedHandler();
            bootstrap.Subscribe(handler);
            var runtime = bootstrap.Build();

            runtime.Publish(new UnitCreatedEvent());
            Assert.AreEqual(1, handler.Count);
        }

        private sealed class UnitCreatedHandler : IDomainEventHandler<UnitCreatedEvent>
        {
            public int Count;
            public void Handle(in UnitCreatedEvent @event) { Count++; }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.DomainEventSemanticsTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-cqrs-domain-event-20260507-202000.xml"`

Expected: FAIL due to missing `IDomainEvent` and `IDomainEventHandler<T>`.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace Change.Framework.Cqrs
{
    public interface IDomainEvent : IEvent
    {
    }

    public interface IDomainEventHandler<TDomainEvent> : IEventHandler<TDomainEvent>
        where TDomainEvent : struct, IDomainEvent
    {
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run the same Unity command as Step 2.

Expected: PASS for `DomainEventSemanticsTests`.

- [ ] **Step 5: Commit**

```bash
git add "UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEvent.cs" \
        "UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IDomainEventHandler.cs" \
        "UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IEvent.cs" \
        "UnityProject/Assets/Change/Framework/Cqrs/Abstractions/IEventHandler.cs" \
        "UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/DomainEventSemanticsTests.cs"
git commit -m "feat(cqrs): add domain event contracts on top of event abstractions"
```

### Task 5: Add Architecture Guard Tests for Semantic Boundaries

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsArchitectureGuardTests.cs`
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsRuntime.cs`
- Test: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsArchitectureGuardTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Linq;
using System.Reflection;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class CqrsArchitectureGuardTests
    {
        [Test]
        public void DomainEventHandler_MustNotDependOnCqrsRuntime()
        {
            var runtimeType = typeof(ICqrsRuntime);
            var handlers = Assembly.GetAssembly(typeof(ICqrsRuntime))
                .GetTypes()
                .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>)));

            foreach (var handler in handlers)
            {
                var hasRuntimeCtorArg = handler.GetConstructors()
                    .SelectMany(c => c.GetParameters())
                    .Any(p => p.ParameterType == runtimeType);
                Assert.IsFalse(hasRuntimeCtorArg, $"{handler.FullName} depends on ICqrsRuntime.");
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.CqrsArchitectureGuardTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-cqrs-guards-20260507-202500.xml"`

Expected: FAIL if any current or sample domain event handler injects runtime/command bus.

- [ ] **Step 3: Write minimal implementation**

```csharp
// Example concrete fix pattern to apply to every violating handler found by the test:
public sealed class UnitCreatedProjectionHandler : IDomainEventHandler<UnitCreatedEvent>
{
    private readonly IUnitProjectionWriter _projectionWriter;

    public UnitCreatedProjectionHandler(IUnitProjectionWriter projectionWriter)
    {
        _projectionWriter = projectionWriter;
    }

    public void Handle(in UnitCreatedEvent @event)
    {
        _projectionWriter.ApplyCreated(@event.UnitId);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run the same Unity command as Step 2.

Expected: PASS for all architecture guard tests.

- [ ] **Step 5: Commit**

```bash
git add "UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CqrsArchitectureGuardTests.cs" \
        "UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsRuntime.cs" \
        "UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs"
git commit -m "test(cqrs): enforce domain event semantic boundaries"
```

### Task 6: Preserve and Re-verify Hot-Path 0GC Through New Runtime Surface

**Files:**
- Modify: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ZeroAllocationDispatchTests.cs`
- Test: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ZeroAllocationDispatchTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void RuntimeFacade_Send_HotPath_AllocatesZeroBytesAfterWarmup()
{
    var state = new TickState();
    var bootstrap = new CqrsBootstrap();
    bootstrap.RegisterCommand(new TickCommandHandler(state));
    var runtime = bootstrap.Build();

    var command = new TickCommand(1);
    for (var i = 0; i < WarmupIterations; i++) runtime.Send(in command);

    ForceFullGc();
    var before = GC.GetAllocatedBytesForCurrentThread();
    for (var i = 0; i < MeasuredIterations; i++) runtime.Send(in command);
    var after = GC.GetAllocatedBytesForCurrentThread();

    Assert.AreEqual(before, after);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.ZeroAllocationDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-cqrs-alloc-20260507-203000.xml"`

Expected: FAIL until tests are fully migrated from direct `CqrsBus` usage to runtime facade path.

- [ ] **Step 3: Write minimal implementation**

```csharp
// Concrete replacement in each allocation test setup:
var bootstrap = new CqrsBootstrap();
bootstrap.RegisterCommand(new TickCommandHandler(state));
bootstrap.RegisterQuery(new TickQueryHandler(state));
bootstrap.Subscribe(new TickEventHandler());
var runtime = bootstrap.Build();

// Use runtime facade in measured loops:
runtime.Send(in command);
runtime.Ask<GetTickQuery, int>(in query);
runtime.Publish(in @event);
```

- [ ] **Step 4: Run test to verify it passes**

Run the same Unity command as Step 2.

Expected: PASS with unchanged allocation assertions.

- [ ] **Step 5: Commit**

```bash
git add "UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/ZeroAllocationDispatchTests.cs"
git commit -m "test(cqrs): verify zero-allocation dispatch via runtime facade"
```

### Task 7: Final API Compatibility Pass and Documentation Sync

**Files:**
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBus.cs`
- Modify: `UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs`
- Modify: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CommandDispatchTests.cs`
- Modify: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/QueryDispatchTests.cs`
- Modify: `UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/EventDispatchTests.cs`
- Modify: `docs/superpowers/specs/2026-05-07-cqrs-architecture-review-and-optimization-design.md` (if naming updates are needed)

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void LegacyBusApi_StillWorks_DuringTransition()
{
    var bus = new CqrsBus();
    bus.RegisterCommand(new IncrementCommandHandler());
    bus.Freeze();

    Assert.DoesNotThrow(() => bus.Send(new IncrementCommand(1)));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
`/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.CommandDispatchTests,Change.Framework.Tests.QueryDispatchTests,Change.Framework.Tests.EventDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-cqrs-compat-20260507-203500.xml"`

Expected: FAIL if compatibility behavior regressed during refactor.

- [ ] **Step 3: Write minimal implementation**

```csharp
// Keep compatibility behavior by preserving CqrsBus public API and semantics:
public sealed class CqrsBus : ICqrsBus, ICqrsRegistry
{
    // existing register/freeze/send/query/publish methods remain available
    // so current call sites and tests continue to work unchanged.
}

// New code uses:
var bootstrap = new CqrsBootstrap();
bootstrap.RegisterCommand(new IncrementCommandHandler());
var runtime = bootstrap.Build();
runtime.Send(new IncrementCommand(1));
```

- [ ] **Step 4: Run test to verify it passes**

Run the same Unity command as Step 2.

Expected: PASS for compatibility and existing behavior tests.

- [ ] **Step 5: Commit**

```bash
git add "UnityProject/Assets/Change/Framework/Cqrs/Abstractions/ICqrsBus.cs" \
        "UnityProject/Assets/Change/Framework/Cqrs/Core/CqrsBus.cs" \
        "UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/CommandDispatchTests.cs" \
        "UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/QueryDispatchTests.cs" \
        "UnityProject/Assets/Change/Framework/Tests/EditMode/Cqrs/EventDispatchTests.cs" \
        "docs/superpowers/specs/2026-05-07-cqrs-architecture-review-and-optimization-design.md"
git commit -m "refactor(cqrs): complete semantic split with compatibility coverage"
```

## Final Verification Gate

- [ ] Run full CQRS EditMode suite:

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -runTests -testPlatform editmode -testFilter "Change.Framework.Tests.CommandDispatchTests,Change.Framework.Tests.QueryDispatchTests,Change.Framework.Tests.EventDispatchTests,Change.Framework.Tests.CqrsBootstrapLifecycleTests,Change.Framework.Tests.CqrsContextRuntimeProviderTests,Change.Framework.Tests.DomainEventSemanticsTests,Change.Framework.Tests.CqrsArchitectureGuardTests,Change.Framework.Tests.ZeroAllocationDispatchTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-cqrs-full-20260507-204000.xml"
```

Expected:
- Exit code indicates successful execution.
- All listed test classes pass.
- No allocation regressions in zero-allocation tests.

- [ ] Run framework graph update after code changes:

```bash
graphify update .
```

Expected:
- Graph update completes without errors.

