using System;
using Change.Framework.Pooling.Internal;

namespace Change.Framework.Pooling
{
    public sealed class InstancePool<T> : IPool<T> where T : class, IPoolable
    {
        private readonly PoolEngine<T> _engine;

        public InstancePool(Func<T> factory)
            : this(factory, PoolDefaults.DefaultMaxSize)
        {
        }

        public InstancePool(Func<T> factory, int maxSize)
        {
            _engine = new PoolEngine<T>(factory, maxSize);
        }

        public int InactiveCount => _engine.InactiveCount;

        public int MaxSize => _engine.MaxSize;

        public T Get()
        {
            return _engine.Get();
        }

        public void Release(T item)
        {
            _engine.Release(item);
        }

        public void Prewarm(int count)
        {
            _engine.Prewarm(count);
        }

        public void SetMaxSize(int maxSize)
        {
            _engine.SetMaxSize(maxSize);
        }

        public void Clear()
        {
            _engine.Clear();
        }

        public PoolStats GetStats()
        {
            return _engine.GetStats();
        }
    }
}
