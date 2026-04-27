using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Change.Framework.Pooling.Internal
{
    internal sealed class PoolEngine<T> where T : class, IPoolable
    {
        // Not thread-safe; pool operations are expected on a single thread.
        private readonly Stack<T> _inactive;
        private readonly Func<T> _factory;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly object _rentedMarker = new object();
        private ConditionalWeakTable<T, object> _rentedItems = new ConditionalWeakTable<T, object>();
#endif
        private int _maxSize;

        private long _created;
        private long _rented;
        private long _released;
        private long _dropped;

        public PoolEngine(Func<T> factory, int maxSize)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize));
            }

            _factory = factory;
            _maxSize = maxSize;
            _inactive = new Stack<T>(maxSize);
        }

        public int InactiveCount => _inactive.Count;

        public int MaxSize => _maxSize;

        public T Get()
        {
            T item;
            if (_inactive.Count > 0)
            {
                item = _inactive.Pop();
            }
            else
            {
                item = CreateValidatedInstance();
                _created++;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            MarkRented(item);
#endif
            _rented++;
            return item;
        }

        public void Release(T item)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (!TryMarkReleased(item))
            {
                throw new InvalidOperationException($"Cannot release instance of {typeof(T).FullName} that is not currently rented by this pool.");
            }
#else
            if (item == null)
            {
                return;
            }
#endif

            item.Reset();
            _released++;

            if (_inactive.Count >= _maxSize)
            {
                _dropped++;
                return;
            }

            _inactive.Push(item);
        }

        public void Prewarm(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var target = Math.Min(count, _maxSize);
            while (_inactive.Count < target)
            {
                var created = CreateValidatedInstance();
                _inactive.Push(created);
                _created++;
            }
        }

        public void SetMaxSize(int maxSize)
        {
            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize));
            }

            _maxSize = maxSize;
            while (_inactive.Count > _maxSize)
            {
                _inactive.Pop();
            }
        }

        public void Clear()
        {
            _inactive.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _rentedItems = new ConditionalWeakTable<T, object>();
#endif
        }

        public PoolStats GetStats()
        {
            return new PoolStats(_created, _rented, _released, _dropped, _maxSize, _inactive.Count);
        }

        private T CreateValidatedInstance()
        {
            var item = _factory();
            if (item == null)
            {
                throw new InvalidOperationException($"Pool factory returned null for {typeof(T).FullName}.");
            }

            return item;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void MarkRented(T item)
        {
            if (_rentedItems.TryGetValue(item, out _))
            {
                throw new InvalidOperationException($"Cannot rent instance of {typeof(T).FullName} because it is already marked as rented.");
            }

            _rentedItems.Add(item, _rentedMarker);
        }

        private bool TryMarkReleased(T item)
        {
            return _rentedItems.Remove(item);
        }
#endif
    }
}
