using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class ContentDownloadOrchestratorTests
    {
        [Test]
        public void SyncAndEnqueue_WhenPolicyAllows_StartsDownload()
        {
            var fixture = OrchestratorFixture.Create();
            var addedEvents = 0;
            var stateEvents = 0;

            fixture.Orchestrator.TaskAdded += _ => addedEvents++;
            fixture.Orchestrator.TaskStateChanged += _ => stateEvents++;

            fixture.Orchestrator.SyncCatalogAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(fixture.Orchestrator.EnqueuePack("voice_pack"));

            fixture.Orchestrator.TickAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(fixture.Orchestrator.TryGetPackState("voice_pack", out var snapshot));
            Assert.AreEqual(DownloadTaskState.Completed, snapshot.State);
            Assert.AreEqual(1, addedEvents);
            Assert.GreaterOrEqual(stateEvents, 2);
        }

        [Test]
        public void TickAsync_WhenHighPressure_DoesNotDequeue()
        {
            var fixture = OrchestratorFixture.Create(isHighPressure: true);

            fixture.Orchestrator.SyncCatalogAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(fixture.Orchestrator.EnqueuePack("voice_pack"));

            fixture.Orchestrator.TickAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(fixture.Orchestrator.TryGetPackState("voice_pack", out var snapshot));
            Assert.AreEqual(DownloadTaskState.Queued, snapshot.State);
        }

        private sealed class OrchestratorFixture
        {
            public static OrchestratorFixture Create(bool isHighPressure = false)
            {
                var definitions = new List<ContentPackDefinition>
                {
                    new("voice_pack", "1.0.0", 1000, 100, DateTime.UtcNow.AddDays(2).Ticks, false),
                };

                var orchestrator = new ContentDownloadOrchestrator(
                    new CatalogSyncService(new FakeCatalogClient(definitions)),
                    new ContentDownloadPolicyEngine(),
                    new DownloadTaskScheduler(),
                    new ContentDownloadStateStore(),
                    new ContentDownloadEventHub(),
                    new FakeAdapter(),
                    new FixedNetwork(NetworkType.Wifi),
                    new FixedPressure(isHighPressure),
                    new FixedBudget(1024 * 1024));

                return new OrchestratorFixture(orchestrator);
            }

            private OrchestratorFixture(ContentDownloadOrchestrator orchestrator)
            {
                Orchestrator = orchestrator;
            }

            public ContentDownloadOrchestrator Orchestrator { get; }
        }

        private sealed class FakeCatalogClient : ICatalogClient
        {
            private readonly IReadOnlyList<ContentPackDefinition> _definitions;

            public FakeCatalogClient(IReadOnlyList<ContentPackDefinition> definitions)
            {
                _definitions = definitions;
            }

            public UniTask<CatalogSyncResult> FetchAsync(string currentVersion)
            {
                return UniTask.FromResult(new CatalogSyncResult("v1", _definitions));
            }
        }

        private sealed class FakeAdapter : IAssetDownloadAdapter
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
