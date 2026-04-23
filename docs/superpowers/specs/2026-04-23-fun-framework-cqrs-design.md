# Fun.Framework CQRS Design (MVP)

- Date: 2026-04-23
- Project: `UnityProject/Assets/Fun/Framework`
- Scope: Framework-layer reusable CQRS foundation (business-agnostic)
- Status: Approved design baseline for planning

## 1. Background and Goals

Build a production-grade but intentionally simplified CQRS implementation inside `Fun.Framework`, so business code can directly develop on top of it.

Primary goals:

1. Business-agnostic framework capability, no domain coupling.
2. Minimal API surface and low cognitive overhead.
3. Zero third-party dependencies.
4. Synchronous execution model only (no async/await in MVP).
5. Hot-path zero allocations (0GC) for command/query/event dispatch.

## 2. Non-goals (Explicitly Out of Scope)

To preserve simplicity and keep boundaries clear, MVP excludes:

- DDD modeling primitives (Aggregate root base classes, domain services, etc.)
- Persistence abstractions and concrete storage adapters
- Distributed messaging / external event bus integration
- Retry/circuit-breaker/transaction orchestration
- Reflection-based auto-scan registration
- AOP/pipeline behaviors/interceptors

## 3. Design Constraints and Decisions

Confirmed decisions:

1. Layer and location: framework-only under `Framework` directory.
2. Message model: `Command` / `Query` / `Event` all prefer `struct`.
3. Execution model: synchronous only.
4. Dependencies: zero external libraries.
5. Registration style: explicit manual registration (startup phase).
6. Event model: generic and strongly typed; events use `struct`.
7. Performance target: hot-path 0GC; startup-time allocations are acceptable.

Selected architecture approach:

- **Approach B (chosen):** struct messages + class handlers + strongly typed generic dispatch.
- Why this approach:
  - Preserves API simplicity and maintainability.
  - Achieves runtime 0GC target with fewer edge-case pitfalls than all-struct internals.
  - Avoids the complexity overhead of pooling-heavy or reflection-heavy designs.

## 4. Architecture Overview

## 4.1 Component Map

`Fun.Framework.Cqrs` (Runtime asmdef):

1. Message contracts
   - `ICommand`
   - `IQuery<TResult>`
   - `IEvent`
2. Handler contracts
   - `ICommandHandler<TCommand>`
   - `IQueryHandler<TQuery, TResult>`
   - `IEventHandler<TEvent>`
3. Dispatch facade (bus)
   - `ICqrsBus` + concrete `CqrsBus`
4. Registry
   - Command/query/event registration tables
   - Freeze state management
5. Exceptions
   - Registration/runtime misconfiguration exceptions
6. Optional logging abstraction
   - `ICqrsLogger` + `NullCqrsLogger`

## 4.2 Boundary Rules

- Business layer depends on bus contracts and message/handler interfaces.
- Framework layer never depends on business assemblies.
- Registration happens during bootstrap/composition root.
- Runtime gameplay path only executes dispatch APIs (`Send/Query/Publish`).

## 5. Public API (MVP)

## 5.1 Message Contracts

```csharp
public interface ICommand {}
public interface IQuery<TResult> {}
public interface IEvent {}
```

Usage convention:

- Business message types should be declared as `readonly struct` whenever feasible.
- Fields should avoid reference-heavy payloads on hot paths.

## 5.2 Handler Contracts

```csharp
public interface ICommandHandler<TCommand> where TCommand : struct, ICommand
{
    void Handle(in TCommand command);
}

public interface IQueryHandler<TQuery, TResult>
    where TQuery : struct, IQuery<TResult>
{
    TResult Handle(in TQuery query);
}

public interface IEventHandler<TEvent> where TEvent : struct, IEvent
{
    void Handle(in TEvent evt);
}
```

## 5.3 Bus Contracts

```csharp
public interface ICqrsBus
{
    void Send<TCommand>(in TCommand command)
        where TCommand : struct, ICommand;

    TResult Query<TQuery, TResult>(in TQuery query)
        where TQuery : struct, IQuery<TResult>;

    void Publish<TEvent>(in TEvent evt)
        where TEvent : struct, IEvent;
}
```

