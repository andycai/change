# Fun.Framework CQRS Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a zero-dependency, synchronous, hot-path-0GC CQRS core in `UnityProject/Assets/Fun/Framework` with command/query/event support and explicit manual registration.

**Architecture:** Implement one concrete `CqrsBus` that owns both registration (`ICqrsRegistry`) and dispatch (`ICqrsBus`) using strongly typed generic handlers keyed by type. Keep the API intentionally small (`Send`, `Query`, `Publish`, `Register*`, `Freeze`) and fail fast with explicit exceptions for all misconfiguration paths. Keep runtime dispatch allocation-free after warm-up by using `in` parameters, prebuilt handler tables, and typed event-handler arrays.

**Tech Stack:** C# (Unity 2022.3), Unity asmdef assemblies, Unity EditMode tests (`com.unity.test-framework`).

---

## Scope Check

This spec is a single subsystem (framework-layer CQRS core) and does not need decomposition into multiple independent plans.

## File Structure (Create/Modify Map)

### Runtime files

- Create: `UnityProject/Assets/Fun/Framework/Runtime/Fun.Framework.asmdef`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICommand.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IQuery.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IEvent.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICommandHandler.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IQueryHandler.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IEventHandler.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsBus.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsRegistry.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/ICqrsLogger.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/NullCqrsLogger.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Exceptions/DuplicateRegistrationException.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Exceptions/HandlerNotRegisteredException.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Exceptions/RegistryFrozenException.cs`
- Create: `UnityProject/Assets/Fun/Framework/README.md`

### Test files

- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Fun.Framework.Tests.asmdef`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/CommandDispatchTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/QueryDispatchTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/EventDispatchTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/RegistrationGuardTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/ZeroAllocationDispatchTests.cs`

### Tooling command used repeatedly

```bash
UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.60f1/Unity.app/Contents/MacOS/Unity"
```

Use this command shape for test runs:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "<TestFilter>" \
  -logFile -
```

---

### Task 1: Scaffold assemblies and command-only vertical slice

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Fun.Framework.asmdef`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/*.cs` (command-related first)
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs` (command path only)
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Fun.Framework.Tests.asmdef`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/CommandDispatchTests.cs`

- [ ] **Step 1: Write the failing command dispatch test**

```csharp
using Fun.Framework.Cqrs;
using NUnit.Framework;

namespace Fun.Framework.Tests
{
    public class CommandDispatchTests
    {
        private readonly struct IncrementCounterCommand : ICommand
        {
            public IncrementCounterCommand(int amount) => Amount = amount;
            public int Amount { get; }
        }

        private sealed class CounterState
        {
            public int Value;
        }

        private sealed class IncrementCounterHandler : ICommandHandler<IncrementCounterCommand>
        {
            private readonly CounterState _state;

            public IncrementCounterHandler(CounterState state)
            {
                _state = state;
            }

            public void Handle(in IncrementCounterCommand command)
            {
                _state.Value += command.Amount;
            }
        }

