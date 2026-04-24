# Fun.Framework High Performance Collections Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a production-grade, pure-managed, single-threaded, hot-path-0GC collections package under `UnityProject/Assets/Fun/Framework/Collections` with six core containers and allocation tests.

**Architecture:** Implement shared guards/capacity policy once, then build containers with explicit `NoResize` APIs and struct enumerators. Keep APIs familiar to `List`/`Dictionary` where possible, but only expose a strict 0GC-safe subset. Validate behavior with EditMode unit tests and `GC.GetAllocatedBytesForCurrentThread()` steady-state allocation tests.

**Tech Stack:** C# (Unity 2022.3.60f1), Unity asmdef assemblies, NUnit/EditMode tests (`com.unity.test-framework`).

---

## Scope Check

The approved spec covers one coherent subsystem (framework-level managed collections). No decomposition is required for this plan.

## File Structure (Create/Modify Map)

### Runtime files

- Create: `UnityProject/Assets/Fun/Framework/Runtime/Fun.Framework.asmdef`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Abstractions/IClearable.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Abstractions/IResettable.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Core/ClearMode.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Core/CollectionGuards.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Core/GrowPolicy.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Diagnostics/CollectionMetrics.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastList.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastDictionary.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastHashSet.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/RingBuffer.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastPriorityQueue.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/ObjectPool.cs`
- Create: `UnityProject/Assets/Fun/Framework/README.md`

### Test files

- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Fun.Framework.Tests.asmdef`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/Core/CollectionCoreTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/FastListTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/FastDictionaryTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/FastHashSetTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/RingBufferTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/FastPriorityQueueTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/ObjectPoolTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/Allocation/FastCollectionsAllocationTests.cs`

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

### Task 1: Scaffold assembly + shared abstractions + core guards

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Fun.Framework.asmdef`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Abstractions/IClearable.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Abstractions/IResettable.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Core/ClearMode.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Core/CollectionGuards.cs`
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Core/GrowPolicy.cs`
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Fun.Framework.Tests.asmdef`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/Core/CollectionCoreTests.cs`

- [ ] **Step 1: Write the failing core tests**

```csharp
using System;
using Fun.Framework.Collections;
using NUnit.Framework;

namespace Fun.Framework.Tests.Collections
{
    public class CollectionCoreTests
    {
        [Test]
        public void GrowPolicy_DoublesAndRespectsMinimum()
        {
            Assert.AreEqual(4, GrowPolicy.Next(0, 4));
            Assert.AreEqual(8, GrowPolicy.Next(4, 5));
            Assert.AreEqual(16, GrowPolicy.Next(8, 9));
        }

        [Test]
        public void ThrowIfNegativeCapacity_ThrowsForNegativeValue()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CollectionGuards.ThrowIfNegativeCapacity(-1));
        }

        [Test]
        public void ThrowIfNegativeCapacity_DoesNotThrowForZeroOrPositive()
        {
            CollectionGuards.ThrowIfNegativeCapacity(0);
            CollectionGuards.ThrowIfNegativeCapacity(8);
        }

        [Test]
        public void CollectionMetrics_ResetClearsCounters()
        {
            CollectionMetrics.RecordFastListGrow();
            CollectionMetrics.RecordFastDictionaryGrow();
            CollectionMetrics.Reset();

            Assert.AreEqual(0, CollectionMetrics.FastListGrowCount);
            Assert.AreEqual(0, CollectionMetrics.FastDictionaryGrowCount);
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
  -testFilter "Fun.Framework.Tests.Collections.CollectionCoreTests" \
  -logFile -
```

Expected: FAIL with compile errors because `Fun.Framework.Collections` core types do not exist.

- [ ] **Step 3: Create asmdefs and shared core files**

`UnityProject/Assets/Fun/Framework/Runtime/Fun.Framework.asmdef`

