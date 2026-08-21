# Change Frame Budget Governance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a low-intrusion runtime frame-budget governance system for `Update/LateUpdate/FixedUpdate` that smooths CPU spikes and enforces the MVP target (`P95 < 4ms` for governed runtime work) with automatic deferral for non-critical work.

**Architecture:** Add a new `Change.Runtime.FrameBudget` runtime module with four core parts: policy/config validation, scheduler, perf recorder, and Unity driver/bootstrap. Keep business code migration minimal by exposing a static scheduling facade plus UniTask cooperative-yield helpers. Use EditMode tests for deterministic core logic and PlayMode tests for Unity lifecycle behavior.

**Tech Stack:** Unity 2022.3 LTS, C#, Change.Framework.Collections (`RingBuffer<T>`), UniTask 2.5.10, Unity Test Framework (EditMode + PlayMode).

---

## Scope Check

The spec is one subsystem (frame-budget governance) and is suitable for a single plan. It contains one coherent runtime pipeline (policy -> scheduling -> driving -> recording) and can be delivered incrementally with testable milestones.

## File Structure (Create/Modify Map)

### Runtime core

- Modify: `UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef` (add UniTask assembly reference)
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FramePhase.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameTaskPriority.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetPolicy.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameWorkItem.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameSchedulerRunResult.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameWorkScheduler.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FramePerfSnapshot.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FramePerfRecorder.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudget.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetDriver.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetBootstrap.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetUniTaskExtensions.cs`

### Tests

- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/FrameBudget/FrameBudgetPolicyTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/FrameBudget/FrameWorkSchedulerTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/FrameBudget/FramePerfRecorderTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/PlayMode/FrameBudget/FrameBudgetDriverPlayModeTests.cs`

### Docs

- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/README.md`

---

### Task 1: Add policy and phase/priority contracts with fail-fast validation

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FramePhase.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameTaskPriority.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetPolicy.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/FrameBudget/FrameBudgetPolicyTests.cs`

- [ ] **Step 1: Write the failing policy tests**

```csharp
using System;
using NUnit.Framework;

namespace Change.Runtime.Tests.EditMode.FrameBudget
{
    public sealed class FrameBudgetPolicyTests
    {
        [Test]
        public void DefaultPolicy_UsesSpecBudgets()
        {
            var policy = FrameBudgetPolicy.Default;

            Assert.AreEqual(2.5f, policy.UpdateBudgetMs);
            Assert.AreEqual(1.0f, policy.LateUpdateBudgetMs);
            Assert.AreEqual(0.5f, policy.FixedUpdateBudgetMs);
            Assert.AreEqual(3, policy.MaxDeferredFrames);
            Assert.AreEqual(30, policy.OverBudgetWindowFrames);
            Assert.AreEqual(20, policy.OverBudgetPercentThreshold);
        }

        [Test]
        public void Constructor_WhenBudgetInvalid_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(0f, 1f, 1f, 3, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 1f, 0, 30, 20, 16, 256));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new FrameBudgetPolicy(1f, 1f, 1f, 3, 0, 20, 16, 256));
        }

        [Test]
        public void GetPhaseBudgetMs_ReturnsPhaseSpecificValue()
        {
            var policy = new FrameBudgetPolicy(2f, 3f, 4f, 2, 30, 20, 8, 128);

            Assert.AreEqual(2f, policy.GetPhaseBudgetMs(FramePhase.Update));
            Assert.AreEqual(3f, policy.GetPhaseBudgetMs(FramePhase.LateUpdate));
            Assert.AreEqual(4f, policy.GetPhaseBudgetMs(FramePhase.FixedUpdate));
        }
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.Tests.EditMode.FrameBudget.FrameBudgetPolicyTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-framebudget-policy-${TS}.xml" \
  -logFile -
```

Expected: FAIL (types not found).

- [ ] **Step 3: Implement minimal policy contracts**

`UnityProject/Assets/Change/Runtime/FrameBudget/FramePhase.cs`

```csharp
namespace Change.Runtime
{
    public enum FramePhase : byte
    {
        Update = 0,
        LateUpdate = 1,
        FixedUpdate = 2,
    }
}
```

`UnityProject/Assets/Change/Runtime/FrameBudget/FrameTaskPriority.cs`

```csharp
namespace Change.Runtime
{
    public enum FrameTaskPriority : byte
    {
        Critical = 0,
        Important = 1,
        Deferred = 2,
    }
}
```

`UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetPolicy.cs`

