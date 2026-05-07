# Change.Framework CQRS Architecture Review and Optimization Design

- Date: 2026-05-07
- Project: `UnityProject/Assets/Change/Framework/Cqrs`
- Scope: Architectural review and optimization of the in-process CQRS implementation
- Status: Approved design baseline for planning

## 1. Background and Problem Statement

The current `CqrsBus` implementation is a high-performance in-process message dispatcher with explicit registration and freeze lifecycle. It has strong engineering properties (fail-fast, strong typing, hot-path zero allocation) but mixes CQRS semantics with generic bus semantics.

User-raised pain points:

1. Potential responsibility overlap between command and event.
2. High apparent redundancy: each command/query/event requires handlers and registrations.
3. Handler class proliferation and concern about handler instantiation GC.
4. Unclear requirement and boundaries for multiple bus instances.
5. `Freeze()` timing is hard to reason about in real usage.
6. Uncertainty whether the implementation matches "standard CQRS".

Target direction selected by user:

- Prioritize alignment with standard CQRS semantics.
- Adopt "CQRS + Domain Events" (not Event Sourcing).
- Enforce strict semantic boundary: Domain Event is a past-tense fact; domain event handlers must not dispatch business commands.
- Use mixed ergonomics strategy: command/query conservative, event handling more flexible.
- Support multiple instances, but only via explicit naming and isolation.

## 2. Design Goals and Non-goals

### 2.1 Goals

1. Make CQRS semantics explicit at API level: command, query, and domain event have distinct responsibilities.
2. Keep current performance posture: no hot-path allocation regressions after warm-up.
3. Remove lifecycle ambiguity around runtime dispatch readiness.
4. Provide explicit, controlled multi-instance support.
5. Reduce practical boilerplate without weakening semantic boundaries.

### 2.2 Non-goals

1. Introduce Event Sourcing or event-store-backed state reconstruction.
2. Introduce distributed bus, broker integration, retries, or transactional saga orchestration in framework MVP.
3. Add reflection-based auto-scan registration on hot path.
4. Expand into persistence or domain modeling frameworks.

## 3. CQRS Semantics and Boundary Rules

### 3.1 Semantic Definitions

- `Command`: intent to change state; exactly one handler.
- `Query<TResult>`: read-only request; exactly one handler; must not mutate write-model state.
- `DomainEvent`: fact that already happened (past tense); zero or more handlers/subscribers.

### 3.2 Hard Rules

1. Domain event handlers are not allowed to dispatch business commands (`Send`) to avoid hidden process orchestration.
2. Query handlers are not allowed to perform write operations.
3. Command and domain event naming are separated by convention:
   - Command: `CreateXCommand`, `ApplyYCommand`
   - Domain event: `XCreatedEvent`, `YAppliedEvent`

These rules are enforced by architecture tests and code review guardrails.

## 4. Component Architecture

### 4.1 Public-facing split

Introduce explicit startup/runtime split:

- `ICqrsBootstrap`: registration-only surface during composition/bootstrap.
- `ICqrsRuntime`: dispatch-only surface for gameplay/runtime.
- `ICqrsRuntimeProvider`: explicit lookup by context id for controlled multi-instance use.

Runtime API shape:

- `Send<TCommand>(in TCommand command)`
- `Ask<TQuery, TResult>(in TQuery query)` (or keep `Query` name if backward compatibility is preferred)
- `Publish<TDomainEvent>(in TDomainEvent event)`

Bootstrap API shape:

- Register command/query/domain event handlers.
- `Build()` produces an immutable runtime and seals registration lifecycle.

### 4.2 Internal structure

Internal implementation may continue reusing current dictionary-based dispatch internals, but must be conceptually split into three registries:

- `ICommandRegistry` (1:1)
- `IQueryRegistry` (1:1)
- `IDomainEventRegistry` (1:N)

This preserves performance while making semantic boundaries first-class.

## 5. Lifecycle and Freeze Replacement

Current ambiguity originates from exposing `Freeze()` to consumers. Replace external `Freeze()` usage with bootstrap finalization:

1. Create bootstrap.
2. Register handlers/subscribers.
3. Call `Build()`.
4. Use returned `ICqrsRuntime` only.

