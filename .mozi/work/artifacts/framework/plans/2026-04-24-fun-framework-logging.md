# Fun.Framework Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a simplicity-first, engine-agnostic logging module under `Fun.Framework.Logging`, then migrate `CqrsBus` from CQRS-local logger types to the shared logger abstraction.

**Architecture:** Add a tiny runtime logging core (`LogLevel`, `ILogger`, `ILogSink`, `NullLogger`, `LogRouter`) with register-then-freeze lifecycle, per-sink `minLevel` filtering, and sink exception isolation. Keep Unity/file output implementations outside framework core. Migrate `CqrsBus` constructor injection to `Fun.Framework.Logging.ILogger` while preserving current CQRS behavior.

**Tech Stack:** C# (Unity 2022.3.60f1), Unity asmdef (`Fun.Framework` + `Fun.Framework.Tests`), NUnit EditMode tests.

---

## Scope Check

This spec is one subsystem (framework logging core + CQRS logger unification). No decomposition is required.

## File Structure (Create/Modify Map)

### Runtime files

- Create: `UnityProject/Assets/Fun/Framework/Runtime/Logging/LogLevel.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Logging/ILogger.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Logging/ILogSink.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Logging/NullLogger.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Logging/LogRouter.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs`
- Delete: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/ICqrsLogger.cs`
- Delete: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/NullCqrsLogger.cs`
- Modify: `UnityProject/Assets/Fun/Framework/README.md`

### Test files

- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Logging/LogRouterCoreTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Logging/LogRouterResilienceTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/CqrsLoggingIntegrationTests.cs`

### Shared test command

```bash
UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.60f1/Unity.app/Contents/MacOS/Unity"
```

Use this command shape (project rule: do not add `-quit` with `-runTests`):

```bash
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "<Filter>" \
  -testResults "$(pwd)/UnityProject/Logs/<name>.xml" \
  -logFile -
```

---

### Task 1: Create logging API contracts and pass core routing tests

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Logging/LogRouterCoreTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Logging/LogLevel.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Logging/ILogger.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Logging/ILogSink.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Logging/NullLogger.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Logging/LogRouter.cs`

- [ ] **Step 1: Write the failing core tests**

`UnityProject/Assets/Fun/Framework/Tests/EditMode/Logging/LogRouterCoreTests.cs`

```csharp
using System;
using System.Collections.Generic;
using Fun.Framework.Logging;
using NUnit.Framework;

namespace Fun.Framework.Tests.Logging
{
    public class LogRouterCoreTests
    {
        private sealed class RecordingSink : ILogSink
        {
            public readonly List<(LogLevel Level, string Message)> Entries = new List<(LogLevel, string)>();

            public void Write(LogLevel level, string message)
            {
                Entries.Add((level, message));
            }
        }

        [Test]
        public void AddSink_NullSink_ThrowsArgumentNullException()
        {
            var router = new LogRouter();

            Assert.Throws<ArgumentNullException>(() => router.AddSink(null, LogLevel.Debug));
        }

        [Test]
        public void Info_WhenLevelIsEnabled_ForwardsToSink()
        {
            var router = new LogRouter();
            var sink = new RecordingSink();
            router.AddSink(sink, LogLevel.Info);
            router.Freeze();

            router.Info("ready");

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreEqual(LogLevel.Info, sink.Entries[0].Level);
            Assert.AreEqual("ready", sink.Entries[0].Message);
        }

        [Test]
        public void Debug_WhenMinLevelIsInfo_DoesNotForward()
        {
            var router = new LogRouter();
            var sink = new RecordingSink();
            router.AddSink(sink, LogLevel.Info);
            router.Freeze();

            router.Debug("hidden");

            Assert.AreEqual(0, sink.Entries.Count);
        }

        [Test]
        public void AddSink_AfterFreeze_ThrowsInvalidOperationException()
        {
            var router = new LogRouter();
            router.Freeze();

            Assert.Throws<InvalidOperationException>(() => router.AddSink(new RecordingSink(), LogLevel.Debug));
        }

        [Test]
        public void NullLogger_AllMethods_DoNotThrow()
        {
            var logger = NullLogger.Instance;

            Assert.DoesNotThrow(() => logger.Debug("d"));
            Assert.DoesNotThrow(() => logger.Info("i"));
            Assert.DoesNotThrow(() => logger.Warn("w"));
            Assert.DoesNotThrow(() => logger.Error("e"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.Logging.LogRouterCoreTests" \
  -testResults "$(pwd)/UnityProject/Logs/log-router-core-fail.xml" \
  -logFile -
```