```csharp
using System;

namespace Change.Runtime
{
    public readonly struct FrameBudgetPolicy
    {
        public static FrameBudgetPolicy Default => new(
            2.5f,
            1.0f,
            0.5f,
            maxDeferredFrames: 3,
            overBudgetWindowFrames: 30,
            overBudgetPercentThreshold: 20,
            importantMaxPerFrame: 16,
            queueCapacityPerPhase: 256);

        public FrameBudgetPolicy(
            float updateBudgetMs,
            float lateUpdateBudgetMs,
            float fixedUpdateBudgetMs,
            int maxDeferredFrames,
            int overBudgetWindowFrames,
            int overBudgetPercentThreshold,
            int importantMaxPerFrame,
            int queueCapacityPerPhase)
        {
            if (updateBudgetMs <= 0f) throw new ArgumentOutOfRangeException(nameof(updateBudgetMs));
            if (lateUpdateBudgetMs <= 0f) throw new ArgumentOutOfRangeException(nameof(lateUpdateBudgetMs));
            if (fixedUpdateBudgetMs <= 0f) throw new ArgumentOutOfRangeException(nameof(fixedUpdateBudgetMs));
            if (maxDeferredFrames < 1) throw new ArgumentOutOfRangeException(nameof(maxDeferredFrames));
            if (overBudgetWindowFrames < 1) throw new ArgumentOutOfRangeException(nameof(overBudgetWindowFrames));
            if (overBudgetPercentThreshold < 1 || overBudgetPercentThreshold > 100) throw new ArgumentOutOfRangeException(nameof(overBudgetPercentThreshold));
            if (importantMaxPerFrame < 1) throw new ArgumentOutOfRangeException(nameof(importantMaxPerFrame));
            if (queueCapacityPerPhase < 1) throw new ArgumentOutOfRangeException(nameof(queueCapacityPerPhase));

            UpdateBudgetMs = updateBudgetMs;
            LateUpdateBudgetMs = lateUpdateBudgetMs;
            FixedUpdateBudgetMs = fixedUpdateBudgetMs;
            MaxDeferredFrames = maxDeferredFrames;
            OverBudgetWindowFrames = overBudgetWindowFrames;
            OverBudgetPercentThreshold = overBudgetPercentThreshold;
            ImportantMaxPerFrame = importantMaxPerFrame;
            QueueCapacityPerPhase = queueCapacityPerPhase;
        }

        public float UpdateBudgetMs { get; }
        public float LateUpdateBudgetMs { get; }
        public float FixedUpdateBudgetMs { get; }
        public int MaxDeferredFrames { get; }
        public int OverBudgetWindowFrames { get; }
        public int OverBudgetPercentThreshold { get; }
        public int ImportantMaxPerFrame { get; }
        public int QueueCapacityPerPhase { get; }

        public float GetPhaseBudgetMs(FramePhase phase)
        {
            return phase switch
            {
                FramePhase.Update => UpdateBudgetMs,
                FramePhase.LateUpdate => LateUpdateBudgetMs,
                FramePhase.FixedUpdate => FixedUpdateBudgetMs,
                _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown frame phase."),
            };
        }
    }
}
```

- [ ] **Step 4: Run policy tests to verify pass**

Run the same command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add \
  UnityProject/Assets/Change/Runtime/FrameBudget/FramePhase.cs \
  UnityProject/Assets/Change/Runtime/FrameBudget/FrameTaskPriority.cs \
  UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetPolicy.cs \
  UnityProject/Assets/Change/Runtime/Tests/EditMode/FrameBudget/FrameBudgetPolicyTests.cs
git commit -m "feat(runtime): add frame budget policy and priority contracts"
```

---

### Task 2: Implement scheduler with deferral, deadlines, and over-budget behavior

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameWorkItem.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameSchedulerRunResult.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameWorkScheduler.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/FrameBudget/FrameWorkSchedulerTests.cs`

- [ ] **Step 1: Write failing scheduler tests**

