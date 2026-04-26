# Change.Framework Pooling InstancePool Addendum Design

Date: 2026-04-26
Status: Approved in-session (addendum)
Scope: Extend pooling API for custom factory and multiple pools per same `T` without breaking existing static `Pool<T>`.

## 1. Why this addendum exists

The unification work kept `Pool<T>` as the single default entry and removed duplicate collection-side pooling.  
After that cleanup, we identified two advanced requirements:

1. Support `Func<T>` custom construction.
2. Support multiple independent pools for the same payload type `T`.

## 2. Design decisions

1. Keep `Pool<T>` as-is for default usage (`Get`/`Release` API unchanged).
2. Add `InstancePool<T>` for advanced scenarios (factory + per-instance pool isolation).
3. Extract shared pool behavior into one internal engine to avoid duplicated implementations.
4. Keep strict safety policy consistent with existing pooling behavior:
   - `UNITY_EDITOR`/`DEVELOPMENT_BUILD`: throw on misuse.
   - non-development: ignore misuse safely.

## 3. Target API shape

- New interface: `IPool<T>` with shared operations:
  - `Get`, `Release`, `Prewarm`, `SetMaxSize`, `Clear`, `GetStats`, `InactiveCount`.
- New type: `InstancePool<T> : IPool<T>` (constructor takes `Func<T>` and optional `maxSize`).
- Existing static type: `Pool<T>` delegates to shared internal engine, preserving public API and constraints.

## 4. Non-goals

1. No API rename (`Rent/Return` aliases are out of scope).
2. No threading model change (still non-thread-safe by design).
3. No Collections-layer pool reintroduction.

## 5. Acceptance criteria

1. Existing `Pool<T>` call sites compile and behave unchanged.
2. `InstancePool<T>` supports custom factory and same-`T` multi-pool isolation.
3. Pool logic exists in one internal implementation only.
4. New and existing pooling tests pass.