Expected: FAIL with compile errors because `Fun.Framework.Logging` types do not exist.

- [ ] **Step 3: Implement minimal logging runtime types**

`UnityProject/Assets/Fun/Framework/Runtime/Logging/LogLevel.cs`

```csharp
namespace Fun.Framework.Logging
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Logging/ILogger.cs`

```csharp
namespace Fun.Framework.Logging
{
    public interface ILogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Logging/ILogSink.cs`

```csharp
namespace Fun.Framework.Logging
{
    public interface ILogSink
    {
        void Write(LogLevel level, string message);
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Logging/NullLogger.cs`

```csharp
namespace Fun.Framework.Logging
{
    public sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger()
        {
        }

        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message)
        {
        }
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Logging/LogRouter.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Fun.Framework.Logging
{
    public sealed class LogRouter : ILogger
    {
        private readonly struct SinkRegistration
        {
            public SinkRegistration(ILogSink sink, LogLevel minLevel)
            {
                Sink = sink;
                MinLevel = minLevel;
            }

            public ILogSink Sink { get; }
            public LogLevel MinLevel { get; }
        }

        private readonly List<SinkRegistration> _registrations = new List<SinkRegistration>();
        private bool _isFrozen;

        public void AddSink(ILogSink sink, LogLevel minLevel)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            if (_isFrozen)
            {
                throw new InvalidOperationException("Log router is frozen.");
            }

            _registrations.Add(new SinkRegistration(sink, minLevel));
        }

        public void Freeze()
        {
            _isFrozen = true;
        }

        public void Debug(string message)
        {
            Write(LogLevel.Debug, message);
        }

        public void Info(string message)
        {
            Write(LogLevel.Info, message);
        }

        public void Warn(string message)
        {
            Write(LogLevel.Warn, message);
        }

        public void Error(string message)
        {
            Write(LogLevel.Error, message);
        }

        private void Write(LogLevel level, string message)
        {
            var normalizedMessage = message ?? string.Empty;

            for (var i = 0; i < _registrations.Count; i++)
            {
                var registration = _registrations[i];
                if (level < registration.MinLevel)
                {
                    continue;
                }

                registration.Sink.Write(level, normalizedMessage);
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.Logging.LogRouterCoreTests" \
  -testResults "$(pwd)/UnityProject/Logs/log-router-core-pass.xml" \
  -logFile -
```

Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add \
  UnityProject/Assets/Fun/Framework/Runtime/Logging/LogLevel.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Logging/ILogger.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Logging/ILogSink.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Logging/NullLogger.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Logging/LogRouter.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Logging/LogRouterCoreTests.cs
git commit -m "feat(logging): add core logger contracts and router"
```

---

### Task 2: Add resilience semantics tests (multi-sink, swallow exceptions, idempotent freeze)

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Logging/LogRouterResilienceTests.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Runtime/Logging/LogRouter.cs`

- [ ] **Step 1: Write failing resilience tests**

`UnityProject/Assets/Fun/Framework/Tests/EditMode/Logging/LogRouterResilienceTests.cs`