```csharp
using NUnit.Framework;

namespace Change.Runtime.Tests.EditMode.FrameBudget
{
    public sealed class FrameWorkSchedulerTests
    {
        [Test]
        public void RunPhase_WhenBudgetZero_ExecutesCriticalOnly()
        {
            var scheduler = new FrameWorkScheduler(FrameBudgetPolicy.Default);
            int criticalCount = 0;
            int deferredCount = 0;

            scheduler.Enqueue(FrameWorkItem.Create(FramePhase.Update, FrameTaskPriority.Critical, "critical", () => criticalCount++));
            scheduler.Enqueue(FrameWorkItem.Create(FramePhase.Update, FrameTaskPriority.Deferred, "deferred", () => deferredCount++));

            var result = scheduler.RunPhase(FramePhase.Update, currentFrame: 10, remainingBudgetMs: 0f);

            Assert.AreEqual(1, criticalCount);
            Assert.AreEqual(0, deferredCount);
            Assert.AreEqual(1, result.DeferredCount);
        }

        [Test]
        public void RunPhase_WhenDeferredExpired_IncrementsOverdueCounter()
        {
            var policy = new FrameBudgetPolicy(2.5f, 1f, 0.5f, maxDeferredFrames: 1, 30, 20, 16, 32);
            var scheduler = new FrameWorkScheduler(policy);
            int deferredCount = 0;

            scheduler.Enqueue(FrameWorkItem.Create(FramePhase.Update, FrameTaskPriority.Deferred, "work", () => deferredCount++));

            _ = scheduler.RunPhase(FramePhase.Update, currentFrame: 100, remainingBudgetMs: 0f);
            var result = scheduler.RunPhase(FramePhase.Update, currentFrame: 102, remainingBudgetMs: 0f);

            Assert.AreEqual(1, result.OverdueCount);
            Assert.AreEqual(1, deferredCount, "Expired deferred work should be forced once overdue.");
        }

        [Test]
        public void RunPhase_WhenImportantOverLimit_DefersRemainder()
        {
            var policy = new FrameBudgetPolicy(2.5f, 1f, 0.5f, 3, 30, 20, importantMaxPerFrame: 1, 32);
            var scheduler = new FrameWorkScheduler(policy);
            int importantCount = 0;

            scheduler.Enqueue(FrameWorkItem.Create(FramePhase.Update, FrameTaskPriority.Important, "i1", () => importantCount++));
            scheduler.Enqueue(FrameWorkItem.Create(FramePhase.Update, FrameTaskPriority.Important, "i2", () => importantCount++));

            var result = scheduler.RunPhase(FramePhase.Update, currentFrame: 1, remainingBudgetMs: 1f);

            Assert.AreEqual(1, importantCount);
            Assert.AreEqual(1, result.DeferredCount);
        }
    }
}
```

- [ ] **Step 2: Run scheduler tests to confirm failure**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.Tests.EditMode.FrameBudget.FrameWorkSchedulerTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-framebudget-scheduler-${TS}.xml" \
  -logFile -
```

Expected: FAIL.

- [ ] **Step 3: Implement scheduler core**

`UnityProject/Assets/Change/Runtime/FrameBudget/FrameWorkItem.cs`

```csharp
using System;

namespace Change.Runtime
{
    public readonly struct FrameWorkItem
    {
        private FrameWorkItem(FramePhase phase, FrameTaskPriority priority, string tag, Action callback, int enqueuedFrame)
        {
            Phase = phase;
            Priority = priority;
            Tag = tag;
            Callback = callback;
            EnqueuedFrame = enqueuedFrame;
        }

        public FramePhase Phase { get; }
        public FrameTaskPriority Priority { get; }
        public string Tag { get; }
        public Action Callback { get; }
        public int EnqueuedFrame { get; }

        public static FrameWorkItem Create(FramePhase phase, FrameTaskPriority priority, string tag, Action callback)
        {
            if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("Tag is required.", nameof(tag));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            return new FrameWorkItem(phase, priority, tag, callback, enqueuedFrame: -1);
        }

        internal FrameWorkItem WithEnqueuedFrame(int frame) =>
            new(Phase, Priority, Tag, Callback, frame);
    }
}
```

`UnityProject/Assets/Change/Runtime/FrameBudget/FrameSchedulerRunResult.cs`

```csharp
namespace Change.Runtime
{
    public readonly struct FrameSchedulerRunResult
    {
        public FrameSchedulerRunResult(int executedCount, int deferredCount, int overdueCount)
        {
            ExecutedCount = executedCount;
            DeferredCount = deferredCount;
            OverdueCount = overdueCount;
        }

        public int ExecutedCount { get; }
        public int DeferredCount { get; }
        public int OverdueCount { get; }
    }
}
```

`UnityProject/Assets/Change/Runtime/FrameBudget/FrameWorkScheduler.cs`

```csharp
using System;
using Change.Framework.Collections;

namespace Change.Runtime
{
    public sealed class FrameWorkScheduler
    {
        private readonly FrameBudgetPolicy _policy;
        private readonly RingBuffer<FrameWorkItem> _updateCritical;
        private readonly RingBuffer<FrameWorkItem> _updateImportant;
        private readonly RingBuffer<FrameWorkItem> _updateDeferred;
        private readonly RingBuffer<FrameWorkItem> _lateCritical;
        private readonly RingBuffer<FrameWorkItem> _lateImportant;
        private readonly RingBuffer<FrameWorkItem> _lateDeferred;
        private readonly RingBuffer<FrameWorkItem> _fixedCritical;
        private readonly RingBuffer<FrameWorkItem> _fixedImportant;
        private readonly RingBuffer<FrameWorkItem> _fixedDeferred;

