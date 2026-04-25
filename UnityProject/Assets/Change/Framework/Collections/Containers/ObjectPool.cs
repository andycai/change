using System;

namespace Change.Framework.Collections
{
    public sealed class ObjectPool<T> : IClearable where T : class
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

        public bool Return(T value)
        {
            CollectionGuards.ThrowIfNull(value, nameof(value));

            if (value is IResettable resettable)
            {
                resettable.ResetState();
            }

            if (_stack.Count < _maxSize)
            {
                _stack.AddNoResize(value);
                return true;
            }

            return false;
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

        public void Clear(ClearMode mode = ClearMode.Logical)
        {
            _stack.Clear(mode);
        }
    }
}
