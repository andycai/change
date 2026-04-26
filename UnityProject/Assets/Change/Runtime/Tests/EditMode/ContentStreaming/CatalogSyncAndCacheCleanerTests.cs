using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class CatalogSyncAndCacheCleanerTests
    {
        [Test]
        public void SyncCatalogAsync_UpdatesCatalogVersionAndDefinitions()
        {
            var client = new FakeCatalogClient(
                new[]
                {
                    new ContentPackDefinition("voice_pack", "1.0.0", 1024, 10, DateTime.UtcNow.AddDays(1).Ticks, false),
                },
                "v10");
            var sync = new CatalogSyncService(client);

            sync.SyncAsync().GetAwaiter().GetResult();

            Assert.AreEqual("v10", sync.CatalogVersion);
            Assert.AreEqual(1, sync.Definitions.Count);
        }

        [Test]
        public void CollectExpired_ReturnsOnlyExpiredRecords()
        {
            var now = DateTime.UtcNow;
            var records = new List<ContentCacheRecord>
            {
                new("a", "1.0", now.AddMinutes(-1).Ticks, now.Ticks),
                new("b", "1.0", now.AddDays(1).Ticks, now.Ticks),
            };

            var cleaner = new CacheExpiryCleaner();
            var expired = new List<ContentCacheRecord>();
            cleaner.CollectExpired(records, now.Ticks, expired);

            Assert.AreEqual(1, expired.Count);
            Assert.AreEqual("a", expired[0].PackId);
        }

        private sealed class FakeCatalogClient : ICatalogClient
        {
            private readonly IReadOnlyList<ContentPackDefinition> _definitions;
            private readonly string _version;

            public FakeCatalogClient(IReadOnlyList<ContentPackDefinition> definitions, string version)
            {
                _definitions = definitions;
                _version = version;
            }

            public UniTask<CatalogSyncResult> FetchAsync(string currentVersion)
            {
                return UniTask.FromResult(new CatalogSyncResult(_version, _definitions));
            }
        }
    }
}