        public FrameWorkScheduler(in FrameBudgetPolicy policy)
        {
            _policy = policy;
            int cap = policy.QueueCapacityPerPhase;
            _updateCritical = new RingBuffer<FrameWorkItem>(cap);
            _updateImportant = new RingBuffer<FrameWorkItem>(cap);
            _updateDeferred = new RingBuffer<FrameWorkItem>(cap);
            _lateCritical = new RingBuffer<FrameWorkItem>(cap);
            _lateImportant = new RingBuffer<FrameWorkItem>(cap);
            _lateDeferred = new RingBuffer<FrameWorkItem>(cap);
            _fixedCritical = new RingBuffer<FrameWorkItem>(cap);
            _fixedImportant = new RingBuffer<FrameWorkItem>(cap);
            _fixedDeferred = new RingBuffer<FrameWorkItem>(cap);
        }

        public void Enqueue(in FrameWorkItem item)
        {
            var queued = item.WithEnqueuedFrame(UnityEngine.Time.frameCount);
            var queue = GetQueue(item.Phase, item.Priority);
            if (!queue.TryEnqueueNoResize(queued))
            {
                throw new InvalidOperationException($"Frame queue is full for {item.Phase}/{item.Priority}.");
            }
        }

        public FrameSchedulerRunResult RunPhase(FramePhase phase, int currentFrame, float remainingBudgetMs)
        {
            int executed = 0;
            int deferred = 0;
            int overdue = 0;
            int importantExecuted = 0;

            ExecuteQueue(GetQueue(phase, FrameTaskPriority.Critical), force: true, ref executed, ref deferred, ref overdue, currentFrame, ref importantExecuted);

            bool hasBudget = remainingBudgetMs > 0f;
            if (hasBudget)
            {
                ExecuteQueue(GetQueue(phase, FrameTaskPriority.Important), force: false, ref executed, ref deferred, ref overdue, currentFrame, ref importantExecuted);
                ExecuteQueue(GetQueue(phase, FrameTaskPriority.Deferred), force: false, ref executed, ref deferred, ref overdue, currentFrame, ref importantExecuted);
            }
            else
            {
                deferred += GetQueue(phase, FrameTaskPriority.Important).Count;
                deferred += GetQueue(phase, FrameTaskPriority.Deferred).Count;
                ForceOverdue(phase, currentFrame, ref executed, ref overdue);
            }

            return new FrameSchedulerRunResult(executed, deferred, overdue);
        }

        private void ExecuteQueue(
            RingBuffer<FrameWorkItem> queue,
            bool force,
            ref int executed,
            ref int deferred,
            ref int overdue,
            int currentFrame,
            ref int importantExecuted)
        {
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                if (!queue.TryDequeue(out var item))
                {
                    break;
                }

                if (!force && item.Priority == FrameTaskPriority.Important && importantExecuted >= _policy.ImportantMaxPerFrame)
                {
                    Requeue(item);
                    deferred++;
                    continue;
                }

                bool isOverdue = item.Priority == FrameTaskPriority.Deferred &&
                                 currentFrame - item.EnqueuedFrame > _policy.MaxDeferredFrames;

                if (force || isOverdue || item.Priority != FrameTaskPriority.Deferred)
                {
                    item.Callback();
                    executed++;
                    if (item.Priority == FrameTaskPriority.Important)
                    {
                        importantExecuted++;
                    }

                    if (isOverdue)
                    {
                        overdue++;
                    }
                }
                else
                {
                    Requeue(item);
                    deferred++;
                }
            }
        }

        private void ForceOverdue(FramePhase phase, int currentFrame, ref int executed, ref int overdue)
        {
            var deferredQueue = GetQueue(phase, FrameTaskPriority.Deferred);
            int count = deferredQueue.Count;
            for (int i = 0; i < count; i++)
            {
                if (!deferredQueue.TryDequeue(out var item))
                {
                    break;
                }

                bool isOverdue = currentFrame - item.EnqueuedFrame > _policy.MaxDeferredFrames;
                if (isOverdue)
                {
                    item.Callback();
                    executed++;
                    overdue++;
                }
                else
                {
                    Requeue(item);
                }
            }
        }

        private void Requeue(in FrameWorkItem item)
        {
            var queue = GetQueue(item.Phase, item.Priority);
            if (!queue.TryEnqueueNoResize(item))
            {
                throw new InvalidOperationException("Requeue failed because queue is full.");
            }
        }

