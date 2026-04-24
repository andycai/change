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
