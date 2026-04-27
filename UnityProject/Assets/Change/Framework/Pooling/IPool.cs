namespace Change.Framework.Pooling
{
    public interface IPool<T> where T : class, IPoolable
    {
        int InactiveCount { get; }

        int MaxSize { get; }

        T Get();

        void Release(T item);

        void Prewarm(int count);

        void SetMaxSize(int maxSize);

        void Clear();

        PoolStats GetStats();
    }
}