```csharp
using System;
using System.Collections.Generic;
using Fun.Framework.Logging;
using NUnit.Framework;

namespace Fun.Framework.Tests.Logging
{
    public class LogRouterResilienceTests
    {
        private sealed class RecordingSink : ILogSink
        {
            public readonly List<string> Entries = new List<string>();

            public void Write(LogLevel level, string message)
            {
                Entries.Add(level + ":" + message);
            }
        }

        private sealed class ThrowingSink : ILogSink
        {
            public int Calls;

            public void Write(LogLevel level, string message)
            {
                Calls++;
                throw new InvalidOperationException("sink failure");
            }
        }

        [Test]
        public void Write_WhenOneSinkThrows_StillWritesToLaterSink()
        {
            var router = new LogRouter();
            var first = new ThrowingSink();
            var second = new RecordingSink();
            router.AddSink(first, LogLevel.Debug);
            router.AddSink(second, LogLevel.Debug);
            router.Freeze();

            Assert.DoesNotThrow(() => router.Error("boom"));
            Assert.AreEqual(1, first.Calls);
            Assert.AreEqual(1, second.Entries.Count);
            Assert.AreEqual("Error:boom", second.Entries[0]);
        }

        [Test]
        public void Freeze_CanBeCalledMoreThanOnce()
        {
            var router = new LogRouter();

            Assert.DoesNotThrow(() => router.Freeze());
            Assert.DoesNotThrow(() => router.Freeze());
        }

        [Test]
        public void NullMessage_IsNormalizedToEmptyString()
        {
            var router = new LogRouter();
            var sink = new RecordingSink();
            router.AddSink(sink, LogLevel.Debug);
            router.Freeze();

            router.Warn(null);

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreEqual("Warn:", sink.Entries[0]);
        }

        [Test]
        public void MultiSink_UsesPerSinkMinLevelFiltering()
        {
            var router = new LogRouter();
            var debugSink = new RecordingSink();
            var warnSink = new RecordingSink();
            router.AddSink(debugSink, LogLevel.Debug);
            router.AddSink(warnSink, LogLevel.Warn);
            router.Freeze();

            router.Info("i");
            router.Error("e");

            CollectionAssert.AreEqual(new[] { "Info:i", "Error:e" }, debugSink.Entries);
            CollectionAssert.AreEqual(new[] { "Error:e" }, warnSink.Entries);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.Logging.LogRouterResilienceTests" \
  -testResults "$(pwd)/UnityProject/Logs/log-router-resilience-fail.xml" \
  -logFile -
```

Expected: FAIL on `Write_WhenOneSinkThrows_StillWritesToLaterSink` because current router does not isolate sink exceptions.

- [ ] **Step 3: Update `LogRouter` to isolate sink failures and keep ordering semantics**

`UnityProject/Assets/Fun/Framework/Runtime/Logging/LogRouter.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Fun.Framework.Logging
{
    public sealed class LogRouter : ILogger
    {
        private readonly struct SinkRegistration
        {
            public SinkRegistration(ILogSink sink, LogLevel minLevel)
            {
                Sink = sink;
                MinLevel = minLevel;
            }

            public ILogSink Sink { get; }
            public LogLevel MinLevel { get; }
        }

        private readonly List<SinkRegistration> _registrations = new List<SinkRegistration>();
        private bool _isFrozen;

        public void AddSink(ILogSink sink, LogLevel minLevel)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            if (_isFrozen)
            {
                throw new InvalidOperationException("Log router is frozen.");
            }

            _registrations.Add(new SinkRegistration(sink, minLevel));
        }

        public void Freeze()
        {
            _isFrozen = true;
        }

        public void Debug(string message)
        {
            Write(LogLevel.Debug, message);
        }

        public void Info(string message)
        {
            Write(LogLevel.Info, message);
        }

        public void Warn(string message)
        {
            Write(LogLevel.Warn, message);
        }

        public void Error(string message)
        {
            Write(LogLevel.Error, message);
        }

        private void Write(LogLevel level, string message)
        {
            var normalizedMessage = message ?? string.Empty;

            for (var i = 0; i < _registrations.Count; i++)
            {
                var registration = _registrations[i];
                if (level < registration.MinLevel)
                {
                    continue;
                }

                try
                {
                    registration.Sink.Write(level, normalizedMessage);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.Logging.LogRouterCoreTests|Fun.Framework.Tests.Logging.LogRouterResilienceTests" \
  -testResults "$(pwd)/UnityProject/Logs/log-router-all-pass.xml" \
  -logFile -
```

Expected: PASS (9 tests).

- [ ] **Step 5: Commit**

