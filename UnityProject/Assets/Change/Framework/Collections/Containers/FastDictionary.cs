using System;
using System.Collections.Generic;

namespace Change.Framework.Collections
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
            return TryInsert(key, value, allowResize: true);
        }

        public bool TryAddNoResize(TKey key, TValue value)
        {
            return TryInsert(key, value, allowResize: false);
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

        public bool ContainsKey(TKey key)
        {
            return FindEntryIndex(key) >= 0;
        }

        public bool Remove(TKey key)
        {
            if (_buckets.Length == 0)
            {
                return false;
            }

            var hashCode = _comparer.GetHashCode(key) & int.MaxValue;
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

        private bool TryInsert(TKey key, TValue value, bool allowResize)
        {
            if (_buckets.Length == 0)
            {
                if (!allowResize)
                {
                    return false;
                }

                Resize(GrowPolicy.Next(0, 1));
            }

            var hashCode = _comparer.GetHashCode(key) & int.MaxValue;
            var bucket = hashCode % _buckets.Length;

            for (var index = _buckets[bucket] - 1; index >= 0; index = _entries[index].Next)
            {
                if (_entries[index].HashCode == hashCode && _comparer.Equals(_entries[index].Key, key))
                {
                    return false;
                }
            }

            int entryIndex;
            if (_freeCount > 0)
            {
                entryIndex = _freeList;
                _freeList = _entries[entryIndex].Next;
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

                entryIndex = _count;
                _count++;
            }

            _entries[entryIndex].HashCode = hashCode;
            _entries[entryIndex].Next = _buckets[bucket] - 1;
            _entries[entryIndex].Key = key;
            _entries[entryIndex].Value = value;
            _buckets[bucket] = entryIndex + 1;
            _version++;
            return true;
        }

        private int FindEntryIndex(TKey key)
        {
            if (_buckets.Length == 0)
            {
                return -1;
            }

            var hashCode = _comparer.GetHashCode(key) & int.MaxValue;
            for (var index = _buckets[hashCode % _buckets.Length] - 1; index >= 0; index = _entries[index].Next)
            {
                if (_entries[index].HashCode == hashCode && _comparer.Equals(_entries[index].Key, key))
                {
                    return index;
                }
            }

            return -1;
        }

        private void Resize(int newSize)
        {
            var buckets = new int[newSize];
            var entries = new Entry[newSize];

            if (_count > 0)
            {
                Array.Copy(_entries, 0, entries, 0, _count);

                for (var index = 0; index < _count; index++)
                {
                    if (entries[index].HashCode < 0)
                    {
                        continue;
                    }

                    var bucket = entries[index].HashCode % newSize;
                    entries[index].Next = buckets[bucket] - 1;
                    buckets[bucket] = index + 1;
                }
            }

            _buckets = buckets;
            _entries = entries;
            CollectionMetrics.RecordFastDictionaryGrow();
        }

        public void ForEach(Action<TKey, TValue> action)
        {
            for (var i = 0; i < _count; i++)
            {
                if (_entries[i].HashCode >= 0)
                {
                    action(_entries[i].Key, _entries[i].Value);
                }
            }
        }
    }
}
