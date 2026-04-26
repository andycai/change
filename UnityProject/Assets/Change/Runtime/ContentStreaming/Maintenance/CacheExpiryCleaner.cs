using System.Collections.Generic;

namespace Change.Runtime.ContentStreaming
{
    public sealed class CacheExpiryCleaner
    {
        public void CollectExpired(IReadOnlyList<ContentCacheRecord> records, long nowUtcTicks, List<ContentCacheRecord> expired)
        {
            expired.Clear();

            for (var i = 0; i < records.Count; i++)
            {
                if (records[i].ExpireAtUtcTicks <= nowUtcTicks)
                {
                    expired.Add(records[i]);
                }
            }
        }
    }
}
