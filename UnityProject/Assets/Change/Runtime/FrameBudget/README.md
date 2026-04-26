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
