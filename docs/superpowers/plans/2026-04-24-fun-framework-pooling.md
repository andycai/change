# Fun.Framework Pooling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a production-grade static generic object pooling module under `Fun.Framework.Pooling` while preserving compatibility with existing `Fun.Framework.Collections.ObjectPool<T>`.

**Architecture:** Keep old and new pools fully isolated by namespace and runtime state. Implement `Pool<T>` as a type-level static pool with `IPoolable.Reset()` enforcement, configurable max size, and allocation-free hot path after warm-up. Add strict misuse validation only in Editor/Development builds via conditional compilation.

**Tech Stack:** C# (Unity 2022.3.60f1), Unity asmdef (`Fun.Framework` + `Fun.Framework.Tests`), NUnit EditMode tests.

---

## Scope Check

This spec is one subsystem (framework-level pure C# object pooling). No additional decomposition is required.

## File Structure (Create/Modify Map)

### Runtime files

- Create: `UnityProject/Assets/Fun/Framework/Runtime/Pooling/IPoolable.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Pooling/PoolDefaults.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Pooling/PoolStats.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Pooling/Internal/ReferenceEqualityComparer.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Pooling/Pool.cs`
- Modify: `UnityProject/Assets/Fun/Framework/README.md`

### Test files

- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Pooling/PoolCoreTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Pooling/PoolCapacityAndStatsTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Pooling/PoolStrictSafetyTests.cs`

### Shared command for test runs

```bash
UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.60f1/Unity.app/Contents/MacOS/Unity"
```

Use this command shape:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "<Filter>" \
  -logFile -
```

---

### Task 1: Create pooling contracts and pass core behavior tests

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Pooling/IPoolable.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Pooling/PoolDefaults.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Pooling/PoolStats.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Pooling/Pool.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Pooling/PoolCoreTests.cs`

- [ ] **Step 1: Write the failing core tests**

```csharp
using Fun.Framework.Pooling;
using NUnit.Framework;

namespace Fun.Framework.Tests.Pooling
{
    public class PoolCoreTests
    {
        private sealed class CorePayload : IPoolable
        {
            public int Value;
            public int ResetCount;

            public void Reset()
            {
                Value = 0;
                ResetCount++;
            }
        }

        [SetUp]
        public void SetUp()
        {
            Pool<CorePayload>.SetMaxSize(PoolDefaults.DefaultMaxSize);
            Pool<CorePayload>.Clear();
        }

        [Test]
        public void Get_WhenPoolIsEmpty_CreatesInstance()
        {
            var item = Pool<CorePayload>.Get();

            Assert.IsNotNull(item);
            Assert.AreEqual(0, Pool<CorePayload>.InactiveCount);
        }

        [Test]
        public void Release_ResetsAndReusesSameInstance()
        {
            var item = Pool<CorePayload>.Get();
            item.Value = 42;

            Pool<CorePayload>.Release(item);
            var reused = Pool<CorePayload>.Get();

            Assert.AreSame(item, reused);
            Assert.AreEqual(0, reused.Value);
            Assert.AreEqual(1, reused.ResetCount);
        }

        [Test]
        public void Clear_RemovesInactiveItems()
        {
            var a = Pool<CorePayload>.Get();
            var b = Pool<CorePayload>.Get();
            Pool<CorePayload>.Release(a);
            Pool<CorePayload>.Release(b);

            Pool<CorePayload>.Clear();

            Assert.AreEqual(0, Pool<CorePayload>.InactiveCount);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.Pooling.PoolCoreTests" \
  -logFile -
```

Expected: FAIL with compile errors because `Fun.Framework.Pooling` types do not exist.

- [ ] **Step 3: Implement minimal pooling runtime types**

`UnityProject/Assets/Fun/Framework/Runtime/Pooling/IPoolable.cs`

```csharp
namespace Fun.Framework.Pooling
{
    public interface IPoolable
    {
        void Reset();
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Pooling/PoolDefaults.cs`

```csharp
namespace Fun.Framework.Pooling
{
    public static class PoolDefaults
    {
        public const int DefaultMaxSize = 128;
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Pooling/PoolStats.cs`

```csharp
namespace Fun.Framework.Pooling
{
    public readonly struct PoolStats
    {
        public PoolStats(long created, long rented, long released, long dropped, int maxSize, int inactiveCount)
        {
            Created = created;
            Rented = rented;
            Released = released;
            Dropped = dropped;
            MaxSize = maxSize;
            InactiveCount = inactiveCount;
        }

        public long Created { get; }
        public long Rented { get; }
        public long Released { get; }
        public long Dropped { get; }
        public int MaxSize { get; }
        public int InactiveCount { get; }
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Pooling/Pool.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Fun.Framework.Pooling
{
    public static class Pool<T> where T : class, IPoolable, new()
    {
        private static readonly Stack<T> Inactive = new Stack<T>(PoolDefaults.DefaultMaxSize);
        private static int s_maxSize = PoolDefaults.DefaultMaxSize;

        private static long s_created;
        private static long s_rented;
        private static long s_released;
        private static long s_dropped;

        public static int InactiveCount => Inactive.Count;

        public static T Get()
        {
            s_rented++;

            if (Inactive.Count > 0)
            {
                return Inactive.Pop();
            }

            s_created++;
            return new T();
        }

        public static void Release(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

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
                s_created++;
                Inactive.Push(new T());
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
        }

        public static PoolStats GetStats()
        {
            return new PoolStats(s_created, s_rented, s_released, s_dropped, s_maxSize, Inactive.Count);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.Pooling.PoolCoreTests" \
  -logFile -
```

Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add \
  UnityProject/Assets/Fun/Framework/Runtime/Pooling/IPoolable.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Pooling/PoolDefaults.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Pooling/PoolStats.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Pooling/Pool.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Pooling/PoolCoreTests.cs
git commit -m "feat(pooling): add static Pool<T> core behavior"
```

---

### Task 2: Add capacity controls and stats verification

**Files:**
- Modify: `UnityProject/Assets/Fun/Framework/Runtime/Pooling/Pool.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Pooling/PoolCapacityAndStatsTests.cs`

- [ ] **Step 1: Write failing capacity and stats tests**

```csharp
using Fun.Framework.Pooling;
using NUnit.Framework;

namespace Fun.Framework.Tests.Pooling
{
    public class PoolCapacityAndStatsTests
    {
        private sealed class CapacityPayload : IPoolable
        {
            public void Reset() {}
        }

        [SetUp]
        public void SetUp()
        {
            Pool<CapacityPayload>.SetMaxSize(PoolDefaults.DefaultMaxSize);
            Pool<CapacityPayload>.Clear();
        }

        [Test]
        public void Prewarm_DoesNotExceedMaxSize()
        {
            Pool<CapacityPayload>.SetMaxSize(2);

            Pool<CapacityPayload>.Prewarm(8);

            Assert.AreEqual(2, Pool<CapacityPayload>.InactiveCount);
        }

        [Test]
        public void SetMaxSize_Shrink_ImmediatelyTrimsInactive()
        {
            Pool<CapacityPayload>.SetMaxSize(4);
            Pool<CapacityPayload>.Prewarm(4);

            Pool<CapacityPayload>.SetMaxSize(1);

            Assert.AreEqual(1, Pool<CapacityPayload>.InactiveCount);
        }

        [Test]
        public void Release_WhenFull_DropsObjectAndIncrementsDropped()
        {
            Pool<CapacityPayload>.SetMaxSize(1);
            var a = Pool<CapacityPayload>.Get();
            var b = Pool<CapacityPayload>.Get();
            var baseline = Pool<CapacityPayload>.GetStats();

            Pool<CapacityPayload>.Release(a);
            Pool<CapacityPayload>.Release(b);

            var after = Pool<CapacityPayload>.GetStats();
            Assert.AreEqual(1, Pool<CapacityPayload>.InactiveCount);
            Assert.AreEqual(baseline.Dropped + 1, after.Dropped);
        }

        [Test]
        public void GetStats_TracksCreatedRentedReleasedDeltas()
        {
            var before = Pool<CapacityPayload>.GetStats();
            var item = Pool<CapacityPayload>.Get();
            Pool<CapacityPayload>.Release(item);
            var after = Pool<CapacityPayload>.GetStats();

            Assert.AreEqual(before.Rented + 1, after.Rented);
            Assert.AreEqual(before.Released + 1, after.Released);
            Assert.GreaterOrEqual(after.Created, before.Created);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.Pooling.PoolCapacityAndStatsTests" \
  -logFile -
```

Expected: FAIL because capacity trimming / drop accounting is incomplete or mismatched.

- [ ] **Step 3: Harden `Pool<T>` capacity and stats behavior**

`UnityProject/Assets/Fun/Framework/Runtime/Pooling/Pool.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Fun.Framework.Pooling
{
    public static class Pool<T> where T : class, IPoolable, new()
    {
        private static readonly Stack<T> Inactive = new Stack<T>(PoolDefaults.DefaultMaxSize);
        private static int s_maxSize = PoolDefaults.DefaultMaxSize;

        private static long s_created;
        private static long s_rented;
        private static long s_released;
        private static long s_dropped;

        public static int InactiveCount => Inactive.Count;

        public static T Get()
        {
            s_rented++;

            if (Inactive.Count > 0)
            {
                return Inactive.Pop();
            }

            s_created++;
            return new T();
        }

        public static void Release(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

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
                Inactive.Push(new T());
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
        }

        public static PoolStats GetStats()
        {
            return new PoolStats(s_created, s_rented, s_released, s_dropped, s_maxSize, Inactive.Count);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.Pooling.PoolCapacityAndStatsTests" \
  -logFile -
```

Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add \
  UnityProject/Assets/Fun/Framework/Runtime/Pooling/Pool.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Pooling/PoolCapacityAndStatsTests.cs
git commit -m "test(pooling): lock capacity and stats semantics"
```

---

### Task 3: Implement dev-only strict safety checks for invalid release

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Pooling/Internal/ReferenceEqualityComparer.cs`
- Modify: `UnityProject/Assets/Fun/Framework/Runtime/Pooling/Pool.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Pooling/PoolStrictSafetyTests.cs`

- [ ] **Step 1: Write failing strict safety tests**

```csharp
using System;
using Fun.Framework.Pooling;
using NUnit.Framework;

namespace Fun.Framework.Tests.Pooling
{
    public class PoolStrictSafetyTests
    {
        private sealed class StrictPayload : IPoolable
        {
            public void Reset() {}
        }

        [SetUp]
        public void SetUp()
        {
            Pool<StrictPayload>.SetMaxSize(PoolDefaults.DefaultMaxSize);
            Pool<StrictPayload>.Clear();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Test]
        public void Release_SameInstanceTwice_ThrowsInvalidOperationException()
        {
            var item = Pool<StrictPayload>.Get();
            Pool<StrictPayload>.Release(item);

            Assert.Throws<InvalidOperationException>(() => Pool<StrictPayload>.Release(item));
        }

        [Test]
        public void Release_ForeignInstance_ThrowsInvalidOperationException()
        {
            var foreign = new StrictPayload();

            Assert.Throws<InvalidOperationException>(() => Pool<StrictPayload>.Release(foreign));
        }
#else
        [Test]
        public void StrictChecks_NotEnabledInThisBuildTarget()
        {
            Assert.Ignore("Strict checks are only enabled in Editor/Development builds.");
        }
#endif
    }
}
```

- [ ] **Step 2: Run strict safety tests and verify they fail**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.Pooling.PoolStrictSafetyTests" \
  -logFile -
```

Expected: FAIL because `Pool<T>.Release` does not yet validate ownership/double-release.

- [ ] **Step 3: Add dev-only in-use tracking and strict validation**

`UnityProject/Assets/Fun/Framework/Runtime/Pooling/Internal/ReferenceEqualityComparer.cs`

```csharp
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Fun.Framework.Pooling.Internal
{
    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

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

`UnityProject/Assets/Fun/Framework/Runtime/Pooling/Pool.cs`

```csharp
using System;
using System.Collections.Generic;
using Fun.Framework.Pooling.Internal;

namespace Fun.Framework.Pooling
{
    public static class Pool<T> where T : class, IPoolable, new()
    {
        private static readonly Stack<T> Inactive = new Stack<T>(PoolDefaults.DefaultMaxSize);
        private static int s_maxSize = PoolDefaults.DefaultMaxSize;

        private static long s_created;
        private static long s_rented;
        private static long s_released;
        private static long s_dropped;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly HashSet<T> InUse = new HashSet<T>(ReferenceEqualityComparer<T>.Instance);
#endif

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!InUse.Add(item))
            {
                throw new InvalidOperationException($"Pool<{typeof(T).FullName}>: same instance is already rented.");
            }
#endif

            s_rented++;
            return item;
        }

        public static void Release(T item)
        {
            if (item == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new ArgumentNullException(nameof(item));
#else
                return;
#endif
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!InUse.Remove(item))
            {
                throw new InvalidOperationException($"Pool<{typeof(T).FullName}>: releasing unknown or already released instance.");
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
                Inactive.Push(new T());
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            InUse.Clear();
#endif
        }

        public static PoolStats GetStats()
        {
            return new PoolStats(s_created, s_rented, s_released, s_dropped, s_maxSize, Inactive.Count);
        }
    }
}
```

- [ ] **Step 4: Run strict safety tests and verify they pass**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.Pooling.PoolStrictSafetyTests" \
  -logFile -
```

Expected: PASS in Editor.

- [ ] **Step 5: Commit**

```bash
git add \
  UnityProject/Assets/Fun/Framework/Runtime/Pooling/Internal/ReferenceEqualityComparer.cs \
  UnityProject/Assets/Fun/Framework/Runtime/Pooling/Pool.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Pooling/PoolStrictSafetyTests.cs
git commit -m "feat(pooling): add dev-only strict release validation"
```

---

### Task 4: Document new pooling API and run compatibility regression

**Files:**
- Modify: `UnityProject/Assets/Fun/Framework/README.md`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/ObjectPoolTests.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Pooling/PoolCoreTests.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Pooling/PoolCapacityAndStatsTests.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Pooling/PoolStrictSafetyTests.cs`

- [ ] **Step 1: Add README section for dual-track pooling**

`UnityProject/Assets/Fun/Framework/README.md` (append under Collections section)

````markdown
## Pooling (`Fun.Framework.Pooling`)

`Fun.Framework.Pooling` is the production static generic pool track for pure C# objects.

Core contract:

```csharp
public interface IPoolable
{
    void Reset();
}
```

Usage:

```csharp
using Fun.Framework.Pooling;

public sealed class DamageEvent : IPoolable
{
    public int SourceId;
    public int Value;

    public void Reset()
    {
        SourceId = 0;
        Value = 0;
    }
}

Pool<DamageEvent>.SetMaxSize(256);
Pool<DamageEvent>.Prewarm(64);

var evt = Pool<DamageEvent>.Get();
// use evt...
Pool<DamageEvent>.Release(evt);
```

Notes:
- `Pool<T>` requires `where T : class, IPoolable, new()`.
- Pool miss auto-creates via `new T()`.
- In Editor/Development builds, invalid release patterns fail fast with exceptions.
- Existing `Fun.Framework.Collections.ObjectPool<T>` remains supported for compatibility.
````

- [ ] **Step 2: Run full pooling + compatibility test set**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.Pooling|Fun.Framework.Tests.Collections.ObjectPoolTests" \
  -logFile -
```

Expected: PASS for new pooling tests and legacy `ObjectPoolTests`.

- [ ] **Step 3: Run targeted no-regression CQRS sanity tests**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.CommandDispatchTests|Fun.Framework.Tests.QueryDispatchTests|Fun.Framework.Tests.EventDispatchTests" \
  -logFile -
```

Expected: PASS (confirms new module did not impact existing framework runtime modules).

- [ ] **Step 4: Commit docs + test validation changes**

```bash
git add UnityProject/Assets/Fun/Framework/README.md
git commit -m "docs(pooling): document new Pool<T> API and coexistence model"
```

- [ ] **Step 5: Final verification snapshot**

Run:

```bash
git status --short
git log --oneline -n 6
```

Expected: clean working tree for this feature and visible task commits in order.

---

## Spec-to-Task Coverage Map

- Static generic entry (`Pool<T>`) -> Task 1
- `IPoolable.Reset()` strong reset contract -> Task 1
- Pool miss auto-create -> Task 1
- Configurable max size + immediate shrink trim -> Task 2
- Prewarm/Clear lifecycle APIs -> Task 1 + Task 2
- Dev strict checks for invalid release -> Task 3
- Release build downgrade behavior via conditional compile -> Task 3
- Per-type diagnostics (`PoolStats`) -> Task 1 + Task 2
- Legacy pool compatibility (dual track) -> Task 4

## Implementation Notes

- Keep `Pool<T>` independent from existing `Fun.Framework.Collections.ObjectPool<T>` internals to avoid hidden coupling.
- Do not add reflection-based registration or any global pool manager in MVP.
- Keep strict ownership validation behind `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; release builds run best-effort, non-throwing release flow.
- Preserve file-level single responsibility; avoid mixing docs/tests/runtime changes in one commit when possible.
