using System;

namespace Change.Runtime.ContentStreaming
{
    public readonly struct ContentPackDefinition
    {
        public ContentPackDefinition(
            string packId,
            string version,
            long sizeBytes,
            int priority,
            long expireAtUtcTicks,
            bool requiresWifi)
        {
            if (string.IsNullOrWhiteSpace(packId))
            {
                throw new ArgumentException("packId is required.", nameof(packId));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("version is required.", nameof(version));
            }

            if (sizeBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeBytes));
            }

            if (priority < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(priority));
            }

            PackId = packId;
            Version = version;
            SizeBytes = sizeBytes;
            Priority = priority;
            ExpireAtUtcTicks = expireAtUtcTicks;
            RequiresWifi = requiresWifi;
        }

        public string PackId { get; }

        public string Version { get; }

        public long SizeBytes { get; }

        public int Priority { get; }

        public long ExpireAtUtcTicks { get; }

        public bool RequiresWifi { get; }
    }
}
