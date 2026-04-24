# Fun.Framework Logging Design (MVP)

- Date: 2026-04-24
- Project: `UnityProject/Assets/Fun/Framework`
- Scope: Framework-layer reusable logging foundation (business-agnostic, engine-agnostic)
- Status: Approved design baseline for planning

## 1. Background and Goals

`Fun.Framework` currently has CQRS-local logger abstractions (`ICqrsLogger`/`NullCqrsLogger`) but no framework-wide logging module.

This design defines a **minimal, unified logging core** that:

1. keeps framework code independent from Unity and business code,
2. is simple enough to use across Unity, other engines (for example Godot), and server runtime,
3. supports runtime pluggability through external sink implementations,
4. keeps failure semantics explicit and safe.

Primary goals:

1. **Simplicity-first API**: small surface area, low cognitive overhead.
2. **Engine-agnostic core**: no Unity API dependency in framework logging module.
3. **Instance-based usage**: no static global logger in MVP.
4. **Unified abstraction**: replace CQRS-local logger abstraction with shared `ILogger`.
5. **Runtime resilience**: sink failures must not break gameplay/server flow.

## 2. Non-goals (Out of Scope)

To preserve simplicity and keep boundaries clear, MVP excludes:

- built-in Unity console sink
- built-in file sink
- structured key-value payloads
- message-template formatting APIs
- async logging pipeline
- thread-safe multi-writer guarantees
- global static logger facade
- log categories or logger factory abstraction

External runtimes can implement sinks for editor console, files, remote systems, etc., without adding dependencies to `Fun.Framework`.

## 3. Decisions and Constraints

Confirmed decisions:

1. Module is implemented under `Fun/Framework/Runtime` only.
2. Logging levels are fixed to `Debug/Info/Warn/Error`.
3. API accepts `string` message only in MVP.
4. Entry is instance-based (`ILogger` injection), not static global.
5. Router supports **multiple sinks + per-sink minimum level filtering**.
6. Sink `Write` exceptions are swallowed and do not propagate.
7. Registration lifecycle follows framework conventions: explicit registration, then `Freeze()`.

## 4. Architecture Overview

## 4.1 Component Map

New module namespace: `Fun.Framework.Logging`

Target folder:

- `Runtime/Logging/`
  - `LogLevel.cs`
  - `ILogger.cs`
  - `ILogSink.cs`
  - `NullLogger.cs`
  - `LogRouter.cs`

Core responsibilities:

1. **`LogLevel`**
   - `Debug`, `Info`, `Warn`, `Error`
2. **`ILogger`**
   - framework-facing logger contract for callers
3. **`ILogSink`**
   - sink contract for runtime-specific outputs
4. **`NullLogger`**
   - no-op default implementation
5. **`LogRouter`**
   - sink registration + freeze lifecycle + filtering + dispatch

## 4.2 Boundary Rules

- Framework logging core has zero third-party dependencies.
- Framework logging core has no Unity API references.
- Runtime-specific output implementations stay outside framework core.
- Business/runtime bootstrap composes router + sinks during startup.
- Runtime paths call only `ILogger` methods.

## 5. Public API (MVP)

## 5.1 Log Level

```csharp
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
}
```

## 5.2 Logger Contract

```csharp
public interface ILogger
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}
```

## 5.3 Sink Contract

```csharp
public interface ILogSink
{
    void Write(LogLevel level, string message);
}
```

## 5.4 Router Contract

`LogRouter` implements `ILogger` and provides registration/freeze APIs:

```csharp
public sealed class LogRouter : ILogger
{
    public void AddSink(ILogSink sink, LogLevel minLevel);
    public void Freeze();

    public void Debug(string message);
    public void Info(string message);
    public void Warn(string message);
    public void Error(string message);
}
```

`NullLogger` provides no-op behavior and can be used as safe default.

## 6. Lifecycle and Data Flow

## 6.1 Startup (Registration) Flow

1. Create `LogRouter`.
2. Register one or more sinks with `AddSink(sink, minLevel)`.
3. Call `Freeze()`.
4. Inject `ILogger` (`LogRouter` or `NullLogger`) into framework/business components.

## 6.2 Runtime Dispatch Flow

For each call (`Debug/Info/Warn/Error`):

1. Map method to `LogLevel`.
2. Normalize null message to `string.Empty`.
3. Iterate sink registrations in registration order.
4. If `level >= minLevel`, invoke `sink.Write(level, message)`.
5. If sink throws, catch and continue to next sink.

## 6.3 Freeze Semantics

- `AddSink` is valid only before freeze.
- `Freeze()` is idempotent.
- `AddSink` after freeze throws `InvalidOperationException`.
- Logging calls remain valid after freeze.

This mirrors existing framework composition style (register -> freeze -> runtime dispatch).

## 7. Error Handling Semantics

Fail-fast programming errors:

1. `AddSink(null, ...)` -> `ArgumentNullException`
2. `AddSink` after freeze -> `InvalidOperationException`

Fail-safe runtime logging:

1. sink `Write` exceptions are swallowed
2. sink failures do not block other sinks
3. logging never throws because a sink failed

This balances correctness of setup APIs with runtime robustness.

## 8. CQRS Unification and Migration

Current CQRS-specific diagnostics are replaced by framework logging abstraction.

Migration plan (design-level):

1. `CqrsBus` constructor dependency changes from `ICqrsLogger` to `ILogger`.
2. Default constructor fallback changes from `NullCqrsLogger.Instance` to `NullLogger.Instance`.
3. CQRS-local logger types are removed:
   - `Runtime/Cqrs/Diagnostics/ICqrsLogger.cs`
   - `Runtime/Cqrs/Diagnostics/NullCqrsLogger.cs`
4. CQRS behavior remains unchanged except unified logger abstraction.

Compatibility posture:

- This is an intentional framework API change in a controlled internal surface.
- Default no-op logging behavior remains the same.

## 9. Testing Strategy and Acceptance Criteria

## 9.1 Functional Tests (EditMode)

1. Single sink receives eligible levels.
2. Per-sink `minLevel` filtering works.
3. Multiple sinks execute in registration order.
4. One sink throwing does not stop later sinks.
5. `Freeze()` blocks post-freeze `AddSink`.
6. `Freeze()` is idempotent.
7. Null message normalization to `string.Empty`.

## 9.2 CQRS Regression Tests

1. Existing command/query/event dispatch tests keep passing after logger unification.
2. Existing registration guard and failure semantic tests keep passing.

Acceptance gate:

- All new logging tests pass.
- Existing CQRS test suite passes without behavior regressions.

## 10. Risks and Mitigations

1. **Risk:** teams expect built-in Unity/file sinks in framework.
   - **Mitigation:** document that framework provides abstractions only; sinks live in runtime adapters.
2. **Risk:** swallowed sink exceptions hide output issues.
   - **Mitigation:** recommend adapter-level self-monitoring/counters in integration layer.
3. **Risk:** later demand for structured logs complicates MVP API.
   - **Mitigation:** keep `ILogSink.Write(LogLevel, string)` stable; add optional advanced abstractions in future design rounds.

## 11. Milestone for Planning Handoff

This design is ready for implementation planning when:

1. API and semantics in sections 5-7 are accepted.
2. CQRS migration scope in section 8 is accepted.
3. Test acceptance gate in section 9 is accepted.
