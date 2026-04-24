namespace Change.Framework.Pooling
{
    public readonly struct PoolStats
    {
        public PoolStats(long created, long rented, long released, long dropped, int maxSize, int inactiveCount)
        {
            Created = created;
            Rented = rented;
            Released = released;
            Dropped = dropped;
            MaxSize = maxSize;
            InactiveCount = inactiveCount;
        }

        public long Created { get; }

        public long Rented { get; }

        public long Released { get; }

        public long Dropped { get; }

        public int MaxSize { get; }

        public int InactiveCount { get; }
    }
}