## 5.4 Registration Contracts

```csharp
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
```

Behavior:

- Registration is allowed only before `Freeze()`.
- Runtime dispatch assumes frozen registry.

## 6. Runtime Data Flow

## 6.1 Startup Flow

1. Create registry/bus.
2. Register command/query handlers and event subscribers explicitly.
3. Call `Freeze()` to lock registration.
4. Expose `ICqrsBus` to business layer entry points.

## 6.2 Runtime Flow

- Command: `Send` -> command table lookup -> handler `Handle(in cmd)`.
- Query: `Query` -> query table lookup -> handler `Handle(in query)` -> return result.
- Event: `Publish` -> subscriber list lookup -> sequential `Handle(in evt)`.

No reflection, no dynamic scanning, no lambda capture on hot path.

## 7. Error Handling and Operational Semantics

Default policy is fail-fast for misconfiguration:

1. Missing command/query handler -> throw `HandlerNotRegisteredException`.
2. Duplicate command/query registration -> throw `DuplicateRegistrationException`.
3. Register/subscribe after freeze -> throw `RegistryFrozenException`.
4. Publish with zero subscribers -> no-op (optionally trace in dev builds).

Exception propagation:

- Handler exceptions are not swallowed; they propagate to caller.
- Framework does not apply retry/fallback logic.

## 8. 0GC Strategy and Guardrails

Target definition:

- After warm-up, repeated `Send/Query/Publish` calls produce zero managed allocations on the hot path.

Implementation guardrails:

1. `in` parameters to avoid message copying overhead.
2. Generic strongly typed dispatch to avoid boxing.
3. No runtime reflection or dynamic invocation.
4. No closure allocations on dispatch path.
5. Event subscriber storage allocated during startup only.
6. Error-path string construction acceptable only in exceptional cases.

Pooling policy:

- No object pooling in MVP core.
- If future business constraints require reference-type messages, pooling can be introduced outside core MVP path.

## 9. Testing and Acceptance Criteria

## 9.1 Functional Tests

1. Command dispatch reaches the registered handler.
2. Query dispatch reaches handler and returns expected result.
3. Event publish notifies all subscribers in registration order.
4. Duplicate registrations throw expected exception.
5. Missing handler throws expected exception.
6. Registration after freeze throws expected exception.

## 9.2 Allocation Tests

Use allocation measurement (for example `GC.GetAllocatedBytesForCurrentThread()`):

1. Warm up bus and handlers.
2. Execute high-volume loops (e.g., 100,000 iterations) for command/query/event.
3. Verify no allocation growth in steady-state loops (excluding framework/test harness noise, if any).

Acceptance gate:

- Functional tests pass.
- Hot-path allocation checks pass for `Send/Query/Publish`.

## 10. Folder and Assembly Plan (Design-level)

Target under `UnityProject/Assets/Fun/Framework`:

- `Runtime/`
  - `Abstractions/` (interfaces/contracts)
  - `Core/` (bus/registry/dispatch)
  - `Exceptions/`
  - `Diagnostics/` (optional logger abstractions)
- `Fun.Framework.asmdef`

Naming principle:

- Keep names explicit and stable.
- Avoid introducing extra modules until necessary.

## 11. Risks and Mitigations

1. Risk: accidental boxing through loosely typed APIs.
   - Mitigation: enforce generic constraints and avoid `object`-based dispatch in hot path.
2. Risk: overly broad initial feature set breaks simplicity goal.
   - Mitigation: keep strict MVP scope; add extensions only after validated usage.
3. Risk: runtime misconfiguration (missing registration).
   - Mitigation: fail-fast exceptions and mandatory bootstrap registration.

## 12. Milestone Definition for Planning Handoff

MVP is ready for implementation planning when:

1. Contracts are finalized as in this spec.
2. Startup registration/freeze lifecycle is locked.
3. Exception semantics and event no-subscriber behavior are fixed.
4. Allocation acceptance criteria are accepted as release gate.

This document is the approved baseline for the next step: implementation planning via writing-plans workflow.