```bash
git add \
  UnityProject/Assets/Fun/Framework/Runtime/Logging/LogRouter.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Logging/LogRouterResilienceTests.cs
git commit -m "test(logging): add resilience semantics for router"
```

---

### Task 3: Migrate CQRS from `ICqrsLogger` to shared `ILogger`

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/CqrsLoggingIntegrationTests.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs`
- Delete: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/ICqrsLogger.cs`
- Delete: `UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/NullCqrsLogger.cs`

- [ ] **Step 1: Write failing CQRS logging integration tests**

`UnityProject/Assets/Fun/Framework/Tests/EditMode/CqrsLoggingIntegrationTests.cs`

```csharp
using System;
using Fun.Framework.Cqrs;
using Fun.Framework.Logging;
using NUnit.Framework;

namespace Fun.Framework.Tests
{
    public class CqrsLoggingIntegrationTests
    {
        private readonly struct TestCommand : ICommand
        {
        }

        private sealed class TestCommandHandler : ICommandHandler<TestCommand>
        {
            public void Handle(in TestCommand command)
            {
            }
        }

        private sealed class RecordingLogger : ILogger
        {
            public int InfoCalls;

            public void Debug(string message)
            {
            }

            public void Info(string message)
            {
                InfoCalls++;
            }

            public void Warn(string message)
            {
            }

            public void Error(string message)
            {
            }
        }

        private sealed class ThrowingInfoLogger : ILogger
        {
            public void Debug(string message)
            {
            }

            public void Info(string message)
            {
                throw new InvalidOperationException("logger failed");
            }

            public void Warn(string message)
            {
            }

            public void Error(string message)
            {
            }
        }

        [Test]
        public void RegisterCommand_UsesInjectedLoggerInfo()
        {
            var logger = new RecordingLogger();
            var bus = new CqrsBus(logger);

            bus.RegisterCommand(new TestCommandHandler());

            Assert.AreEqual(1, logger.InfoCalls);
        }

        [Test]
        public void RegisterCommand_WhenLoggerThrows_DoesNotPropagate()
        {
            var bus = new CqrsBus(new ThrowingInfoLogger());

            Assert.DoesNotThrow(() => bus.RegisterCommand(new TestCommandHandler()));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.CqrsLoggingIntegrationTests" \
  -testResults "$(pwd)/UnityProject/Logs/cqrs-logging-fail.xml" \
  -logFile -
```

Expected: FAIL to compile because `CqrsBus` currently takes `ICqrsLogger`, not `Fun.Framework.Logging.ILogger`.

- [ ] **Step 3: Update CQRS logger dependency and remove CQRS-local logger types**

`UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs` (only changed lines shown)

```csharp
using Fun.Framework.Logging;

namespace Fun.Framework.Cqrs
{
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry
    {
        private readonly ILogger _logger;

        public CqrsBus()
            : this(NullLogger.Instance)
        {
        }

        public CqrsBus(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        private void SafeInfo(string message)
        {
            try
            {
                _logger.Info(message);
            }
            catch (Exception)
            {
            }
        }
    }
}
```

Delete files:

```text
UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/ICqrsLogger.cs
UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/NullCqrsLogger.cs
```

- [ ] **Step 4: Run CQRS + logging tests to verify no regression**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.CommandDispatchTests|Fun.Framework.Tests.QueryDispatchTests|Fun.Framework.Tests.EventDispatchTests|Fun.Framework.Tests.RegistrationGuardTests|Fun.Framework.Tests.ZeroAllocationDispatchTests|Fun.Framework.Tests.CqrsLoggingIntegrationTests|Fun.Framework.Tests.Logging.LogRouterCoreTests|Fun.Framework.Tests.Logging.LogRouterResilienceTests" \
  -testResults "$(pwd)/UnityProject/Logs/cqrs-logging-regression-pass.xml" \
  -logFile -