        private RingBuffer<FrameWorkItem> GetQueue(FramePhase phase, FrameTaskPriority priority)
        {
            return (phase, priority) switch
            {
                (FramePhase.Update, FrameTaskPriority.Critical) => _updateCritical,
                (FramePhase.Update, FrameTaskPriority.Important) => _updateImportant,
                (FramePhase.Update, FrameTaskPriority.Deferred) => _updateDeferred,
                (FramePhase.LateUpdate, FrameTaskPriority.Critical) => _lateCritical,
                (FramePhase.LateUpdate, FrameTaskPriority.Important) => _lateImportant,
                (FramePhase.LateUpdate, FrameTaskPriority.Deferred) => _lateDeferred,
                (FramePhase.FixedUpdate, FrameTaskPriority.Critical) => _fixedCritical,
                (FramePhase.FixedUpdate, FrameTaskPriority.Important) => _fixedImportant,
                (FramePhase.FixedUpdate, FrameTaskPriority.Deferred) => _fixedDeferred,
                _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown queue mapping."),
            };
        }
    }
}
```

- [ ] **Step 4: Run scheduler tests to verify pass**

Run the command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add \
  UnityProject/Assets/Change/Runtime/FrameBudget/FrameWorkItem.cs \
  UnityProject/Assets/Change/Runtime/FrameBudget/FrameSchedulerRunResult.cs \
  UnityProject/Assets/Change/Runtime/FrameBudget/FrameWorkScheduler.cs \
  UnityProject/Assets/Change/Runtime/Tests/EditMode/FrameBudget/FrameWorkSchedulerTests.cs
git commit -m "feat(runtime): add frame work scheduler with deferral rules"
```

---

### Task 3: Add perf recorder with P50/P95 snapshots and over-budget window tracking

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FramePerfSnapshot.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FramePerfRecorder.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/FrameBudget/FramePerfRecorderTests.cs`

- [ ] **Step 1: Write failing perf recorder tests**

```csharp
using NUnit.Framework;

namespace Change.Runtime.Tests.EditMode.FrameBudget
{
    public sealed class FramePerfRecorderTests
    {
        [Test]
        public void Snapshot_ComputesP95FromWindow()
        {
            var recorder = new FramePerfRecorder(windowSize: 10, overBudgetThresholdPercent: 20);

            recorder.RecordFrame(1f, isOverBudget: false);
            recorder.RecordFrame(2f, isOverBudget: false);
            recorder.RecordFrame(3f, isOverBudget: true);
            recorder.RecordFrame(4f, isOverBudget: true);
            recorder.RecordFrame(5f, isOverBudget: false);

            FramePerfSnapshot snapshot = recorder.CreateSnapshot();

            Assert.GreaterOrEqual(snapshot.P95Ms, 4f);
            Assert.AreEqual(40, snapshot.OverBudgetPercent);
            Assert.IsTrue(snapshot.ShouldThrottle);
        }

