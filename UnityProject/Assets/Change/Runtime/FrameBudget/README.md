# Frame Budget Governance (MVP)

## What this module provides

- Unified scheduling API for `Update/LateUpdate/FixedUpdate`
- Priority classes: `Critical`, `Important`, `Deferred`
- Frame budget policy and deferral rules
- P50/P95 performance snapshots and over-budget window checks
- UniTask helper for cooperative yielding

## Quick start

```csharp
FrameBudget.Initialize(FrameBudgetPolicy.Default);

FrameBudget.Schedule(FramePhase.Update, FrameTaskPriority.Deferred, "refresh-mini-map", RefreshMiniMap);
```

## Default policy (`FrameBudgetPolicy.Default`)

- `UpdateBudgetMs`: `2.5`
- `LateUpdateBudgetMs`: `1.0`
- `FixedUpdateBudgetMs`: `0.5`
- `MaxDeferredFrames`: `3`
- `OverBudgetWindowFrames`: `30`
- `OverBudgetPercentThreshold`: `20`
- `ImportantMaxPerFrame`: `16`
- `QueueCapacityPerPhase`: `256`

## Priority execution semantics

- `Critical`: always executes in the current phase, even when budget is exhausted.
- `Important`: executes only when `remainingBudgetMs > 0`, capped by `ImportantMaxPerFrame`.
- Aged `Important` items can spill over by at most one extra execution per run when age is at least one frame.
- `Deferred`: while budget remains, non-overdue items are re-queued; overdue items (`age > MaxDeferredFrames`) are forced.
- When no budget remains, `Important` items are deferred and only overdue `Deferred` items are forced.

## Long-loop migration pattern

```csharp
for (int i = 0; i < entities.Count; i++)
{
    TickOneEntity(entities[i]);

    if ((i & 31) == 0)
    {
        await FrameBudgetUniTaskExtensions.YieldIfBudgetExceeded(FramePhase.Update, ct);
    }
}
```

## Team rules

1. New high-frequency logic must declare priority.
2. Work expected above 0.3ms must expose a cut point (`YieldIfBudgetExceeded`).
3. `Critical` is only for same-frame gameplay correctness.
