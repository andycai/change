namespace Change.Runtime.ContentStreaming
{
    public readonly struct ContentCacheRecord
    {
        public ContentCacheRecord(
            string packId,
            string version,
            long expireAtUtcTicks,
            long lastAccessUtcTicks)
        {
            PackId = packId;
            Version = version;
            ExpireAtUtcTicks = expireAtUtcTicks;
            LastAccessUtcTicks = lastAccessUtcTicks;
        }

        public string PackId { get; }

        public string Version { get; }

        public long ExpireAtUtcTicks { get; }

        public long LastAccessUtcTicks { get; }
    }
}