        [Test]
        public void Send_DispatchesRegisteredHandler()
        {
            var state = new CounterState();
            var bus = new CqrsBus();

            bus.RegisterCommand(new IncrementCounterHandler(state));
            bus.Freeze();
            bus.Send(new IncrementCounterCommand(3));

            Assert.AreEqual(3, state.Value);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.CommandDispatchTests" \
  -logFile -
```

Expected: FAIL with compile/type errors because `Fun.Framework.Cqrs` contracts and `CqrsBus` do not exist yet.

- [ ] **Step 3: Create runtime asmdef and command contracts plus minimal bus implementation**

`UnityProject/Assets/Fun/Framework/Runtime/Fun.Framework.asmdef`

```json
{
  "name": "Fun.Framework",
  "rootNamespace": "Fun.Framework",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICommand.cs`

```csharp
namespace Fun.Framework.Cqrs
{
    public interface ICommand
    {
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICommandHandler.cs`

```csharp
namespace Fun.Framework.Cqrs
{
    public interface ICommandHandler<TCommand>
        where TCommand : struct, ICommand
    {
        void Handle(in TCommand command);
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsBus.cs`

```csharp
namespace Fun.Framework.Cqrs
{
    public interface ICqrsBus
    {
        void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand;
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsRegistry.cs`

```csharp
namespace Fun.Framework.Cqrs
{
    public interface ICqrsRegistry
    {
        void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;

        void Freeze();
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Fun.Framework.Cqrs
{
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry
    {
        private readonly Dictionary<Type, object> _commandHandlers = new();
        private bool _isFrozen;

        public void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_isFrozen)
            {
                throw new InvalidOperationException("Registry is frozen.");
            }

            var key = typeof(TCommand);
            if (_commandHandlers.ContainsKey(key))
            {
                throw new InvalidOperationException($"Command handler already registered: {key.FullName}");
            }

            _commandHandlers[key] = handler;
        }

        public void Freeze()
        {
            _isFrozen = true;
        }

        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            if (!_commandHandlers.TryGetValue(typeof(TCommand), out var boxedHandler))
            {
                throw new InvalidOperationException($"Command handler not registered: {typeof(TCommand).FullName}");
            }

            var handler = (ICommandHandler<TCommand>)boxedHandler;
            handler.Handle(in command);
        }
    }
}
```

`UnityProject/Assets/Fun/Framework/Tests/EditMode/Fun.Framework.Tests.asmdef`

```json
{
  "name": "Fun.Framework.Tests",
  "rootNamespace": "Fun.Framework.Tests",
  "references": [
    "Fun.Framework"
  ],
  "optionalUnityReferences": [
    "TestAssemblies"
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
  "noEngineReferences": false
}
```

- [ ] **Step 4: Run command test to verify it passes**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.CommandDispatchTests" \
  -logFile -
```

Expected: PASS for `Send_DispatchesRegisteredHandler`.

- [ ] **Step 5: Commit Task 1**

```bash
git add \
  UnityProject/Assets/Fun/Framework/Runtime/Fun.Framework.asmdef \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICommand.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICommandHandler.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsBus.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsRegistry.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Fun.Framework.Tests.asmdef \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/CommandDispatchTests.cs

git commit -m "feat(framework): scaffold CQRS command dispatch core"
```

---

### Task 2: Add query support with strong typing

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IQuery.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IQueryHandler.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsBus.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsRegistry.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/QueryDispatchTests.cs`

- [ ] **Step 1: Write the failing query dispatch test**

```csharp
using Fun.Framework.Cqrs;
using NUnit.Framework;

namespace Fun.Framework.Tests
{
    public class QueryDispatchTests
    {
        private readonly struct GetScoreQuery : IQuery<int>
        {
        }

        private sealed class ScoreState
        {
            public int Value;
        }

        private sealed class GetScoreQueryHandler : IQueryHandler<GetScoreQuery, int>
        {
            private readonly ScoreState _state;

            public GetScoreQueryHandler(ScoreState state)
            {
                _state = state;
            }

            public int Handle(in GetScoreQuery query)
            {
                return _state.Value;
            }
        }

        [Test]
        public void Query_ReturnsResultFromRegisteredHandler()
        {
            var state = new ScoreState { Value = 27 };
            var bus = new CqrsBus();

            bus.RegisterQuery(new GetScoreQueryHandler(state));
            bus.Freeze();

            var result = bus.Query<GetScoreQuery, int>(new GetScoreQuery());

            Assert.AreEqual(27, result);
        }
    }
}
```

- [ ] **Step 2: Run query test to verify it fails**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.QueryDispatchTests" \
  -logFile -
```

Expected: FAIL because query interfaces and bus query methods are missing.

- [ ] **Step 3: Implement query contracts and query routing in the bus**

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IQuery.cs`

```csharp
namespace Fun.Framework.Cqrs
{
    public interface IQuery<TResult>
    {
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IQueryHandler.cs`

```csharp
namespace Fun.Framework.Cqrs
{
    public interface IQueryHandler<TQuery, TResult>
        where TQuery : struct, IQuery<TResult>
    {
        TResult Handle(in TQuery query);
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsBus.cs`

```csharp
namespace Fun.Framework.Cqrs
{
    public interface ICqrsBus
    {
        void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand;

        TResult Query<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>;
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsRegistry.cs`

```csharp
namespace Fun.Framework.Cqrs
{
    public interface ICqrsRegistry
    {
        void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;

        void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>;

        void Freeze();
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Fun.Framework.Cqrs
{
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry
    {
        private readonly struct QueryKey : IEquatable<QueryKey>
        {
            public QueryKey(Type queryType, Type resultType)
            {
                QueryType = queryType;
                ResultType = resultType;
            }

            public Type QueryType { get; }
            public Type ResultType { get; }

            public bool Equals(QueryKey other)
            {
                return QueryType == other.QueryType && ResultType == other.ResultType;
            }

            public override bool Equals(object obj)
            {
                return obj is QueryKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((QueryType != null ? QueryType.GetHashCode() : 0) * 397) ^
                           (ResultType != null ? ResultType.GetHashCode() : 0);
                }
            }
        }

        private readonly Dictionary<Type, object> _commandHandlers = new();
        private readonly Dictionary<QueryKey, object> _queryHandlers = new();
        private bool _isFrozen;

        public void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_isFrozen)
            {
                throw new InvalidOperationException("Registry is frozen.");
            }

            var key = typeof(TCommand);
            if (_commandHandlers.ContainsKey(key))
            {
                throw new InvalidOperationException($"Command handler already registered: {key.FullName}");
            }

            _commandHandlers[key] = handler;
        }

        public void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_isFrozen)
            {
                throw new InvalidOperationException("Registry is frozen.");
            }

            var key = new QueryKey(typeof(TQuery), typeof(TResult));
            if (_queryHandlers.ContainsKey(key))
            {
                throw new InvalidOperationException($"Query handler already registered: {typeof(TQuery).FullName}");
            }

            _queryHandlers[key] = handler;
        }

        public void Freeze()
        {
            _isFrozen = true;
        }

        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            if (!_commandHandlers.TryGetValue(typeof(TCommand), out var boxedHandler))
            {
                throw new InvalidOperationException($"Command handler not registered: {typeof(TCommand).FullName}");
            }

            var handler = (ICommandHandler<TCommand>)boxedHandler;
            handler.Handle(in command);
        }

        public TResult Query<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            var key = new QueryKey(typeof(TQuery), typeof(TResult));
            if (!_queryHandlers.TryGetValue(key, out var boxedHandler))
            {
                throw new InvalidOperationException($"Query handler not registered: {typeof(TQuery).FullName}");
            }

            var handler = (IQueryHandler<TQuery, TResult>)boxedHandler;
            return handler.Handle(in query);
        }
    }
}
```

- [ ] **Step 4: Run command + query tests to verify they pass**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.CommandDispatchTests|Fun.Framework.Tests.QueryDispatchTests" \
  -logFile -
```

Expected: PASS for both test classes.

- [ ] **Step 5: Commit Task 2**

```bash
git add \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IQuery.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IQueryHandler.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsBus.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsRegistry.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/QueryDispatchTests.cs

git commit -m "feat(framework): add CQRS query dispatch"
```

---

### Task 3: Add generic struct event publish/subscribe

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IEvent.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IEventHandler.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsBus.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsRegistry.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/EventDispatchTests.cs`

- [ ] **Step 1: Write failing event tests (dispatch order + no-subscriber no-op)**

```csharp
using System.Collections.Generic;
using Fun.Framework.Cqrs;
using NUnit.Framework;

namespace Fun.Framework.Tests
{
    public class EventDispatchTests
    {
        private readonly struct DamageAppliedEvent : IEvent
        {
            public DamageAppliedEvent(int amount) => Amount = amount;
            public int Amount { get; }
        }

        private sealed class OrderedEventHandler : IEventHandler<DamageAppliedEvent>
        {
            private readonly int _id;
            private readonly List<int> _order;

            public OrderedEventHandler(int id, List<int> order)
            {
                _id = id;
                _order = order;
            }

            public void Handle(in DamageAppliedEvent evt)
            {
                _order.Add(_id);
            }
        }

        [Test]
        public void Publish_InvokesSubscribersInRegistrationOrder()
        {
            var order = new List<int>();
            var bus = new CqrsBus();

            bus.Subscribe(new OrderedEventHandler(1, order));
            bus.Subscribe(new OrderedEventHandler(2, order));
            bus.Freeze();

            bus.Publish(new DamageAppliedEvent(10));

            CollectionAssert.AreEqual(new[] { 1, 2 }, order);
        }

        [Test]
        public void Publish_WithoutSubscribers_DoesNothing()
        {
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.DoesNotThrow(() => bus.Publish(new DamageAppliedEvent(5)));
        }
    }
}
```

- [ ] **Step 2: Run event tests to verify they fail**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.EventDispatchTests" \
  -logFile -
```

Expected: FAIL because event abstractions and publish/subscribe bus methods are missing.

- [ ] **Step 3: Implement event abstractions and event routing in bus**

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IEvent.cs`

```csharp
namespace Fun.Framework.Cqrs
{
    public interface IEvent
    {
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IEventHandler.cs`

```csharp
namespace Fun.Framework.Cqrs
{
    public interface IEventHandler<TEvent>
        where TEvent : struct, IEvent
    {
        void Handle(in TEvent evt);
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsBus.cs`

```csharp
namespace Fun.Framework.Cqrs
{
    public interface ICqrsBus
    {
        void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand;

        TResult Query<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>;

        void Publish<TEvent>(in TEvent evt)
            where TEvent : struct, IEvent;
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsRegistry.cs`

```csharp
namespace Fun.Framework.Cqrs
{
    public interface ICqrsRegistry
    {
        void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand;

        void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>;

        void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent;

        void Freeze();
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Fun.Framework.Cqrs
{
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry
    {
        private readonly struct QueryKey : IEquatable<QueryKey>
        {
            public QueryKey(Type queryType, Type resultType)
            {
                QueryType = queryType;
                ResultType = resultType;
            }

            public Type QueryType { get; }
            public Type ResultType { get; }

            public bool Equals(QueryKey other)
            {
                return QueryType == other.QueryType && ResultType == other.ResultType;
            }

            public override bool Equals(object obj)
            {
                return obj is QueryKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((QueryType != null ? QueryType.GetHashCode() : 0) * 397) ^
                           (ResultType != null ? ResultType.GetHashCode() : 0);
                }
            }
        }

        private readonly Dictionary<Type, object> _commandHandlers = new();
        private readonly Dictionary<QueryKey, object> _queryHandlers = new();
        private readonly Dictionary<Type, object> _eventHandlers = new();
        private bool _isFrozen;

        public void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureNotFrozen();

            var key = typeof(TCommand);
            if (_commandHandlers.ContainsKey(key))
            {
                throw new InvalidOperationException($"Command handler already registered: {key.FullName}");
            }

            _commandHandlers[key] = handler;
        }

        public void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureNotFrozen();

            var key = new QueryKey(typeof(TQuery), typeof(TResult));
            if (_queryHandlers.ContainsKey(key))
            {
                throw new InvalidOperationException($"Query handler already registered: {typeof(TQuery).FullName}");
            }

            _queryHandlers[key] = handler;
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureNotFrozen();

            var key = typeof(TEvent);
            if (_eventHandlers.TryGetValue(key, out var boxedHandlers))
            {
                var oldHandlers = (IEventHandler<TEvent>[])boxedHandlers;
                var newHandlers = new IEventHandler<TEvent>[oldHandlers.Length + 1];
                Array.Copy(oldHandlers, newHandlers, oldHandlers.Length);
                newHandlers[oldHandlers.Length] = handler;
                _eventHandlers[key] = newHandlers;
                return;
            }

            _eventHandlers[key] = new[] { handler };
        }

        public void Freeze()
        {
            _isFrozen = true;
        }

        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            if (!_commandHandlers.TryGetValue(typeof(TCommand), out var boxedHandler))
            {
                throw new InvalidOperationException($"Command handler not registered: {typeof(TCommand).FullName}");
            }

            ((ICommandHandler<TCommand>)boxedHandler).Handle(in command);
        }

        public TResult Query<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            var key = new QueryKey(typeof(TQuery), typeof(TResult));
            if (!_queryHandlers.TryGetValue(key, out var boxedHandler))
            {
                throw new InvalidOperationException($"Query handler not registered: {typeof(TQuery).FullName}");
            }

            return ((IQueryHandler<TQuery, TResult>)boxedHandler).Handle(in query);
        }

        public void Publish<TEvent>(in TEvent evt)
            where TEvent : struct, IEvent
        {
            if (!_eventHandlers.TryGetValue(typeof(TEvent), out var boxedHandlers))
            {
                return;
            }

            var handlers = (IEventHandler<TEvent>[])boxedHandlers;
            for (var i = 0; i < handlers.Length; i++)
            {
                handlers[i].Handle(in evt);
            }
        }

        private void EnsureNotFrozen()
        {
            if (_isFrozen)
            {
                throw new InvalidOperationException("Registry is frozen.");
            }
        }
    }
}
```

- [ ] **Step 4: Run command/query/event tests to verify they pass**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.CommandDispatchTests|Fun.Framework.Tests.QueryDispatchTests|Fun.Framework.Tests.EventDispatchTests" \
  -logFile -
```

Expected: PASS for all three test classes.

- [ ] **Step 5: Commit Task 3**

```bash
git add \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IEvent.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/IEventHandler.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsBus.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Abstractions/ICqrsRegistry.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/EventDispatchTests.cs

git commit -m "feat(framework): add CQRS event publish-subscribe"
```

---

### Task 4: Add explicit framework exceptions and registration guards

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Exceptions/DuplicateRegistrationException.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Exceptions/HandlerNotRegisteredException.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Exceptions/RegistryFrozenException.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/RegistrationGuardTests.cs`

- [ ] **Step 1: Write failing guard/exception tests**

```csharp
using Fun.Framework.Cqrs;
using NUnit.Framework;

namespace Fun.Framework.Tests
{
    public class RegistrationGuardTests
    {
        private readonly struct TestCommand : ICommand
        {
        }

        private readonly struct TestQuery : IQuery<int>
        {
        }

        private readonly struct TestEvent : IEvent
        {
        }

        private sealed class TestCommandHandler : ICommandHandler<TestCommand>
        {
            public void Handle(in TestCommand command)
            {
            }
        }

        private sealed class TestQueryHandler : IQueryHandler<TestQuery, int>
        {
            public int Handle(in TestQuery query)
            {
                return 1;
            }
        }

        private sealed class TestEventHandler : IEventHandler<TestEvent>
        {
            public void Handle(in TestEvent evt)
            {
            }
        }

        [Test]
        public void Send_WithoutHandler_ThrowsHandlerNotRegisteredException()
        {
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.Throws<HandlerNotRegisteredException>(() => bus.Send(new TestCommand()));
        }

        [Test]
        public void Query_WithoutHandler_ThrowsHandlerNotRegisteredException()
        {
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.Throws<HandlerNotRegisteredException>(() => bus.Query<TestQuery, int>(new TestQuery()));
        }

        [Test]
        public void RegisterCommand_Twice_ThrowsDuplicateRegistrationException()
        {
            var bus = new CqrsBus();
            bus.RegisterCommand(new TestCommandHandler());

            Assert.Throws<DuplicateRegistrationException>(() => bus.RegisterCommand(new TestCommandHandler()));
        }

        [Test]
        public void RegisterQuery_Twice_ThrowsDuplicateRegistrationException()
        {
            var bus = new CqrsBus();
            bus.RegisterQuery(new TestQueryHandler());

            Assert.Throws<DuplicateRegistrationException>(() => bus.RegisterQuery(new TestQueryHandler()));
        }

        [Test]
        public void RegisterAfterFreeze_ThrowsRegistryFrozenException()
        {
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.Throws<RegistryFrozenException>(() => bus.RegisterCommand(new TestCommandHandler()));
            Assert.Throws<RegistryFrozenException>(() => bus.RegisterQuery(new TestQueryHandler()));
            Assert.Throws<RegistryFrozenException>(() => bus.Subscribe(new TestEventHandler()));
        }
    }
}
```

- [ ] **Step 2: Run guard tests to verify they fail**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.RegistrationGuardTests" \
  -logFile -
```

Expected: FAIL because bus currently throws generic `InvalidOperationException`.

- [ ] **Step 3: Implement explicit exception types and wire them into the bus**

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Exceptions/DuplicateRegistrationException.cs`

```csharp
using System;

namespace Fun.Framework.Cqrs
{
    public sealed class DuplicateRegistrationException : InvalidOperationException
    {
        public DuplicateRegistrationException(string message) : base(message)
        {
        }
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Exceptions/HandlerNotRegisteredException.cs`

```csharp
using System;

namespace Fun.Framework.Cqrs
{
    public sealed class HandlerNotRegisteredException : InvalidOperationException
    {
        public HandlerNotRegisteredException(string message) : base(message)
        {
        }
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Exceptions/RegistryFrozenException.cs`

```csharp
using System;

namespace Fun.Framework.Cqrs
{
    public sealed class RegistryFrozenException : InvalidOperationException
    {
        public RegistryFrozenException(string message) : base(message)
        {
        }
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Fun.Framework.Cqrs
{
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry
    {
        private readonly struct QueryKey : IEquatable<QueryKey>
        {
            public QueryKey(Type queryType, Type resultType)
            {
                QueryType = queryType;
                ResultType = resultType;
            }

            public Type QueryType { get; }
            public Type ResultType { get; }

            public bool Equals(QueryKey other)
            {
                return QueryType == other.QueryType && ResultType == other.ResultType;
            }

            public override bool Equals(object obj)
            {
                return obj is QueryKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((QueryType != null ? QueryType.GetHashCode() : 0) * 397) ^
                           (ResultType != null ? ResultType.GetHashCode() : 0);
                }
            }
        }

        private readonly Dictionary<Type, object> _commandHandlers = new();
        private readonly Dictionary<QueryKey, object> _queryHandlers = new();
        private readonly Dictionary<Type, object> _eventHandlers = new();
        private bool _isFrozen;

        public void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureNotFrozen();

            var key = typeof(TCommand);
            if (_commandHandlers.ContainsKey(key))
            {
                throw new DuplicateRegistrationException($"Command handler already registered: {key.FullName}");
            }

            _commandHandlers[key] = handler;
        }

        public void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureNotFrozen();

            var key = new QueryKey(typeof(TQuery), typeof(TResult));
            if (_queryHandlers.ContainsKey(key))
            {
                throw new DuplicateRegistrationException($"Query handler already registered: {typeof(TQuery).FullName}");
            }

            _queryHandlers[key] = handler;
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureNotFrozen();

            var key = typeof(TEvent);
            if (_eventHandlers.TryGetValue(key, out var boxedHandlers))
            {
                var oldHandlers = (IEventHandler<TEvent>[])boxedHandlers;
                var newHandlers = new IEventHandler<TEvent>[oldHandlers.Length + 1];
                Array.Copy(oldHandlers, newHandlers, oldHandlers.Length);
                newHandlers[oldHandlers.Length] = handler;
                _eventHandlers[key] = newHandlers;
                return;
            }

            _eventHandlers[key] = new[] { handler };
        }

        public void Freeze()
        {
            _isFrozen = true;
        }

        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            if (!_commandHandlers.TryGetValue(typeof(TCommand), out var boxedHandler))
            {
                throw new HandlerNotRegisteredException($"Command handler not registered: {typeof(TCommand).FullName}");
            }

            ((ICommandHandler<TCommand>)boxedHandler).Handle(in command);
        }

        public TResult Query<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            var key = new QueryKey(typeof(TQuery), typeof(TResult));
            if (!_queryHandlers.TryGetValue(key, out var boxedHandler))
            {
                throw new HandlerNotRegisteredException($"Query handler not registered: {typeof(TQuery).FullName}");
            }

            return ((IQueryHandler<TQuery, TResult>)boxedHandler).Handle(in query);
        }

        public void Publish<TEvent>(in TEvent evt)
            where TEvent : struct, IEvent
        {
            if (!_eventHandlers.TryGetValue(typeof(TEvent), out var boxedHandlers))
            {
                return;
            }

            var handlers = (IEventHandler<TEvent>[])boxedHandlers;
            for (var i = 0; i < handlers.Length; i++)
            {
                handlers[i].Handle(in evt);
            }
        }

        private void EnsureNotFrozen()
        {
            if (_isFrozen)
            {
                throw new RegistryFrozenException("CQRS registry is frozen and cannot be modified.");
            }
        }
    }
}
```

- [ ] **Step 4: Run all behavior tests to verify pass**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.CommandDispatchTests|Fun.Framework.Tests.QueryDispatchTests|Fun.Framework.Tests.EventDispatchTests|Fun.Framework.Tests.RegistrationGuardTests" \
  -logFile -
```

Expected: PASS for all listed test classes.

- [ ] **Step 5: Commit Task 4**

```bash
git add \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Exceptions/DuplicateRegistrationException.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Exceptions/HandlerNotRegisteredException.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Exceptions/RegistryFrozenException.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/RegistrationGuardTests.cs

git commit -m "feat(framework): add explicit CQRS guard exceptions"
```

---

### Task 5: Add optional diagnostics and enforce hot-path 0GC with tests

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/ICqrsLogger.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/NullCqrsLogger.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/ZeroAllocationDispatchTests.cs`

- [ ] **Step 1: Write allocation characterization tests (send/query/publish)**

```csharp
using System;
using Fun.Framework.Cqrs;
using NUnit.Framework;

namespace Fun.Framework.Tests
{
    public class ZeroAllocationDispatchTests
    {
        private readonly struct TickCommand : ICommand
        {
            public TickCommand(int delta) => Delta = delta;
            public int Delta { get; }
        }

        private readonly struct GetTickQuery : IQuery<int>
        {
        }

        private readonly struct TickEvent : IEvent
        {
            public TickEvent(int delta) => Delta = delta;
            public int Delta { get; }
        }

        private sealed class TickState
        {
            public int Value;
        }

        private sealed class TickCommandHandler : ICommandHandler<TickCommand>
        {
            private readonly TickState _state;

            public TickCommandHandler(TickState state)
            {
                _state = state;
            }

            public void Handle(in TickCommand command)
            {
                _state.Value += command.Delta;
            }
        }

        private sealed class TickQueryHandler : IQueryHandler<GetTickQuery, int>
        {
            private readonly TickState _state;

            public TickQueryHandler(TickState state)
            {
                _state = state;
            }

            public int Handle(in GetTickQuery query)
            {
                return _state.Value;
            }
        }

        private sealed class TickEventHandler : IEventHandler<TickEvent>
        {
            public int Count;

            public void Handle(in TickEvent evt)
            {
                Count += evt.Delta;
            }
        }

        [Test]
        public void Send_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var state = new TickState();
            var bus = new CqrsBus();
            bus.RegisterCommand(new TickCommandHandler(state));
            bus.Freeze();

            var cmd = new TickCommand(1);
            for (var i = 0; i < 1000; i++) bus.Send(in cmd);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100000; i++) bus.Send(in cmd);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
        }

        [Test]
        public void Query_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var state = new TickState { Value = 7 };
            var bus = new CqrsBus();
            bus.RegisterQuery(new TickQueryHandler(state));
            bus.Freeze();

            var query = new GetTickQuery();
            for (var i = 0; i < 1000; i++) bus.Query<GetTickQuery, int>(in query);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100000; i++) bus.Query<GetTickQuery, int>(in query);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
        }

        [Test]
        public void Publish_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var handler = new TickEventHandler();
            var bus = new CqrsBus();
            bus.Subscribe(handler);
            bus.Freeze();

            var evt = new TickEvent(1);
            for (var i = 0; i < 1000; i++) bus.Publish(in evt);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100000; i++) bus.Publish(in evt);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
        }
    }
}
```

- [ ] **Step 2: Run allocation tests to verify current behavior**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.ZeroAllocationDispatchTests" \
  -logFile -
```

Expected: if any dispatch path allocates, one or more tests fail with `Assert.AreEqual(before, after)` mismatch.

- [ ] **Step 3: Add no-op diagnostics abstraction and finalize no-allocation dispatch internals**

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/ICqrsLogger.cs`

```csharp
using System;

namespace Fun.Framework.Cqrs
{
    public interface ICqrsLogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception exception);
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/NullCqrsLogger.cs`

```csharp
using System;

namespace Fun.Framework.Cqrs
{
    public sealed class NullCqrsLogger : ICqrsLogger
    {
        public static readonly NullCqrsLogger Instance = new NullCqrsLogger();

        private NullCqrsLogger()
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message, Exception exception)
        {
        }
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Fun.Framework.Cqrs
{
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry
    {
        private readonly struct QueryKey : IEquatable<QueryKey>
        {
            public QueryKey(Type queryType, Type resultType)
            {
                QueryType = queryType;
                ResultType = resultType;
            }

            public Type QueryType { get; }
            public Type ResultType { get; }

            public bool Equals(QueryKey other)
            {
                return QueryType == other.QueryType && ResultType == other.ResultType;
            }

            public override bool Equals(object obj)
            {
                return obj is QueryKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((QueryType != null ? QueryType.GetHashCode() : 0) * 397) ^
                           (ResultType != null ? ResultType.GetHashCode() : 0);
                }
            }
        }

        private readonly Dictionary<Type, object> _commandHandlers;
        private readonly Dictionary<QueryKey, object> _queryHandlers;
        private readonly Dictionary<Type, object> _eventHandlers;
        private readonly ICqrsLogger _logger;
        private bool _isFrozen;

        public CqrsBus(ICqrsLogger logger = null)
        {
            _commandHandlers = new Dictionary<Type, object>(32);
            _queryHandlers = new Dictionary<QueryKey, object>(32);
            _eventHandlers = new Dictionary<Type, object>(32);
            _logger = logger ?? NullCqrsLogger.Instance;
        }

        public void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureNotFrozen();

            var key = typeof(TCommand);
            if (_commandHandlers.ContainsKey(key))
            {
                throw new DuplicateRegistrationException($"Command handler already registered: {key.FullName}");
            }

            _commandHandlers[key] = handler;
            _logger.Info("Registered command handler.");
        }

        public void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureNotFrozen();

            var key = new QueryKey(typeof(TQuery), typeof(TResult));
            if (_queryHandlers.ContainsKey(key))
            {
                throw new DuplicateRegistrationException($"Query handler already registered: {typeof(TQuery).FullName}");
            }

            _queryHandlers[key] = handler;
            _logger.Info("Registered query handler.");
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            EnsureNotFrozen();

            var key = typeof(TEvent);
            if (_eventHandlers.TryGetValue(key, out var boxedHandlers))
            {
                var oldHandlers = (IEventHandler<TEvent>[])boxedHandlers;
                var newHandlers = new IEventHandler<TEvent>[oldHandlers.Length + 1];
                Array.Copy(oldHandlers, newHandlers, oldHandlers.Length);
                newHandlers[oldHandlers.Length] = handler;
                _eventHandlers[key] = newHandlers;
                return;
            }

            _eventHandlers[key] = new IEventHandler<TEvent>[] { handler };
        }

        public void Freeze()
        {
            _isFrozen = true;
        }

        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            if (!_commandHandlers.TryGetValue(typeof(TCommand), out var boxedHandler))
            {
                throw new HandlerNotRegisteredException($"Command handler not registered: {typeof(TCommand).FullName}");
            }

            ((ICommandHandler<TCommand>)boxedHandler).Handle(in command);
        }

        public TResult Query<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            var key = new QueryKey(typeof(TQuery), typeof(TResult));
            if (!_queryHandlers.TryGetValue(key, out var boxedHandler))
            {
                throw new HandlerNotRegisteredException($"Query handler not registered: {typeof(TQuery).FullName}");
            }

            return ((IQueryHandler<TQuery, TResult>)boxedHandler).Handle(in query);
        }

        public void Publish<TEvent>(in TEvent evt)
            where TEvent : struct, IEvent
        {
            if (!_eventHandlers.TryGetValue(typeof(TEvent), out var boxedHandlers))
            {
                return;
            }

            var handlers = (IEventHandler<TEvent>[])boxedHandlers;
            for (var i = 0; i < handlers.Length; i++)
            {
                handlers[i].Handle(in evt);
            }
        }

        private void EnsureNotFrozen()
        {
            if (_isFrozen)
            {
                throw new RegistryFrozenException("CQRS registry is frozen and cannot be modified.");
            }
        }
    }
}
```

- [ ] **Step 4: Run full EditMode suite to verify functional + allocation gates pass**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests" \
  -logFile -
```

Expected: PASS for command/query/event/guard/allocation tests.

- [ ] **Step 5: Commit Task 5**

```bash
git add \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/ICqrsLogger.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/NullCqrsLogger.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/ZeroAllocationDispatchTests.cs

git commit -m "feat(framework): add CQRS diagnostics and 0GC dispatch tests"
```

---

### Task 6: Add framework usage guide and representative demo snippets

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/README.md`

- [ ] **Step 1: Write framework README with startup wiring and message examples**

`UnityProject/Assets/Fun/Framework/README.md`

````markdown
# Fun.Framework CQRS

Minimal, zero-dependency, synchronous CQRS foundation for Unity.

## What this includes

- Command dispatch: `Send<TCommand>(in TCommand)`
- Query dispatch: `Query<TQuery, TResult>(in TQuery)`
- Event publish/subscribe: `Publish<TEvent>(in TEvent)` + `Subscribe<TEvent>(handler)`
- Explicit manual registration and `Freeze()` lifecycle

## Core constraints

- Messages are `struct` (`ICommand`, `IQuery<TResult>`, `IEvent`)
- No runtime reflection scan
- No async API in MVP
- Hot-path allocation target: 0GC after warm-up

## Bootstrap example

```csharp
using Fun.Framework.Cqrs;

public static class GameCqrsBootstrap
{
    public static ICqrsBus Build()
    {
        var bus = new CqrsBus();

        bus.RegisterCommand(new MovePlayerCommandHandler());
        bus.RegisterQuery(new GetPlayerHpQueryHandler());
        bus.Subscribe(new DamageAppliedEventHandler());

        bus.Freeze();
        return bus;
    }
}
```

## Message examples

```csharp
public readonly struct MovePlayerCommand : ICommand
{
    public MovePlayerCommand(int playerId, int x, int y)
    {
        PlayerId = playerId;
        X = x;
        Y = y;
    }

    public int PlayerId { get; }
    public int X { get; }
    public int Y { get; }
}

public readonly struct GetPlayerHpQuery : IQuery<int>
{
    public GetPlayerHpQuery(int playerId)
    {
        PlayerId = playerId;
    }

    public int PlayerId { get; }
}

public readonly struct DamageAppliedEvent : IEvent
{
    public DamageAppliedEvent(int playerId, int amount)
    {
        PlayerId = playerId;
        Amount = amount;
    }

    public int PlayerId { get; }
    public int Amount { get; }
}
```

## Failure semantics

- Missing command/query handler: `HandlerNotRegisteredException`
- Duplicate command/query registration: `DuplicateRegistrationException`
- Mutation after `Freeze()`: `RegistryFrozenException`
- Event publish with no subscribers: no-op
````

- [ ] **Step 2: Quick validation run to ensure documentation examples align with API names**

Run:

```bash
rg -n "RegisterCommand|RegisterQuery|Subscribe|Freeze|Send|Query|Publish|HandlerNotRegisteredException|DuplicateRegistrationException|RegistryFrozenException" \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs \
  UnityProject/Assets/Fun/Framework/README.md
```

Expected: method and type names match between code and docs.

- [ ] **Step 3: Commit Task 6**

```bash
git add UnityProject/Assets/Fun/Framework/README.md

git commit -m "docs(framework): add CQRS usage and bootstrap guide"
```

---

## Final Verification Checklist

- [ ] `Fun.Framework` asmdef compiles in Unity Editor.
- [ ] All EditMode tests in `Fun.Framework.Tests` pass.
- [ ] Allocation tests pass with zero deltas after warm-up.
- [ ] API names in README match actual interfaces/classes.
- [ ] No runtime reflection scanning introduced.

## Spec Coverage Self-Review

- **Spec coverage:**
  - Command/Query/Event contracts and dispatch: Tasks 1-3
  - Manual registration + freeze lifecycle: Tasks 1-4
  - Explicit error semantics: Task 4
  - Hot-path 0GC acceptance gate: Task 5
  - Usage/demo documentation: Task 6
- **Placeholder scan:** No unresolved placeholders or deferred implementation notes present.
- **Type consistency:** `ICommand`, `IQuery<TResult>`, `IEvent`, `CqrsBus`, and exception type names are consistent across all tasks.