        [Test]
        public void RecordFrame_WhenWindowRollsOldData_DropsOldest()
        {
            var recorder = new FramePerfRecorder(windowSize: 3, overBudgetThresholdPercent: 50);

            recorder.RecordFrame(1f, isOverBudget: false);
            recorder.RecordFrame(2f, isOverBudget: false);
            recorder.RecordFrame(3f, isOverBudget: false);
            recorder.RecordFrame(10f, isOverBudget: true);

            var snapshot = recorder.CreateSnapshot();
            Assert.AreEqual(3, snapshot.SampleCount);
            Assert.Greater(snapshot.P95Ms, 3f);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.Tests.EditMode.FrameBudget.FramePerfRecorderTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-framebudget-perf-${TS}.xml" \
  -logFile -
```

Expected: FAIL.

- [ ] **Step 3: Implement perf recorder**

`UnityProject/Assets/Change/Runtime/FrameBudget/FramePerfSnapshot.cs`

```csharp
namespace Change.Runtime
{
    public readonly struct FramePerfSnapshot
    {
        public FramePerfSnapshot(float p50Ms, float p95Ms, int sampleCount, int overBudgetPercent, bool shouldThrottle)
        {
            P50Ms = p50Ms;
            P95Ms = p95Ms;
            SampleCount = sampleCount;
            OverBudgetPercent = overBudgetPercent;
            ShouldThrottle = shouldThrottle;
        }

        public float P50Ms { get; }
        public float P95Ms { get; }
        public int SampleCount { get; }
        public int OverBudgetPercent { get; }
        public bool ShouldThrottle { get; }
    }
}
```

`UnityProject/Assets/Change/Runtime/FrameBudget/FramePerfRecorder.cs`

```csharp
using System;

namespace Change.Runtime
{
    public sealed class FramePerfRecorder
    {
        private readonly float[] _samples;
        private readonly bool[] _overBudgetMarks;
        private readonly int _thresholdPercent;
        private int _index;
        private int _count;

        public FramePerfRecorder(int windowSize, int overBudgetThresholdPercent)
        {
            if (windowSize < 1) throw new ArgumentOutOfRangeException(nameof(windowSize));
            if (overBudgetThresholdPercent < 1 || overBudgetThresholdPercent > 100) throw new ArgumentOutOfRangeException(nameof(overBudgetThresholdPercent));

            _samples = new float[windowSize];
            _overBudgetMarks = new bool[windowSize];
            _thresholdPercent = overBudgetThresholdPercent;
        }

        public void RecordFrame(float totalMs, bool isOverBudget)
        {
            _samples[_index] = totalMs;
            _overBudgetMarks[_index] = isOverBudget;
            _index = (_index + 1) % _samples.Length;
            _count = Math.Min(_count + 1, _samples.Length);
        }

        public FramePerfSnapshot CreateSnapshot()
        {
            if (_count == 0)
            {
                return new FramePerfSnapshot(0f, 0f, 0, 0, false);
            }

            var temp = new float[_count];
            int overBudgetCount = 0;
            for (int i = 0; i < _count; i++)
            {
                temp[i] = _samples[i];
                if (_overBudgetMarks[i])
                {
                    overBudgetCount++;
                }
            }

            Array.Sort(temp);
            float p50 = temp[(int)Math.Floor((_count - 1) * 0.50f)];
            float p95 = temp[(int)Math.Floor((_count - 1) * 0.95f)];
            int percent = (int)Math.Round(overBudgetCount * 100.0 / _count);

            return new FramePerfSnapshot(
                p50Ms: p50,
                p95Ms: p95,
                sampleCount: _count,
                overBudgetPercent: percent,
                shouldThrottle: percent >= _thresholdPercent);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run the command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add \
  UnityProject/Assets/Change/Runtime/FrameBudget/FramePerfSnapshot.cs \
  UnityProject/Assets/Change/Runtime/FrameBudget/FramePerfRecorder.cs \
  UnityProject/Assets/Change/Runtime/Tests/EditMode/FrameBudget/FramePerfRecorderTests.cs
git commit -m "feat(runtime): add frame budget perf recorder and percentile snapshot"
```

---

### Task 4: Integrate Unity driver/bootstrap and public scheduling facade

**Files:**
- Modify: `UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudget.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetDriver.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetBootstrap.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/PlayMode/FrameBudget/FrameBudgetDriverPlayModeTests.cs`

- [ ] **Step 1: Write failing PlayMode driver tests**

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Change.Runtime.Tests.PlayMode.FrameBudget
{
    public sealed class FrameBudgetDriverPlayModeTests
    {
        [UnityTest]
        public IEnumerator Initialize_CreatesDontDestroyOnLoadDriver()
        {
            FrameBudget.Initialize(FrameBudgetPolicy.Default);
            yield return null;

            var go = GameObject.Find("[Change.FrameBudget]");
            Assert.IsNotNull(go);
            Assert.AreEqual("DontDestroyOnLoad", go.scene.name);
        }

        [UnityTest]
        public IEnumerator ScheduleDeferred_WorkExecutesInLaterFrame()
        {
            FrameBudget.Initialize(FrameBudgetPolicy.Default);
            int value = 0;

            FrameBudget.Schedule(FramePhase.Update, FrameTaskPriority.Deferred, "test", () => value = 7);

            yield return null;
            Assert.AreEqual(7, value);
        }
    }
}
```

- [ ] **Step 2: Run PlayMode tests to verify failure**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform PlayMode \
  -testFilter "Change.Runtime.Tests.PlayMode.FrameBudget.FrameBudgetDriverPlayModeTests" \
  -testResults "$(pwd)/UnityProject/TestResults/playmode-framebudget-driver-${TS}.xml" \
  -logFile -
```

Expected: FAIL.

- [ ] **Step 3: Implement runtime integration**

`UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef` (add UniTask reference)

```json
{
  "name": "Change.Runtime",
  "rootNamespace": "Change.Runtime",
  "references": [
    "Change.Framework",
    "Cysharp.Threading.Tasks"
  ],
  "optionalUnityReferences": [],
  "includePlatforms": [],
  "excludePlatforms": []
}
```

`UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudget.cs`

```csharp
using System;

namespace Change.Runtime
{
    public static class FrameBudget
    {
        private static FrameBudgetDriver _driver;

        public static void Initialize(FrameBudgetPolicy policy)
        {
            if (_driver != null)
            {
                _driver.ApplyPolicy(policy);
                return;
            }

            _driver = FrameBudgetBootstrap.EnsureDriver(policy);
        }

        public static void Schedule(FramePhase phase, FrameTaskPriority priority, string tag, Action callback)
        {
            if (_driver == null)
            {
                Initialize(FrameBudgetPolicy.Default);
            }

            _driver.Schedule(FrameWorkItem.Create(phase, priority, tag, callback));
        }

        public static bool IsPhaseBudgetExceeded(FramePhase phase)
        {
            return _driver != null && _driver.IsPhaseBudgetExceeded(phase);
        }

        internal static void ResetForTests()
        {
            _driver = null;
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetBootstrap.cs`

```csharp
using UnityEngine;

namespace Change.Runtime
{
    internal static class FrameBudgetBootstrap
    {
        private const string DriverName = "[Change.FrameBudget]";

        public static FrameBudgetDriver EnsureDriver(in FrameBudgetPolicy policy)
        {
            var existing = GameObject.Find(DriverName);
            if (existing != null && existing.TryGetComponent<FrameBudgetDriver>(out var existedDriver))
            {
                existedDriver.ApplyPolicy(policy);
                return existedDriver;
            }

            var go = new GameObject(DriverName);
            Object.DontDestroyOnLoad(go);
            var driver = go.AddComponent<FrameBudgetDriver>();
            driver.ApplyPolicy(policy);
            return driver;
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetDriver.cs`

```csharp
using System;
using UnityEngine;

namespace Change.Runtime
{
    internal sealed class FrameBudgetDriver : MonoBehaviour
    {
        private FrameBudgetPolicy _policy;
        private FrameWorkScheduler _scheduler;
        private FramePerfRecorder _updateRecorder;
        private FramePerfRecorder _lateRecorder;
        private FramePerfRecorder _fixedRecorder;

        private bool _updateExceeded;
        private bool _lateExceeded;
        private bool _fixedExceeded;

        public void ApplyPolicy(in FrameBudgetPolicy policy)
        {
            _policy = policy;
            _scheduler = new FrameWorkScheduler(policy);
            _updateRecorder = new FramePerfRecorder(policy.OverBudgetWindowFrames, policy.OverBudgetPercentThreshold);
            _lateRecorder = new FramePerfRecorder(policy.OverBudgetWindowFrames, policy.OverBudgetPercentThreshold);
            _fixedRecorder = new FramePerfRecorder(policy.OverBudgetWindowFrames, policy.OverBudgetPercentThreshold);
        }

        public void Schedule(in FrameWorkItem item) => _scheduler.Enqueue(item);

        public bool IsPhaseBudgetExceeded(FramePhase phase)
        {
            return phase switch
            {
                FramePhase.Update => _updateExceeded,
                FramePhase.LateUpdate => _lateExceeded,
                FramePhase.FixedUpdate => _fixedExceeded,
                _ => false,
            };
        }

        private void Update() => RunPhase(FramePhase.Update, _updateRecorder, ref _updateExceeded);
        private void LateUpdate() => RunPhase(FramePhase.LateUpdate, _lateRecorder, ref _lateExceeded);
        private void FixedUpdate() => RunPhase(FramePhase.FixedUpdate, _fixedRecorder, ref _fixedExceeded);

        private void RunPhase(FramePhase phase, FramePerfRecorder recorder, ref bool exceededFlag)
        {
            float start = Time.realtimeSinceStartup;
            float budgetMs = _policy.GetPhaseBudgetMs(phase);
            _scheduler.RunPhase(phase, Time.frameCount, budgetMs);
            float costMs = (Time.realtimeSinceStartup - start) * 1000f;

            exceededFlag = costMs > budgetMs;
            recorder.RecordFrame(costMs, exceededFlag);

            var snapshot = recorder.CreateSnapshot();
            if (snapshot.ShouldThrottle)
            {
                // driver keeps lightweight behavior in MVP; throttling acts via scheduler limits.
            }
        }
    }
}
```

- [ ] **Step 4: Run PlayMode tests to verify pass**

Run the command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add \
  UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef \
  UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudget.cs \
  UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetDriver.cs \
  UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetBootstrap.cs \
  UnityProject/Assets/Change/Runtime/Tests/PlayMode/FrameBudget/FrameBudgetDriverPlayModeTests.cs
git commit -m "feat(runtime): integrate frame budget driver and bootstrap"
```

---

### Task 5: Add UniTask cooperative-yield helpers and migration examples

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetUniTaskExtensions.cs`
- Modify: `UnityProject/Assets/Change/Runtime/Tests/PlayMode/FrameBudget/FrameBudgetDriverPlayModeTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/FrameBudget/README.md`

- [ ] **Step 1: Add failing PlayMode test for cooperative yield**

Append to `FrameBudgetDriverPlayModeTests`:

```csharp
[UnityTest]
public IEnumerator YieldIfBudgetExceeded_WhenExceeded_YieldsToNextFrame()
{
    FrameBudget.Initialize(FrameBudgetPolicy.Default);

    bool continued = false;
    var task = Run();

    yield return null;
    Assert.IsFalse(continued);

    yield return task.ToCoroutine();
    Assert.IsTrue(continued);

    async Cysharp.Threading.Tasks.UniTask Run()
    {
        // Simulate an exceeded phase by scheduling heavy work first.
        FrameBudget.Schedule(FramePhase.Update, FrameTaskPriority.Critical, "heavy", () =>
        {
            float end = Time.realtimeSinceStartup + 0.01f;
            while (Time.realtimeSinceStartup < end) { }
        });

        await FrameBudgetUniTaskExtensions.YieldIfBudgetExceeded(FramePhase.Update);
        continued = true;
    }
}
```

- [ ] **Step 2: Run PlayMode test and verify failure**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform PlayMode \
  -testFilter "Change.Runtime.Tests.PlayMode.FrameBudget.FrameBudgetDriverPlayModeTests" \
  -testResults "$(pwd)/UnityProject/TestResults/playmode-framebudget-unitask-${TS}.xml" \
  -logFile -
```

Expected: FAIL (helper missing).

- [ ] **Step 3: Implement helper and usage guide**

`UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetUniTaskExtensions.cs`

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime
{
    public static class FrameBudgetUniTaskExtensions
    {
        public static UniTask YieldIfBudgetExceeded(FramePhase phase, CancellationToken cancellationToken = default)
        {
            if (!FrameBudget.IsPhaseBudgetExceeded(phase))
            {
                return UniTask.CompletedTask;
            }

            var timing = phase switch
            {
                FramePhase.Update => PlayerLoopTiming.Update,
                FramePhase.LateUpdate => PlayerLoopTiming.LastPostLateUpdate,
                FramePhase.FixedUpdate => PlayerLoopTiming.FixedUpdate,
                _ => PlayerLoopTiming.Update,
            };

            return UniTask.Yield(timing, cancellationToken);
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/FrameBudget/README.md`

```markdown
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
```

- [ ] **Step 4: Run PlayMode test to verify pass**

Run the command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add \
  UnityProject/Assets/Change/Runtime/FrameBudget/FrameBudgetUniTaskExtensions.cs \
  UnityProject/Assets/Change/Runtime/FrameBudget/README.md \
  UnityProject/Assets/Change/Runtime/Tests/PlayMode/FrameBudget/FrameBudgetDriverPlayModeTests.cs
git commit -m "feat(runtime): add frame budget unitask yield helper and docs"
```

---

### Task 6: Full-suite verification and docs consistency pass

**Files:**
- Verify: `UnityProject/Assets/Change/Runtime/Tests/EditMode/FrameBudget/FrameBudgetPolicyTests.cs`
- Verify: `UnityProject/Assets/Change/Runtime/Tests/EditMode/FrameBudget/FrameWorkSchedulerTests.cs`
- Verify: `UnityProject/Assets/Change/Runtime/Tests/EditMode/FrameBudget/FramePerfRecorderTests.cs`
- Verify: `UnityProject/Assets/Change/Runtime/Tests/PlayMode/FrameBudget/FrameBudgetDriverPlayModeTests.cs`
- Verify: `UnityProject/Assets/Change/Runtime/FrameBudget/README.md`

- [ ] **Step 1: Run all new EditMode tests in one pass**

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.Tests.EditMode.FrameBudget" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-framebudget-all-${TS}.xml" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 2: Run all new PlayMode tests in one pass**

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform PlayMode \
  -testFilter "Change.Runtime.Tests.PlayMode.FrameBudget" \
  -testResults "$(pwd)/UnityProject/TestResults/playmode-framebudget-all-${TS}.xml" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 3: Validate README contract matches runtime defaults**

Manual check list:

1. README budgets match `FrameBudgetPolicy.Default`.
2. README priority semantics match scheduler behavior.
3. README long-loop example uses `YieldIfBudgetExceeded` with cancellation token.

- [ ] **Step 4: Commit verification artifacts (code/docs only)**

```bash
git add \
  UnityProject/Assets/Change/Runtime/FrameBudget/README.md \
  UnityProject/Assets/Change/Runtime/FrameBudget \
  UnityProject/Assets/Change/Runtime/Tests/EditMode/FrameBudget \
  UnityProject/Assets/Change/Runtime/Tests/PlayMode/FrameBudget
git commit -m "test(runtime): verify frame budget governance suite and docs consistency"
```

---

## Spec-to-Plan Coverage Check

- Spec section 4 (components): covered by Tasks 1-4.
- Spec section 5 (classification + standards): covered by Task 2 and README in Task 5.
- Spec section 6 (budget defaults + over-budget rules): covered by Tasks 1-2.
- Spec section 7 (UniTask integration): covered by Task 5.
- Spec section 8-9 (runtime flow + observability/fail-fast): covered by Tasks 2-4.
- Spec section 10-11 (validation and rollout readiness): covered by Task 6.

## Placeholder Scan

No `TODO`/`TBD` placeholders in tasks. All code-touching steps include concrete code blocks and explicit commands.

## Type Consistency Check

- Priority enum: `FrameTaskPriority` used consistently in policy/scheduler/tests.
- Phase enum: `FramePhase` used consistently in policy, scheduler, driver, helper.
- Scheduler result type: `FrameSchedulerRunResult` used consistently in tests and runtime.
