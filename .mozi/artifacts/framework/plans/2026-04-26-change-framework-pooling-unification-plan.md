# Change.Framework Pooling Unification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove duplicated pooling implementations and make `Change.Framework.Pooling` the single object-pool API and runtime source of truth.

**Architecture:** Keep static `Pool<T>` (`Get/Release`) as the only pooling entry. Delete `Collections.ObjectPool<T>` and `Collections.IResettable`, then migrate allocation coverage to `Pool<T>` while preserving fail-fast safety and steady-state zero-allocation expectations after warm-up. Add an architecture regression test that prevents future reintroduction of collection-side pooling types.

**Tech Stack:** C# (.NET Standard 2.1, Unity 2022.3), Unity Test Framework (EditMode), NUnit.

---

## Scope Check

This is one subsystem: framework object pooling unification. No additional decomposition is required.

## File Structure (Create/Modify/Delete Map)

### Runtime

- Modify: `UnityProject/Assets/Change/Framework/Pooling/Pool.cs`
  - Replace rented-state tracking internals with explicit reference-equality tracking suitable for zero-allocation hot paths after warm-up.
- Create: `UnityProject/Assets/Change/Framework/Pooling/Internal/ReferenceEqualityComparer.cs`
  - Provide reference-based comparer for tracking rented class instances safely.
- Delete: `UnityProject/Assets/Change/Framework/Collections/Containers/ObjectPool.cs`
  - Remove duplicate object-pool implementation.
- Delete: `UnityProject/Assets/Change/Framework/Collections/Containers/ObjectPool.cs.meta`
- Delete: `UnityProject/Assets/Change/Framework/Collections/Abstractions/IResettable.cs`
  - Remove duplicate reset protocol.
- Delete: `UnityProject/Assets/Change/Framework/Collections/Abstractions/IResettable.cs.meta`

### Tests

- Create: `UnityProject/Assets/Change/Framework/Tests/EditMode/Pooling/PoolArchitectureTests.cs`
  - Enforce architectural rule: no pooling types in `Change.Framework.Collections`.
- Modify: `UnityProject/Assets/Change/Framework/Tests/EditMode/Collections/Allocation/FastCollectionsAllocationTests.cs`
  - Replace allocation pool test from `ObjectPool<T>` to `Pool<T>`.
- Delete: `UnityProject/Assets/Change/Framework/Tests/EditMode/Collections/ObjectPoolTests.cs`
  - Remove tests for deleted type.
- Delete: `UnityProject/Assets/Change/Framework/Tests/EditMode/Collections/ObjectPoolTests.cs.meta`

### Docs

- Modify: `docs/superpowers/specs/2026-04-23-fun-framework-high-performance-collections-design.md`
  - Add historical note that object pooling is no longer part of Collections as of 2026-04-26.
- Modify: `docs/superpowers/plans/2026-04-24-fun-framework-pooling.md`
  - Add historical note that dual-track coexistence is superseded by unification.

### Shared test command setup

```bash
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
TS="$(date +%Y%m%d-%H%M%S)"
mkdir -p UnityProject/TestResults
```

All `-testResults` files must stay under `UnityProject/TestResults/` and use `<suite>-<yyyyMMdd-HHmmss>.xml` naming.

---

### Task 1: Add architecture regression tests (red)

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Tests/EditMode/Pooling/PoolArchitectureTests.cs`

- [ ] **Step 1: Write the failing architecture tests**

```csharp
using System;
using NUnit.Framework;

namespace Change.Framework.Tests.Pooling
{
    public class PoolArchitectureTests
    {
        [Test]
        public void Collections_ObjectPoolType_MustNotExist()
        {
            var objectPoolType = Type.GetType("Change.Framework.Collections.ObjectPool`1, Change.Framework");
            Assert.IsNull(objectPoolType, "Collections layer must not expose ObjectPool<T>.");
        }

        [Test]
        public void Collections_IResettableType_MustNotExist()
        {
            var resettableType = Type.GetType("Change.Framework.Collections.IResettable, Change.Framework");
            Assert.IsNull(resettableType, "Collections layer must not expose IResettable.");
        }
    }
}
```

- [ ] **Step 2: Run tests to verify red state**

Run:

```bash
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
TS="$(date +%Y%m%d-%H%M%S)"
RESULT="UnityProject/TestResults/editmode-pool-architecture-${TS}.xml"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.Pooling.PoolArchitectureTests" \
  -testResults "$RESULT" \
  -logFile -
