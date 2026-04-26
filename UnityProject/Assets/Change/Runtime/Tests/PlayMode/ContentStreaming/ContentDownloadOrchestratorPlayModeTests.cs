using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class ContentDownloadOrchestratorPlayModeTests
    {
        [UnityTest]
        public IEnumerator TickAsync_WithEmptyCatalog_CompletesWithoutException()
        {
            var orchestrator = new ContentDownloadOrchestrator(
                new CatalogSyncService(new EmptyCatalogClient()),
                new ContentDownloadPolicyEngine(),
                new DownloadTaskScheduler(),
                new ContentDownloadStateStore(),
                new ContentDownloadEventHub(),
                new SuccessAdapter(),
                new FixedNetwork(NetworkType.Wifi),
                new FixedPressure(false),
                new FixedBudget(1024 * 1024));

            yield return orchestrator.SyncCatalogAsync(CancellationToken.None).ToCoroutine();
            yield return orchestrator.TickAsync(CancellationToken.None).ToCoroutine();
        }

        private sealed class EmptyCatalogClient : ICatalogClient
        {
            public UniTask<CatalogSyncResult> FetchAsync(string currentVersion)
            {
                return UniTask.FromResult(new CatalogSyncResult("v0", new List<ContentPackDefinition>()));
            }
        }

        private sealed class SuccessAdapter : IAssetDownloadAdapter
        {
            public UniTask<ContentStreamingErrorCode> DownloadAsync(ContentPackDefinition definition, int rateLimitKbps, CancellationToken cancellationToken)
            {
                return UniTask.FromResult(ContentStreamingErrorCode.None);
            }
        }

        private sealed class FixedNetwork : INetworkStateProvider
        {
            public FixedNetwork(NetworkType current)
            {
                Current = current;
            }

            public NetworkType Current { get; }
        }

        private sealed class FixedPressure : IPlayPressureSignal
        {
            public FixedPressure(bool isHighPressure)
            {
                IsHighPressure = isHighPressure;
            }

            public bool IsHighPressure { get; }
        }

        private sealed class FixedBudget : IDataBudgetProvider
        {
            public FixedBudget(long dailyRemainingBytes)
            {
                DailyRemainingBytes = dailyRemainingBytes;
            }

            public long DailyRemainingBytes { get; private set; }

            public void Consume(long bytes)
            {
                DailyRemainingBytes -= bytes;
            }
        }
    }
}