After `Build()`:

- Registration APIs are no longer reachable from runtime code.
- "Forgot to freeze" becomes structurally impossible for normal consumers.
- Existing freeze semantics become an internal implementation detail.

## 6. Multi-instance Model

Adopt explicit multi-instance support with mandatory context id:

- `CqrsContextId` (for example `global`, `battle`, `lobby`)
- Runtime access only through `ICqrsRuntimeProvider.Get(contextId)`

Rules:

1. No implicit fallback to unnamed default runtime.
2. Context creation and registration are explicit at composition root.
3. Cross-context interaction is explicit and auditable.

This supports isolation while preventing accidental instance proliferation.

## 7. Boilerplate and Handler Ergonomics

### 7.1 Command/Query (conservative)

- Keep one-message-one-handler for semantic clarity and testability.
- Handler instantiation is moved to bootstrap/composition root (container/factory/manual module), not runtime call sites.
- Runtime stores prepared handler references; dispatch performs no per-call allocations.

### 7.2 Domain events (more flexible)

Allow two subscription forms:

1. Class handler (`IDomainEventHandler<TEvent>`) for complex behavior.
2. Static function subscription for lightweight projection/metrics cases.

Constraint:

- Function subscriptions must be non-capturing (static) to avoid hidden closure allocations.

### 7.3 Registration ergonomics

Add module-level registration entrypoints to reduce repetitive setup code:

- `RegisterFromModule(IModuleRegistration module)`

Optional future extension:

- Source-generated registration catalogs (no reflection scan at runtime).

## 8. Error Handling Policy

- `Send`/`Ask`: missing handler is fail-fast exception.
- `Publish`: default behavior invokes all handlers, aggregates failures as `AggregateException`.
- Optional context-level policy may allow `StopOnFirstFailure` for special domains.

Important:

- Domain event failure does not imply automatic command rollback in this design baseline.

## 9. Standards Alignment Conclusion

Current implementation is best described as:

- Strongly-typed in-process message bus with CQRS-style APIs.

After this design is applied, implementation should be described as:

- Standard in-process CQRS with Domain Events.

Explicitly out of scope:

- Event Sourcing semantics.

## 10. Migration Strategy

### Phase 1: Interface split first, internals stable

1. Introduce `ICqrsBootstrap`, `ICqrsRuntime`, `ICqrsRuntimeProvider`.
2. Keep existing `CqrsBus` internals behind adapter/facade.
3. Replace external `Freeze()` usage with `Build()` workflow.

Verification:

- Existing dispatch tests still pass.

### Phase 2: Semantic hardening

1. Introduce domain event naming/type boundary (`IEvent` to `IDomainEvent`, with compatibility transition if needed).
2. Add architecture tests:
   - domain event handlers cannot depend on command bus.
   - query handlers cannot depend on write-side services.
3. Document non-Event-Sourcing scope.

Verification:

- Architecture tests enforce semantic boundaries.

### Phase 3: Ergonomics without performance regression

1. Add module registration helpers.
2. Add non-capturing static domain event subscription path.
3. Keep zero-allocation dispatch guarantees.

Verification:

- `Send/Query(or Ask)/Publish` hot-path allocation tests remain green.

## 11. Acceptance Criteria

This design is considered successfully implemented when all are true:

1. API-level responsibility boundaries are explicit and testable.
2. Business code has no explicit `Freeze()` call sites.
3. Runtime instance access is explicit by context id.
4. Existing hot-path allocation targets do not regress.
5. Migration path is documented and executable without ambiguous lifecycle handling.

## 12. Risks and Mitigations

1. Risk: semantic rules drift back into generic bus usage.
   - Mitigation: architecture tests + naming conventions + review checklist.
2. Risk: multi-instance misuse and accidental context fragmentation.
   - Mitigation: explicit context registry and no implicit default fallback.
3. Risk: ergonomics features accidentally introduce hidden allocations.
   - Mitigation: non-capturing function constraints and allocation regression tests.

## 13. Planning Handoff

This document is the approved design baseline for the next step:

- create an implementation plan via writing-plans workflow,
- then execute phased migration with verification gates.
