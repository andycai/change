using Change.Framework.Pooling.Internal;

namespace Change.Framework.Pooling
{
    public static class Pool<T> where T : class, IPoolable, new()
    {
        private static readonly PoolEngine<T> s_engine = new PoolEngine<T>(CreateInstance, PoolDefaults.DefaultMaxSize);

        public static int InactiveCount => s_engine.InactiveCount;

        public static int MaxSize => s_engine.MaxSize;

        public static T Get()
        {
            return s_engine.Get();
        }

        public static void Release(T item)
        {
            s_engine.Release(item);
        }

        public static void Prewarm(int count)
        {
            s_engine.Prewarm(count);
        }

        public static void SetMaxSize(int maxSize)
        {
            s_engine.SetMaxSize(maxSize);
        }

        public static void Clear()
        {
            s_engine.Clear();
        }

        public static PoolStats GetStats()
        {
            return s_engine.GetStats();
        }

        private static T CreateInstance()
        {
            return new T();
        }
    }
}
