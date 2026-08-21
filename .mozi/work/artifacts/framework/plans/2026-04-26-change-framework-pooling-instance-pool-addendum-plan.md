# Change.Framework Pooling InstancePool Addendum Plan

Goal: Add advanced pooling support (`Func<T>` factory and same-`T` multi-pool instances) while keeping `Pool<T>` backward-compatible.

## Implementation steps

- [x] Add `IPool<T>` contract in `Change.Framework.Pooling`.
- [x] Add internal shared pool core (`PoolEngine<T>`) to centralize behavior.
- [x] Refactor static `Pool<T>` into wrapper over shared core without API changes.
- [x] Add `InstancePool<T>` with `(Func<T> factory, int maxSize)` constructor.
- [x] Add EditMode tests for:
  - core get/release reuse behavior,
  - factory validation and call-path,
  - same-`T` pool isolation,
  - strict safety behavior parity.
- [x] Run EditMode test verification and save result XML under `UnityProject/TestResults/`.

## Verification

- Command: Unity EditMode test run (`-runTests -testPlatform EditMode`)
- Result file: `UnityProject/TestResults/editmode-framework-20260426-124534.xml`
- Outcome: `122 passed, 0 failed`