```

Expected: FAIL with `Assert.IsNull` failures because `Collections.ObjectPool<T>` and `Collections.IResettable` still exist.

---

### Task 2: Migrate allocation path to Pooling and keep hot path allocation-free

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Pooling/Internal/ReferenceEqualityComparer.cs`
- Modify: `UnityProject/Assets/Change/Framework/Pooling/Pool.cs`
- Modify: `UnityProject/Assets/Change/Framework/Tests/EditMode/Collections/Allocation/FastCollectionsAllocationTests.cs`

- [ ] **Step 1: Write the failing allocation test against `Pool<T>`**

Replace the object-pool allocation test and payload in `FastCollectionsAllocationTests.cs` with this content:

```csharp
using Change.Framework.Collections;
using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests.Collections.Allocation
{
    public class FastCollectionsAllocationTests
    {
        [Test]
        public void FastList_AddNoResize_SteadyStateZeroAlloc()
        {
            var list = new FastList<int>(4096);
            for (var i = 0; i < 2048; i++)
            {
                list.AddNoResize(i);
            }

            list.Clear(ClearMode.Logical);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2048; i++)
            {
                list.AddNoResize(i);
            }

            list.Clear(ClearMode.Logical);
            var after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0L, after - before);
        }

        [Test]
        public void RingBuffer_EnqueueDequeue_SteadyStateZeroAlloc()
        {
            var rb = new RingBuffer<int>(2048);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2048; i++)
            {
                rb.EnqueueNoResize(i);
            }

            for (var i = 0; i < 2048; i++)
            {
                rb.TryDequeue(out _);
            }

            var after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0L, after - before);
        }

        [Test]
        public void FastDictionary_TryGetValue_SteadyStateZeroAlloc()
        {
            var map = new FastDictionary<int, int>(2048);
            for (var i = 0; i < 1024; i++)
            {
                map.TryAddNoResize(i, i);
            }

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1024; i++)
            {
                map.TryGetValue(i, out _);
            }

            var after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0L, after - before);
        }

        [Test]
        public void Pool_GetRelease_SteadyStateZeroAlloc()
        {
            Pool<PooledNode>.SetMaxSize(256);
            Pool<PooledNode>.Clear();
            Pool<PooledNode>.Prewarm(256);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 256; i++)
            {
                var value = Pool<PooledNode>.Get();
                Pool<PooledNode>.Release(value);
            }

            var after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0L, after - before);
        }

        private sealed class PooledNode : IPoolable
        {
            public int Value;

            public void Reset()
            {
                Value = 0;
            }
        }
    }
}
```

- [ ] **Step 2: Run allocation test and confirm it fails before pool internals are updated**

Run:

```bash
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
TS="$(date +%Y%m%d-%H%M%S)"
RESULT="UnityProject/TestResults/editmode-pool-allocation-red-${TS}.xml"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.Collections.Allocation.FastCollectionsAllocationTests.Pool_GetRelease_SteadyStateZeroAlloc" \
  -testResults "$RESULT" \
  -logFile -
```

Expected: FAIL (allocation regression) if rented tracking allocates on `Get/Release`.

- [ ] **Step 3: Implement allocation-safe rented tracking in `Pool<T>`**

Create `ReferenceEqualityComparer.cs`:

```csharp
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Change.Framework.Pooling.Internal
{
    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

        private ReferenceEqualityComparer()
        {
        }

        public bool Equals(T x, T y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
```

Replace `Pool.cs` content with:

```csharp
using System;
using System.Collections.Generic;
using Change.Framework.Pooling.Internal;

namespace Change.Framework.Pooling
{
    public static class Pool<T> where T : class, IPoolable, new()
    {
        // Not thread-safe; pool operations are expected on a single thread.
        private static readonly Stack<T> Inactive = new Stack<T>(PoolDefaults.DefaultMaxSize);
        private static readonly HashSet<T> s_rentedItems = new HashSet<T>(PoolDefaults.DefaultMaxSize, ReferenceEqualityComparer<T>.Instance);
        private static int s_maxSize = PoolDefaults.DefaultMaxSize;

        private static long s_created;
        private static long s_rented;
        private static long s_released;
        private static long s_dropped;

        public static int InactiveCount => Inactive.Count;

        public static T Get()
        {
            T item;
            if (Inactive.Count > 0)
            {
                item = Inactive.Pop();
            }
            else
            {
                item = new T();
                s_created++;
            }

            MarkRented(item);
            s_rented++;
            return item;
        }

        public static void Release(T item)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
#else
            if (item == null)
            {
                return;
            }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!TryMarkReleased(item))
            {
                throw new InvalidOperationException($"Cannot release instance of {typeof(T).FullName} that is not currently rented by this pool.");
            }
#else
            if (!TryMarkReleased(item))
            {
                return;
            }
#endif

            s_released++;
            item.Reset();

            if (Inactive.Count >= s_maxSize)
            {
                s_dropped++;
                return;
            }

            Inactive.Push(item);
        }

        public static void Prewarm(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var target = Math.Min(count, s_maxSize);
            while (Inactive.Count < target)
            {
                var created = new T();
                Inactive.Push(created);
                s_created++;
            }
        }

        public static void SetMaxSize(int maxSize)
        {
            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize));
            }

            s_maxSize = maxSize;
            while (Inactive.Count > s_maxSize)
            {
                Inactive.Pop();
            }
        }

        public static void Clear()
        {
            Inactive.Clear();
            s_rentedItems.Clear();
        }

        public static PoolStats GetStats()
        {
            return new PoolStats(s_created, s_rented, s_released, s_dropped, s_maxSize, Inactive.Count);
        }

        private static void MarkRented(T item)
        {
            if (!s_rentedItems.Add(item))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new InvalidOperationException($"Cannot rent instance of {typeof(T).FullName} because it is already marked as rented.");
#else
                return;
#endif
            }
        }

        private static bool TryMarkReleased(T item)
        {
            return s_rentedItems.Remove(item);
        }
    }
}
```

- [ ] **Step 4: Run allocation + pooling strict tests to verify green state**

Run:

```bash
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
TS="$(date +%Y%m%d-%H%M%S)"
RESULT="UnityProject/TestResults/editmode-pool-core-${TS}.xml"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.Collections.Allocation.FastCollectionsAllocationTests.Pool_GetRelease_SteadyStateZeroAlloc|Change.Framework.Tests.Pooling.PoolCoreTests|Change.Framework.Tests.Pooling.PoolCapacityAndStatsTests|Change.Framework.Tests.Pooling.PoolStrictSafetyTests" \
  -testResults "$RESULT" \
  -logFile -
```

Expected: PASS; XML exists at `UnityProject/TestResults/editmode-pool-core-<timestamp>.xml`.

- [ ] **Step 5: Commit task changes**

```bash
git add \
  UnityProject/Assets/Change/Framework/Pooling/Pool.cs \
  UnityProject/Assets/Change/Framework/Pooling/Internal/ReferenceEqualityComparer.cs \
  UnityProject/Assets/Change/Framework/Tests/EditMode/Collections/Allocation/FastCollectionsAllocationTests.cs

git commit -m "refactor(pooling): keep Pool<T> allocation-safe and migrate allocation test"
```

---

### Task 3: Remove collection-side pooling implementation and reset contract

**Files:**
- Delete: `UnityProject/Assets/Change/Framework/Collections/Containers/ObjectPool.cs`
- Delete: `UnityProject/Assets/Change/Framework/Collections/Containers/ObjectPool.cs.meta`
- Delete: `UnityProject/Assets/Change/Framework/Collections/Abstractions/IResettable.cs`
- Delete: `UnityProject/Assets/Change/Framework/Collections/Abstractions/IResettable.cs.meta`
- Delete: `UnityProject/Assets/Change/Framework/Tests/EditMode/Collections/ObjectPoolTests.cs`
- Delete: `UnityProject/Assets/Change/Framework/Tests/EditMode/Collections/ObjectPoolTests.cs.meta`

- [ ] **Step 1: Remove duplicate runtime and test artifacts**

Run:

```bash
git rm \
  UnityProject/Assets/Change/Framework/Collections/Containers/ObjectPool.cs \
  UnityProject/Assets/Change/Framework/Collections/Containers/ObjectPool.cs.meta \
  UnityProject/Assets/Change/Framework/Collections/Abstractions/IResettable.cs \
  UnityProject/Assets/Change/Framework/Collections/Abstractions/IResettable.cs.meta \
  UnityProject/Assets/Change/Framework/Tests/EditMode/Collections/ObjectPoolTests.cs \
  UnityProject/Assets/Change/Framework/Tests/EditMode/Collections/ObjectPoolTests.cs.meta
```

