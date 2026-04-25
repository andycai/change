using System;
using System.Collections.Generic;

namespace Change.Framework.Collections
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

            if (_count == 0)
            {
                _heap[0] = default;
                return true;
            }

            var last = _heap[_count];
            _heap[_count] = default;
            SiftDown(last);
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
            CollectionMetrics.RecordFastPriorityQueueGrow();
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

        private void SiftDown(T value)
        {
            var index = 0;
            while (true)
            {
                var left = index * 2 + 1;
                if (left >= _count)
                {
                    break;
                }

                var right = left + 1;
                var best = left;
                if (right < _count && _comparer.Compare(_heap[right], _heap[left]) < 0)
                {
                    best = right;
                }

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
