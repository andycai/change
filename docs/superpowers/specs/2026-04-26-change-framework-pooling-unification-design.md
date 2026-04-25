# Change.Framework Pooling Unification Design

Date: 2026-04-26
Status: Approved in-session (design phase)
Scope: Framework-level object pooling architecture cleanup (no implementation in this document)

## 1. Context

`Change.Framework` currently contains two object-pooling implementations:

1. `Change.Framework.Pooling.Pool<T>` (static generic pool)
2. `Change.Framework.Collections.ObjectPool<T>` (instance pool)

This duplicates pooling logic, reset protocols, and tests, which conflicts with the goal of a single reusable, business-agnostic framework core.

## 2. Decisions (Locked)

1. **Single public pooling entry only**: keep `Change.Framework.Pooling` as the only object-pooling module.
2. **Immediate removal**: remove `Collections/ObjectPool<T>` in this refactor (no deprecation bridge).
3. **Keep static model**: retain static `Pool<T>` model only (`where T : class, IPoolable, new()`).
4. **API naming unchanged**: keep `Get()` / `Release()` (no `Rent()` / `Return()` aliases).
5. **Remove duplicate reset contract**: remove `Collections/IResettable`; use only `IPoolable.Reset()`.

## 3. Target Architecture

### 3.1 Module boundaries

- `Change.Framework.Pooling`
  - Owns all object-pool behavior and contracts.
  - Public API remains:
    - `IPoolable`
    - `Pool<T>`
    - `PoolDefaults`
    - `PoolStats`
- `Change.Framework.Collections`
  - Owns collection/container data structures only.
  - No object-pool type and no pool reset protocol.

### 3.2 Source-of-truth rule

After migration, there must be exactly one object-pool implementation in `Change.Framework`: `Pooling/Pool.cs`.

## 4. File-Level Change Design

## 4.1 Remove

- `UnityProject/Assets/Change/Framework/Collections/Containers/ObjectPool.cs`
- `UnityProject/Assets/Change/Framework/Collections/Abstractions/IResettable.cs`
- `UnityProject/Assets/Change/Framework/Tests/EditMode/Collections/ObjectPoolTests.cs`

## 4.2 Keep (pooling core)

- `UnityProject/Assets/Change/Framework/Pooling/Pool.cs`
- `UnityProject/Assets/Change/Framework/Pooling/IPoolable.cs`
- `UnityProject/Assets/Change/Framework/Pooling/PoolDefaults.cs`
- `UnityProject/Assets/Change/Framework/Pooling/PoolStats.cs`

## 4.3 Migrate

- `UnityProject/Assets/Change/Framework/Tests/EditMode/Collections/Allocation/FastCollectionsAllocationTests.cs`
  - Replace `ObjectPool<T>` usage with `Pool<T>` usage.
  - Replace local `IResettable` payload contract with `IPoolable`.

## 5. Runtime Behavior Contract (Post-Unification)

## 5.1 Lifecycle

- `Prewarm(count)`
  - Fills inactive pool up to `min(count, maxSize)`.
  - Throws on invalid `count`.
- `Get()`
  - Pops from inactive if available; otherwise constructs `new T()`.
  - Marks rented state for safety checks.
  - Updates stats (`Created`, `Rented`).
- `Release(item)`
  - Validates null and ownership/double-release according to build policy.
  - Calls `item.Reset()`.
  - Returns to inactive when capacity allows; otherwise drops.
  - Updates stats (`Released`, `Dropped`).
- `Clear()`
  - Clears inactive storage.
  - Invalidates outstanding rentals by resetting leased-state tracking.
- `SetMaxSize(max)`
  - Applies immediately.
  - Trims inactive entries when shrinking.

## 5.2 Safety policy

- Editor/Development builds: misuse throws (`ArgumentNullException`, `InvalidOperationException`).
- Non-development builds: misuse path is ignored safely, without state corruption.

## 6. Testing Strategy

## 6.1 Pooling tests remain source of truth

Keep and rely on:

- `Tests/EditMode/Pooling/PoolCoreTests.cs`
- `Tests/EditMode/Pooling/PoolCapacityAndStatsTests.cs`
- `Tests/EditMode/Pooling/PoolStrictSafetyTests.cs`

## 6.2 Allocation contract migration

Update allocation coverage to assert steady-state zero allocation with `Pool<T>.Get/Release` after warm-up.

## 6.3 Removal verification

Run global search to ensure no remaining references to:

- `ObjectPool<`
- `IResettable`
- `ResetState(`

## 7. Documentation Alignment

Update relevant docs/spec text so object pooling is described only under `Change.Framework.Pooling`.

Specifically, remove any statement that treats `Collections.ObjectPool<T>` as an active supported API.

## 8. Implementation Sequence

1. Remove duplicate collection-side pooling types and tests.
2. Migrate remaining usages to `Pooling` API.
3. Re-run pooling + allocation tests.
4. Update framework docs/spec references.

## 9. Risks and Mitigations

1. **Hidden references after removing `IResettable`**
   - Mitigation: full-text scan and compile/test verification.
2. **Allocation regressions after test migration**
   - Mitigation: preserve warm-up and existing steady-state assertions.
3. **Build-policy behavior drift (dev vs release)**
   - Mitigation: keep strict-safety tests as guard rails.

## 10. Done Criteria

1. No object-pool implementation exists in `Collections`.
2. `Pool<T>` is the only pooling API path in framework code.
3. No references to `IResettable`/`ResetState` remain.
4. Pooling tests and allocation tests pass under the updated API.
5. Docs/spec content no longer describes dual-track pooling.