```json
{
  "name": "Fun.Framework",
  "rootNamespace": "Fun.Framework",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

`UnityProject/Assets/Fun/Framework/Tests/EditMode/Fun.Framework.Tests.asmdef`

```json
{
  "name": "Fun.Framework.Tests",
  "rootNamespace": "Fun.Framework.Tests",
  "references": [
    "Fun.Framework"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false,
  "optionalUnityReferences": [
    "TestAssemblies"
  ]
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Collections/Abstractions/IClearable.cs`

```csharp
namespace Fun.Framework.Collections
{
    public interface IClearable
    {
        void Clear(ClearMode mode = ClearMode.Logical);
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Collections/Abstractions/IResettable.cs`

```csharp
namespace Fun.Framework.Collections
{
    public interface IResettable
    {
        void ResetState();
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Collections/Core/ClearMode.cs`

```csharp
namespace Fun.Framework.Collections
{
    public enum ClearMode
    {
        Logical = 0,
        ZeroMemory = 1
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Collections/Core/CollectionGuards.cs`

```csharp
using System;

namespace Fun.Framework.Collections
{
    internal static class CollectionGuards
    {
        public static void ThrowIfNegativeCapacity(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be >= 0.");
            }
        }

        public static void ThrowIfIndexOutOfRange(int index, int count)
        {
            if ((uint)index >= (uint)count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Index {index} out of range. Count={count}.");
            }
        }

        public static void ThrowIfNull(object value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Collections/Core/GrowPolicy.cs`

```csharp
using System;

namespace Fun.Framework.Collections
{
    internal static class GrowPolicy
    {
        public static int Next(int currentCapacity, int minimum)
        {
            CollectionGuards.ThrowIfNegativeCapacity(currentCapacity);
            CollectionGuards.ThrowIfNegativeCapacity(minimum);

            var next = currentCapacity == 0 ? 4 : currentCapacity * 2;
            if (next < minimum)
            {
                next = minimum;
            }

            if (next < 0)
            {
                next = int.MaxValue;
            }

            return Math.Max(next, 4);
        }
    }
}
```

`UnityProject/Assets/Fun/Framework/Runtime/Collections/Diagnostics/CollectionMetrics.cs`

```csharp
namespace Fun.Framework.Collections
{
    public static class CollectionMetrics
    {
        public static int FastListGrowCount { get; private set; }
        public static int FastDictionaryGrowCount { get; private set; }

        public static void RecordFastListGrow()
        {
            FastListGrowCount++;
        }

        public static void RecordFastDictionaryGrow()
        {
            FastDictionaryGrowCount++;
        }

        public static void Reset()
        {
            FastListGrowCount = 0;
            FastDictionaryGrowCount = 0;
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
  -testFilter "Fun.Framework.Tests.Collections.CollectionCoreTests" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Fun/Framework/Runtime/Fun.Framework.asmdef \
  UnityProject/Assets/Fun/Framework/Runtime/Collections \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Fun.Framework.Tests.asmdef \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/Core/CollectionCoreTests.cs
git commit -m "feat(collections): scaffold core abstractions and guards"
```

---

### Task 2: Implement `FastList<T>` with no-resize path and struct enumerator

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastList.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/FastListTests.cs`

- [ ] **Step 1: Write the failing `FastList` tests**

```csharp
using System;
using Fun.Framework.Collections;
using NUnit.Framework;

namespace Fun.Framework.Tests.Collections
{
    public class FastListTests
    {
        [Test]
        public void AddNoResize_ThrowsWhenCapacityIsFull()
        {
            var list = new FastList<int>(1);
            list.AddNoResize(10);

            Assert.Throws<InvalidOperationException>(() => list.AddNoResize(11));
        }

        [Test]
        public void RemoveAtSwapBack_KeepsDenseStorage()
        {
            var list = new FastList<int>(4);
            list.Add(1);
            list.Add(2);
            list.Add(3);

            list.RemoveAtSwapBack(0);

            Assert.AreEqual(2, list.Count);
            Assert.IsTrue(list.Contains(2));
            Assert.IsTrue(list.Contains(3));
        }

        [Test]
        public void Enumerator_ThrowsWhenCollectionModified()
        {
            var list = new FastList<int>(4);
            list.Add(1);
            list.Add(2);

            var e = list.GetEnumerator();
            Assert.IsTrue(e.MoveNext());
            list.Add(3);

            Assert.Throws<InvalidOperationException>(() => e.MoveNext());
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
  -testFilter "Fun.Framework.Tests.Collections.FastListTests" \
  -logFile -
```

Expected: FAIL with missing `FastList<T>` type.

- [ ] **Step 3: Implement `FastList<T>`**

`UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastList.cs`

```csharp
using System;

namespace Fun.Framework.Collections
{
    public sealed class FastList<T> : IClearable
    {
        private T[] _items;
        private int _count;
        private int _version;

        public FastList(int capacity = 4)
        {
            CollectionGuards.ThrowIfNegativeCapacity(capacity);
            _items = capacity == 0 ? Array.Empty<T>() : new T[capacity];
            _count = 0;
            _version = 0;
        }

        public int Count => _count;
        public int Capacity => _items.Length;

        public T this[int index]
        {
            get
            {
                CollectionGuards.ThrowIfIndexOutOfRange(index, _count);
                return _items[index];
            }
            set
            {
                CollectionGuards.ThrowIfIndexOutOfRange(index, _count);
                _items[index] = value;
                _version++;
            }
        }

        public void EnsureCapacity(int minimum)
        {
            if (minimum <= _items.Length)
            {
                return;
            }

            var next = GrowPolicy.Next(_items.Length, minimum);
            Array.Resize(ref _items, next);
            CollectionMetrics.RecordFastListGrow();
        }

        public void Add(T value)
        {
            if (_count == _items.Length)
            {
                EnsureCapacity(_count + 1);
            }

            _items[_count++] = value;
            _version++;
        }

        public void AddNoResize(T value)
        {
            if (_count == _items.Length)
            {
                throw new InvalidOperationException($"FastList capacity exceeded. Count={_count}, Capacity={_items.Length}.");
            }

            _items[_count++] = value;
            _version++;
        }

        public bool Contains(T value)
        {
            return IndexOf(value) >= 0;
        }

        public int IndexOf(T value)
        {
            return Array.IndexOf(_items, value, 0, _count);
        }

        public void RemoveAt(int index)
        {
            CollectionGuards.ThrowIfIndexOutOfRange(index, _count);
            var moveCount = _count - index - 1;
            if (moveCount > 0)
            {
                Array.Copy(_items, index + 1, _items, index, moveCount);
            }

            _count--;
            _items[_count] = default;
            _version++;
        }

        public void RemoveAtSwapBack(int index)
        {
            CollectionGuards.ThrowIfIndexOutOfRange(index, _count);
            var lastIndex = _count - 1;
            _items[index] = _items[lastIndex];
            _items[lastIndex] = default;
            _count--;
            _version++;
        }

        public void Clear(ClearMode mode = ClearMode.Logical)
        {
            if (mode == ClearMode.ZeroMemory)
            {
                Array.Clear(_items, 0, _count);
            }

            _count = 0;
            _version++;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        public struct Enumerator
        {
            private readonly FastList<T> _list;
            private readonly int _version;
            private int _index;
            private T _current;

            internal Enumerator(FastList<T> list)
            {
                _list = list;
                _version = list._version;
                _index = 0;
                _current = default;
            }

            public T Current => _current;

            public bool MoveNext()
            {
                if (_version != _list._version)
                {
                    throw new InvalidOperationException("Enumerator invalid because collection was modified.");
                }

                if (_index < _list._count)
                {
                    _current = _list._items[_index++];
                    return true;
                }

                return false;
            }
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
  -testFilter "Fun.Framework.Tests.Collections.FastListTests" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastList.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/FastListTests.cs
git commit -m "feat(collections): add FastList with NoResize and struct enumerator"
```

---

### Task 3: Implement `FastDictionary<TKey,TValue>`

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastDictionary.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/FastDictionaryTests.cs`

- [ ] **Step 1: Write failing `FastDictionary` tests**

```csharp
using System;
using Fun.Framework.Collections;
using NUnit.Framework;

namespace Fun.Framework.Tests.Collections
{
    public class FastDictionaryTests
    {
        [Test]
        public void TryAddAndTryGetValue_WorkForUniqueKeys()
        {
            var map = new FastDictionary<int, string>(4);

            Assert.IsTrue(map.TryAdd(1, "one"));
            Assert.IsTrue(map.TryGetValue(1, out var value));
            Assert.AreEqual("one", value);
        }

        [Test]
        public void TryAddNoResize_ReturnsFalseWhenCapacityFull()
        {
            var map = new FastDictionary<int, string>(1);
            Assert.IsTrue(map.TryAddNoResize(1, "one"));
            Assert.IsFalse(map.TryAddNoResize(2, "two"));
        }

        [Test]
        public void Remove_DeletesEntry()
        {
            var map = new FastDictionary<int, string>(4);
            map.TryAdd(7, "x");

            Assert.IsTrue(map.Remove(7));
            Assert.IsFalse(map.TryGetValue(7, out _));
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
  -testFilter "Fun.Framework.Tests.Collections.FastDictionaryTests" \
  -logFile -
```

Expected: FAIL with missing `FastDictionary<TKey,TValue>` type.

- [ ] **Step 3: Implement `FastDictionary<TKey,TValue>`**

`UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastDictionary.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Fun.Framework.Collections
{
    public sealed class FastDictionary<TKey, TValue> : IClearable
    {
        private struct Entry
        {
            public int HashCode;
            public int Next;
            public TKey Key;
            public TValue Value;
        }

        private int[] _buckets;
        private Entry[] _entries;
        private int _count;
        private int _freeList;
        private int _freeCount;
        private int _version;
        private readonly IEqualityComparer<TKey> _comparer;

        public FastDictionary(int capacity = 4, IEqualityComparer<TKey> comparer = null)
        {
            CollectionGuards.ThrowIfNegativeCapacity(capacity);
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            _buckets = capacity == 0 ? Array.Empty<int>() : new int[capacity];
            _entries = capacity == 0 ? Array.Empty<Entry>() : new Entry[capacity];
            _count = 0;
            _freeList = -1;
            _freeCount = 0;
            _version = 0;
        }

        public int Count => _count - _freeCount;
        public int Capacity => _entries.Length;

        public bool TryAdd(TKey key, TValue value)
        {
            return TryInsert(key, value, allowResize: true, overwrite: false);
        }

        public bool TryAddNoResize(TKey key, TValue value)
        {
            return TryInsert(key, value, allowResize: false, overwrite: false);
        }

        public bool ContainsKey(TKey key)
        {
            return FindEntryIndex(key) >= 0;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            var index = FindEntryIndex(key);
            if (index >= 0)
            {
                value = _entries[index].Value;
                return true;
            }

            value = default;
            return false;
        }

        public bool Remove(TKey key)
        {
            if (_buckets.Length == 0)
            {
                return false;
            }

            var hashCode = _comparer.GetHashCode(key) & 0x7fffffff;
            var bucket = hashCode % _buckets.Length;
            var previous = -1;
            var current = _buckets[bucket] - 1;

            while (current >= 0)
            {
                ref var entry = ref _entries[current];
                if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                {
                    if (previous < 0)
                    {
                        _buckets[bucket] = entry.Next + 1;
                    }
                    else
                    {
                        _entries[previous].Next = entry.Next;
                    }

                    entry.HashCode = -1;
                    entry.Next = _freeList;
                    entry.Key = default;
                    entry.Value = default;
                    _freeList = current;
                    _freeCount++;
                    _version++;
                    return true;
                }

                previous = current;
                current = entry.Next;
            }

            return false;
        }

        public void Clear(ClearMode mode = ClearMode.Logical)
        {
            if (_count == 0)
            {
                return;
            }

            Array.Clear(_buckets, 0, _buckets.Length);
            if (mode == ClearMode.ZeroMemory)
            {
                Array.Clear(_entries, 0, _count);
            }

            _count = 0;
            _freeList = -1;
            _freeCount = 0;
            _version++;
        }

        private bool TryInsert(TKey key, TValue value, bool allowResize, bool overwrite)
        {
            if (_buckets.Length == 0)
            {
                if (!allowResize)
                {
                    return false;
                }

                Resize(4);
            }

            var hashCode = _comparer.GetHashCode(key) & 0x7fffffff;
            var bucket = hashCode % _buckets.Length;

            for (var i = _buckets[bucket] - 1; i >= 0; i = _entries[i].Next)
            {
                if (_entries[i].HashCode == hashCode && _comparer.Equals(_entries[i].Key, key))
                {
                    if (!overwrite)
                    {
                        return false;
                    }

                    _entries[i].Value = value;
                    _version++;
                    return true;
                }
            }

            int index;
            if (_freeCount > 0)
            {
                index = _freeList;
                _freeList = _entries[index].Next;
                _freeCount--;
            }
            else
            {
                if (_count == _entries.Length)
                {
                    if (!allowResize)
                    {
                        return false;
                    }

                    Resize(GrowPolicy.Next(_entries.Length, _count + 1));
                    bucket = hashCode % _buckets.Length;
                }

                index = _count;
                _count++;
            }

            _entries[index].HashCode = hashCode;
            _entries[index].Next = _buckets[bucket] - 1;
            _entries[index].Key = key;
            _entries[index].Value = value;
            _buckets[bucket] = index + 1;
            _version++;
            return true;
        }

        private int FindEntryIndex(TKey key)
        {
            if (_buckets.Length == 0)
            {
                return -1;
            }

            var hashCode = _comparer.GetHashCode(key) & 0x7fffffff;
            for (var i = _buckets[hashCode % _buckets.Length] - 1; i >= 0; i = _entries[i].Next)
            {
                if (_entries[i].HashCode == hashCode && _comparer.Equals(_entries[i].Key, key))
                {
                    return i;
                }
            }

            return -1;
        }

        private void Resize(int newSize)
        {
            var newBuckets = new int[newSize];
            var newEntries = new Entry[newSize];

            if (_count > 0)
            {
                Array.Copy(_entries, 0, newEntries, 0, _count);
                for (var i = 0; i < _count; i++)
                {
                    if (newEntries[i].HashCode < 0)
                    {
                        continue;
                    }

                    var bucket = newEntries[i].HashCode % newSize;
                    newEntries[i].Next = newBuckets[bucket] - 1;
                    newBuckets[bucket] = i + 1;
                }
            }

            _buckets = newBuckets;
            _entries = newEntries;
            CollectionMetrics.RecordFastDictionaryGrow();
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
  -testFilter "Fun.Framework.Tests.Collections.FastDictionaryTests" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastDictionary.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/FastDictionaryTests.cs
git commit -m "feat(collections): add FastDictionary with no-resize insert path"
```

---

### Task 4: Implement `FastHashSet<T>`

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastHashSet.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/FastHashSetTests.cs`

- [ ] **Step 1: Write failing `FastHashSet` tests**

```csharp
using Fun.Framework.Collections;
using NUnit.Framework;

namespace Fun.Framework.Tests.Collections
{
    public class FastHashSetTests
    {
        [Test]
        public void AddContainsRemove_WorkAsExpected()
        {
            var set = new FastHashSet<int>(4);

            Assert.IsTrue(set.Add(10));
            Assert.IsFalse(set.Add(10));
            Assert.IsTrue(set.Contains(10));
            Assert.IsTrue(set.Remove(10));
            Assert.IsFalse(set.Contains(10));
        }

        [Test]
        public void AddNoResize_ReturnsFalseWhenFull()
        {
            var set = new FastHashSet<int>(1);
            Assert.IsTrue(set.AddNoResize(1));
            Assert.IsFalse(set.AddNoResize(2));
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
  -testFilter "Fun.Framework.Tests.Collections.FastHashSetTests" \
  -logFile -
```

Expected: FAIL with missing `FastHashSet<T>` type.

- [ ] **Step 3: Implement `FastHashSet<T>` via `FastDictionary<T, byte>` composition**

`UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastHashSet.cs`

```csharp
using System.Collections.Generic;

namespace Fun.Framework.Collections
{
    public sealed class FastHashSet<T> : IClearable
    {
        private readonly FastDictionary<T, byte> _map;

        public FastHashSet(int capacity = 4, IEqualityComparer<T> comparer = null)
        {
            _map = new FastDictionary<T, byte>(capacity, comparer);
        }

        public int Count => _map.Count;

        public bool Add(T value)
        {
            return _map.TryAdd(value, 1);
        }

        public bool AddNoResize(T value)
        {
            return _map.TryAddNoResize(value, 1);
        }

        public bool Contains(T value)
        {
            return _map.ContainsKey(value);
        }

        public bool Remove(T value)
        {
            return _map.Remove(value);
        }

        public void Clear(ClearMode mode = ClearMode.Logical)
        {
            _map.Clear(mode);
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
  -testFilter "Fun.Framework.Tests.Collections.FastHashSetTests" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastHashSet.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/FastHashSetTests.cs
git commit -m "feat(collections): add FastHashSet"
```

---

### Task 5: Implement fixed-capacity `RingBuffer<T>`

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/RingBuffer.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/RingBufferTests.cs`

- [ ] **Step 1: Write failing `RingBuffer` tests**

```csharp
using Fun.Framework.Collections;
using NUnit.Framework;

namespace Fun.Framework.Tests.Collections
{
    public class RingBufferTests
    {
        [Test]
        public void EnqueueNoResize_ReturnsFalseWhenFull()
        {
            var q = new RingBuffer<int>(2);
            Assert.IsTrue(q.EnqueueNoResize(1));
            Assert.IsTrue(q.EnqueueNoResize(2));
            Assert.IsFalse(q.EnqueueNoResize(3));
        }

        [Test]
        public void TryDequeue_FollowsFifoAcrossWrapAround()
        {
            var q = new RingBuffer<int>(3);
            q.Enqueue(1);
            q.Enqueue(2);
            Assert.IsTrue(q.TryDequeue(out var a));
            q.Enqueue(3);
            q.Enqueue(4);

            Assert.AreEqual(1, a);
            Assert.IsTrue(q.TryDequeue(out var b));
            Assert.IsTrue(q.TryDequeue(out var c));
            Assert.IsTrue(q.TryDequeue(out var d));
            Assert.AreEqual(2, b);
            Assert.AreEqual(3, c);
            Assert.AreEqual(4, d);
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
  -testFilter "Fun.Framework.Tests.Collections.RingBufferTests" \
  -logFile -
```

Expected: FAIL with missing `RingBuffer<T>` type.

- [ ] **Step 3: Implement fixed-capacity `RingBuffer<T>`**

`UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/RingBuffer.cs`

```csharp
using System;

namespace Fun.Framework.Collections
{
    public sealed class RingBuffer<T> : IClearable
    {
        private readonly T[] _buffer;
        private int _head;
        private int _tail;
        private int _count;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be > 0.");
            }

            _buffer = new T[capacity];
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        public int Count => _count;
        public int Capacity => _buffer.Length;

        public void Enqueue(T value)
        {
            if (!EnqueueNoResize(value))
            {
                throw new InvalidOperationException($"RingBuffer is full. Count={_count}, Capacity={_buffer.Length}.");
            }
        }

        public bool EnqueueNoResize(T value)
        {
            if (_count == _buffer.Length)
            {
                return false;
            }

            _buffer[_tail] = value;
            _tail++;
            if (_tail == _buffer.Length)
            {
                _tail = 0;
            }

            _count++;
            return true;
        }

        public bool TryDequeue(out T value)
        {
            if (_count == 0)
            {
                value = default;
                return false;
            }

            value = _buffer[_head];
            _buffer[_head] = default;
            _head++;
            if (_head == _buffer.Length)
            {
                _head = 0;
            }

            _count--;
            return true;
        }

        public bool TryPeek(out T value)
        {
            if (_count == 0)
            {
                value = default;
                return false;
            }

            value = _buffer[_head];
            return true;
        }

        public void Clear(ClearMode mode = ClearMode.Logical)
        {
            if (mode == ClearMode.ZeroMemory)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
            }

            _head = 0;
            _tail = 0;
            _count = 0;
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
  -testFilter "Fun.Framework.Tests.Collections.RingBufferTests" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/RingBuffer.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/RingBufferTests.cs
git commit -m "feat(collections): add fixed-capacity RingBuffer"
```

---

### Task 6: Implement `FastPriorityQueue<T>`

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastPriorityQueue.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/FastPriorityQueueTests.cs`

- [ ] **Step 1: Write failing priority queue tests**

```csharp
using Fun.Framework.Collections;
using NUnit.Framework;

namespace Fun.Framework.Tests.Collections
{
    public class FastPriorityQueueTests
    {
        [Test]
        public void TryDequeue_ReturnsItemsInAscendingOrder()
        {
            var pq = new FastPriorityQueue<int>(8);
            pq.Enqueue(5);
            pq.Enqueue(1);
            pq.Enqueue(3);

            Assert.IsTrue(pq.TryDequeue(out var a));
            Assert.IsTrue(pq.TryDequeue(out var b));
            Assert.IsTrue(pq.TryDequeue(out var c));
            Assert.AreEqual(1, a);
            Assert.AreEqual(3, b);
            Assert.AreEqual(5, c);
        }

        [Test]
        public void EnqueueNoResize_ReturnsFalseWhenCapacityReached()
        {
            var pq = new FastPriorityQueue<int>(1);
            Assert.IsTrue(pq.EnqueueNoResize(10));
            Assert.IsFalse(pq.EnqueueNoResize(11));
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
  -testFilter "Fun.Framework.Tests.Collections.FastPriorityQueueTests" \
  -logFile -
```

Expected: FAIL with missing `FastPriorityQueue<T>` type.

- [ ] **Step 3: Implement `FastPriorityQueue<T>`**

`UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastPriorityQueue.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Fun.Framework.Collections
{
    public sealed class FastPriorityQueue<T> : IClearable
    {
        private T[] _heap;
        private int _count;
        private readonly IComparer<T> _comparer;

        public FastPriorityQueue(int capacity = 4, IComparer<T> comparer = null)
        {
            CollectionGuards.ThrowIfNegativeCapacity(capacity);
            _heap = capacity == 0 ? Array.Empty<T>() : new T[capacity];
            _count = 0;
            _comparer = comparer ?? Comparer<T>.Default;
        }

        public int Count => _count;
        public int Capacity => _heap.Length;

        public void Enqueue(T value)
        {
            if (_count == _heap.Length)
            {
                EnsureCapacity(_count + 1);
            }

            InsertAtTail(value);
        }

        public bool EnqueueNoResize(T value)
        {
            if (_count == _heap.Length)
            {
                return false;
            }

            InsertAtTail(value);
            return true;
        }

        public bool TryPeek(out T value)
        {
            if (_count == 0)
            {
                value = default;
                return false;
            }

            value = _heap[0];
            return true;
        }

        public bool TryDequeue(out T value)
        {
            if (_count == 0)
            {
                value = default;
                return false;
            }

            value = _heap[0];
            _count--;
            if (_count > 0)
            {
                _heap[0] = _heap[_count];
                _heap[_count] = default;
                SiftDown(0);
            }
            else
            {
                _heap[0] = default;
            }

            return true;
        }

        public void Clear(ClearMode mode = ClearMode.Logical)
        {
            if (mode == ClearMode.ZeroMemory)
            {
                Array.Clear(_heap, 0, _count);
            }

            _count = 0;
        }

        private void EnsureCapacity(int minimum)
        {
            if (minimum <= _heap.Length)
            {
                return;
            }

            Array.Resize(ref _heap, GrowPolicy.Next(_heap.Length, minimum));
        }

        private void InsertAtTail(T value)
        {
            var index = _count;
            _count++;
            while (index > 0)
            {
                var parent = (index - 1) / 2;
                if (_comparer.Compare(value, _heap[parent]) >= 0)
                {
                    break;
                }

                _heap[index] = _heap[parent];
                index = parent;
            }

            _heap[index] = value;
        }

        private void SiftDown(int index)
        {
            var value = _heap[index];
            while (true)
            {
                var left = index * 2 + 1;
                if (left >= _count)
                {
                    break;
                }

                var right = left + 1;
                var best = right < _count && _comparer.Compare(_heap[right], _heap[left]) < 0 ? right : left;
                if (_comparer.Compare(_heap[best], value) >= 0)
                {
                    break;
                }

                _heap[index] = _heap[best];
                index = best;
            }

            _heap[index] = value;
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
  -testFilter "Fun.Framework.Tests.Collections.FastPriorityQueueTests" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/FastPriorityQueue.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/FastPriorityQueueTests.cs
git commit -m "feat(collections): add FastPriorityQueue"
```

---

### Task 7: Implement `ObjectPool<T>` with prewarm/reset semantics

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/ObjectPool.cs`
- Test: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/ObjectPoolTests.cs`

- [ ] **Step 1: Write failing pool tests**

```csharp
using System;
using Fun.Framework.Collections;
using NUnit.Framework;

namespace Fun.Framework.Tests.Collections
{
    public class ObjectPoolTests
    {
        private sealed class Payload : IResettable
        {
            public int Value;

            public void ResetState()
            {
                Value = 0;
            }
        }

        [Test]
        public void RentReturn_ReusesInstanceAndResetsState()
        {
            var pool = new ObjectPool<Payload>(() => new Payload(), 4);
            var p = pool.Rent();
            p.Value = 9;
            pool.Return(p);

            var reused = pool.Rent();
            Assert.AreSame(p, reused);
            Assert.AreEqual(0, reused.Value);
        }

        [Test]
        public void Prewarm_CreatesRequestedCount()
        {
            var created = 0;
            var pool = new ObjectPool<Payload>(() => { created++; return new Payload(); }, 8);

            pool.Prewarm(4);

            Assert.AreEqual(4, created);
            Assert.AreEqual(4, pool.InactiveCount);
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
  -testFilter "Fun.Framework.Tests.Collections.ObjectPoolTests" \
  -logFile -
```

Expected: FAIL with missing `ObjectPool<T>`.

- [ ] **Step 3: Implement `ObjectPool<T>`**

`UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/ObjectPool.cs`

```csharp
using System;

namespace Fun.Framework.Collections
{
    public sealed class ObjectPool<T> where T : class
    {
        private readonly FastList<T> _stack;
        private readonly Func<T> _factory;
        private readonly int _maxSize;

        public ObjectPool(Func<T> factory, int maxSize)
        {
            CollectionGuards.ThrowIfNull(factory, nameof(factory));
            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "Max size must be > 0.");
            }

            _factory = factory;
            _maxSize = maxSize;
            _stack = new FastList<T>(maxSize);
        }

        public int InactiveCount => _stack.Count;

        public T Rent()
        {
            if (_stack.Count == 0)
            {
                return _factory();
            }

            var lastIndex = _stack.Count - 1;
            var value = _stack[lastIndex];
            _stack.RemoveAt(lastIndex);
            return value;
        }

        public void Return(T value)
        {
            CollectionGuards.ThrowIfNull(value, nameof(value));

            if (value is IResettable resettable)
            {
                resettable.ResetState();
            }

            if (_stack.Count < _maxSize)
            {
                _stack.AddNoResize(value);
            }
        }

        public void Prewarm(int count)
        {
            CollectionGuards.ThrowIfNegativeCapacity(count);
            if (count > _maxSize)
            {
                count = _maxSize;
            }

            while (_stack.Count < count)
            {
                _stack.AddNoResize(_factory());
            }
        }

        public void TrimExcess(int targetCount)
        {
            CollectionGuards.ThrowIfNegativeCapacity(targetCount);
            while (_stack.Count > targetCount)
            {
                _stack.RemoveAt(_stack.Count - 1);
            }
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
  -testFilter "Fun.Framework.Tests.Collections.ObjectPoolTests" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Fun/Framework/Runtime/Collections/Containers/ObjectPool.cs \
  UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/ObjectPoolTests.cs
git commit -m "feat(collections): add ObjectPool with resettable support"
```

---

### Task 8: Add allocation tests + usage guide + full-suite verification

**Files:**
- Create: `UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/Allocation/FastCollectionsAllocationTests.cs`
- Create: `UnityProject/Assets/Fun/Framework/README.md`

- [ ] **Step 1: Write failing allocation tests**

```csharp
using Fun.Framework.Collections;
using NUnit.Framework;

namespace Fun.Framework.Tests.Collections.Allocation
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
        public void ObjectPool_RentReturn_SteadyStateZeroAlloc()
        {
            var pool = new ObjectPool<PooledNode>(() => new PooledNode(), 256);
            pool.Prewarm(256);

            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 256; i++)
            {
                var value = pool.Rent();
                pool.Return(value);
            }
            var after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0L, after - before);
        }

        private sealed class PooledNode : IResettable
        {
            public int Value;

            public void ResetState()
            {
                Value = 0;
            }
        }
    }
}
```

- [ ] **Step 2: Run allocation tests to verify failure and baseline issues**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.Collections.Allocation.FastCollectionsAllocationTests" \
  -logFile -
```

Expected: FAIL if any hot path still allocates.

- [ ] **Step 3: Write user-facing framework README (0GC usage contract)**

`UnityProject/Assets/Fun/Framework/README.md`

```markdown
# Fun.Framework.Collections

## Containers

- `FastList<T>`
- `FastDictionary<TKey, TValue>`
- `FastHashSet<T>`
- `RingBuffer<T>`
- `FastPriorityQueue<T>`
- `ObjectPool<T>`

## 0GC Rules

1. Pre-size using constructor capacity or `EnsureCapacity`.
2. Use `NoResize` APIs in hot paths.
3. Avoid implicit allocation APIs in gameplay loops.
4. Prefer `Clear(ClearMode.Logical)` for reuse.

## Patterns

### FastList hot loop

```csharp
var list = new FastList<int>(1024);
for (var i = 0; i < 1024; i++)
{
    list.AddNoResize(i);
}
list.Clear(ClearMode.Logical);
```

### ObjectPool reuse

```csharp
var pool = new ObjectPool<MyReusable>(() => new MyReusable(), 256);
pool.Prewarm(128);
var item = pool.Rent();
pool.Return(item);
```
```

- [ ] **Step 4: Run full EditMode suite for collections**

Run:

```bash
"$UNITY_BIN" -batchmode -nographics -quit \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Fun.Framework.Tests.Collections" \
  -logFile -
```

Expected: PASS for all collections tests and allocation assertions.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Fun/Framework/Tests/EditMode/Collections/Allocation/FastCollectionsAllocationTests.cs \
  UnityProject/Assets/Fun/Framework/README.md
git commit -m "test(collections): add zero-allocation tests and usage guide"
```

---

## Risk Checklist During Execution

- Keep `NoResize` paths branch-light and allocation-free.
- Avoid using `foreach` on interface-typed collections in tests (can hide boxing).
- Keep exception messages contextual without string work on non-error path.
- Do not introduce `unsafe`, Burst, Jobs, or `Unity.Collections` dependencies.

## Done Criteria Checklist

- [ ] All 6 containers exist and compile in `Fun.Framework` runtime assembly.
- [ ] All container behavior tests pass.
- [ ] Allocation tests pass in steady-state hot path.
- [ ] README documents correct usage contracts.
- [ ] Commit history is incremental and task-scoped.