```

Expected: PASS for all listed test classes.

- [ ] **Step 5: Commit**

```bash
git add \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Core/CqrsBus.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/CqrsLoggingIntegrationTests.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/ICqrsLogger.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Cqrs/Diagnostics/NullCqrsLogger.cs
git commit -m "refactor(cqrs): migrate to shared framework logger"
```

---

### Task 4: Update framework docs and run final verification

**Files:**
- Modify: `UnityProject/Assets/Fun/Framework/README.md`

- [ ] **Step 1: Update README module list and usage examples for logging**

`UnityProject/Assets/Fun/Framework/README.md` updates:

```markdown
Current runtime modules in this directory:
- `Runtime/Cqrs`: synchronous CQRS bus with explicit registration and fail-fast dispatch.
- `Runtime/Collections`: pure-managed high-performance containers for hot-path gameplay loops.
- `Runtime/Pooling`: static generic object pooling for pure C# reusable objects.
- `Runtime/Logging`: engine-agnostic logger abstraction and multi-sink routing.
```

Add a new section:

```markdown
## Logging (`Fun.Framework.Logging`)

Core types:
- `ILogger`
- `ILogSink`
- `LogRouter`
- `NullLogger`
- `LogLevel` (`Debug`, `Info`, `Warn`, `Error`)

Usage:

```csharp
using Fun.Framework.Logging;

var router = new LogRouter();
router.AddSink(new MyRuntimeSink(), LogLevel.Info);
router.AddSink(new MyErrorSink(), LogLevel.Error);
router.Freeze();

ILogger logger = router;
logger.Info("bootstrap complete");
```

`MyRuntimeSink` / `MyErrorSink` are runtime adapter implementations outside `Fun.Framework` core.
```

Adjust CQRS note:

```markdown
- `CqrsBus` supports optional logger injection via `new CqrsBus(ILogger logger)`.
```

- [ ] **Step 2: Run documentation-impact regression tests**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.CommandDispatchTests|Fun.Framework.Tests.QueryDispatchTests|Fun.Framework.Tests.EventDispatchTests|Fun.Framework.Tests.RegistrationGuardTests|Fun.Framework.Tests.CqrsLoggingIntegrationTests|Fun.Framework.Tests.Logging.LogRouterCoreTests|Fun.Framework.Tests.Logging.LogRouterResilienceTests" \
  -testResults "$(pwd)/UnityProject/Logs/logging-readme-regression-pass.xml" \
  -logFile -
```

Expected: PASS for all listed test classes.

- [ ] **Step 3: Refresh code graph after runtime changes**

Run:

```bash
graphify update .
```

Expected: graph update completes successfully and refreshes `graphify-out/GRAPH_REPORT.md`.

- [ ] **Step 4: Commit**

```bash
git add \
  UnityProject/Assets/Fun/Framework/README.md \
  graphify-out/GRAPH_REPORT.md \
  graphify-out/graph.json \
  graphify-out/manifest.json
git commit -m "docs(logging): document framework logging module"
```

---

## Plan Self-Review

### 1) Spec coverage check

- Log core types and API (`LogLevel`, `ILogger`, `ILogSink`, `NullLogger`, `LogRouter`) -> Task 1.
- Multi-sink + per-sink minimum level filtering -> Task 1 + Task 2.
- Sink exception swallow policy -> Task 2.
- Register -> Freeze lifecycle, freeze idempotent, post-freeze registration blocked -> Task 1 + Task 2.
- Null message normalization -> Task 2.
- CQRS migration from `ICqrsLogger` to `ILogger` -> Task 3.
- Keep CQRS behavior/regression stable -> Task 3 test run.
- README updates for new module and CQRS ctor signature -> Task 4.
- Graph update requirement after code changes -> Task 4.

No spec gaps found.

### 2) Placeholder scan

- No `TBD`/`TODO` placeholders.
- Every code-changing step includes concrete code snippets or exact file deletion targets.
- Every verification step includes exact command and expected outcome.

### 3) Type/signature consistency

- `ILogger` method signatures are consistently `Debug/Info/Warn/Error(string message)`.
- `ILogSink` is consistently `Write(LogLevel level, string message)`.
- `CqrsBus` constructor target type is consistently `Fun.Framework.Logging.ILogger`.
- Tests use `Fun.Framework.Tests.Logging` namespace for logging tests and existing `Fun.Framework.Tests` for CQRS integration.

No naming/signature contradictions found.
