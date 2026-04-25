using System;

namespace Change.Framework.Collections
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
            if (index != lastIndex)
            {
                _items[index] = _items[lastIndex];
            }
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