Expected: six deleted files are staged.

- [ ] **Step 2: Re-run architecture test (from Task 1) and pooling suite**

Run:

```bash
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
TS="$(date +%Y%m%d-%H%M%S)"
RESULT="UnityProject/TestResults/editmode-pool-unified-${TS}.xml"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.Pooling.PoolArchitectureTests|Change.Framework.Tests.Pooling.PoolCoreTests|Change.Framework.Tests.Pooling.PoolCapacityAndStatsTests|Change.Framework.Tests.Pooling.PoolStrictSafetyTests|Change.Framework.Tests.Collections.Allocation.FastCollectionsAllocationTests" \
  -testResults "$RESULT" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 3: Verify no source references remain**

Run:

```bash
rg -n "ObjectPool<|\bIResettable\b|ResetState\(" UnityProject/Assets/Change/Framework -g '*.cs'
```

Expected: no matches.

- [ ] **Step 4: Commit unification removals**

```bash
git add UnityProject/Assets/Change/Framework/Tests/EditMode/Pooling/PoolArchitectureTests.cs
git commit -m "refactor(framework): remove collections ObjectPool and IResettable"
```

---

### Task 4: Align documentation and finalize verification artifacts

**Files:**
- Modify: `docs/superpowers/specs/2026-04-23-fun-framework-high-performance-collections-design.md`
- Modify: `docs/superpowers/plans/2026-04-24-fun-framework-pooling.md`

- [ ] **Step 1: Add historical supersession note to collections spec**

Insert immediately below the title in `2026-04-23-fun-framework-high-performance-collections-design.md`:

```markdown
> Historical note (2026-04-26): Object pooling is no longer part of `Change.Framework.Collections`.
> `ObjectPool<T>`/`IResettable` were removed during pooling unification; use `Change.Framework.Pooling.Pool<T>` + `IPoolable`.
```

- [ ] **Step 2: Add historical supersession note to old pooling coexistence plan**

Insert immediately below the title in `2026-04-24-fun-framework-pooling.md`:

```markdown
> Historical note (2026-04-26): The dual-track coexistence model in this document is superseded.
> The active architecture uses a single pooling entry: `Change.Framework.Pooling.Pool<T>`.
```

- [ ] **Step 3: Run graph update required by repository rule**

Run:

```bash
graphify update .
```

Expected: graph update completes without error and updates `graphify-out/` artifacts as needed.

- [ ] **Step 4: Commit docs + graph updates**

```bash
git add \
  docs/superpowers/specs/2026-04-23-fun-framework-high-performance-collections-design.md \
  docs/superpowers/plans/2026-04-24-fun-framework-pooling.md \
  graphify-out

git commit -m "docs(framework): mark pooling unification and supersede dual-track notes"
```

---

## Final Verification Checklist

- [ ] Architecture test passes (`PoolArchitectureTests`).
- [ ] Pooling behavior tests pass (`PoolCore`, `PoolCapacityAndStats`, `PoolStrictSafety`).
- [ ] Allocation suite passes with `Pool<T>.Get/Release` steady-state test.
- [ ] `rg` confirms no `ObjectPool`/`IResettable` references in `Change.Framework` C# code.
- [ ] Graph updated via `graphify update .`.

## Rollback Plan

If regression appears after unification:

1. Revert the last commit (`git revert <sha>`) rather than force-reset.
2. Re-run the same Unity test filter used in Task 3 Step 2.
3. Re-introduce behavior via `Pool<T>` only (do not re-add `Collections.ObjectPool<T>`).

## Self-Review (Completed)

1. **Spec coverage:**
   - Single-entry pooling + immediate removals: Task 3
   - Static `Pool<T>` with `Get/Release`: Task 2
   - Remove `IResettable`: Task 3
   - Keep safety behavior and stats: Task 2 + Task 3 verification
   - Docs no longer describe dual-track as active: Task 4
2. **Placeholder scan:** No `TBD`, `TODO`, or deferred implementation placeholders remain.
3. **Type consistency:** Uses consistent names/signatures: `IPoolable.Reset()`, `Pool<T>.Get()`, `Pool<T>.Release()`.

