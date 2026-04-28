using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class ContentDownloadOrchestratorTests
    {
        [UnityTest]
        public IEnumerator SyncAndEnqueue_WhenPolicyAllows_StartsDownload() => UniTask.ToCoroutine(async () =>
        {
            var fixture = OrchestratorFixture.Create();
            var addedEvents = 0;
            var stateEvents = 0;

            fixture.Orchestrator.TaskAdded += _ => addedEvents++;
            fixture.Orchestrator.TaskStateChanged += _ => stateEvents++;

            await fixture.Orchestrator.SyncCatalogAsync(CancellationToken.None);
            Assert.IsTrue(fixture.Orchestrator.EnqueuePack("voice_pack"));

            await fixture.Orchestrator.TickAsync(CancellationToken.None);

            // Poll for completion since TickAsync now starts a background task
            var timeout = 2000;
            while (timeout > 0)
            {
                fixture.Orchestrator.TryGetPackState("voice_pack", out var snapshot);
                if (snapshot.State == DownloadTaskState.Completed) break;
                await UniTask.Delay(50);
                timeout -= 50;
            }

            Assert.IsTrue(fixture.Orchestrator.TryGetPackState("voice_pack", out var finalSnapshot));
            Assert.AreEqual(DownloadTaskState.Completed, finalSnapshot.State);
            Assert.AreEqual(1, addedEvents);
            Assert.GreaterOrEqual(stateEvents, 2);
        });

        [UnityTest]
        public IEnumerator TickAsync_WhenHighPressure_DoesNotDequeue() => UniTask.ToCoroutine(async () =>
        {
            var fixture = OrchestratorFixture.Create(isHighPressure: true);

            await fixture.Orchestrator.SyncCatalogAsync(CancellationToken.None);
            Assert.IsTrue(fixture.Orchestrator.EnqueuePack("voice_pack"));

            await fixture.Orchestrator.TickAsync(CancellationToken.None);
            await UniTask.Delay(100); // Wait a bit to ensure nothing started

            Assert.IsTrue(fixture.Orchestrator.TryGetPackState("voice_pack", out var snapshot));
            Assert.AreEqual(DownloadTaskState.Queued, snapshot.State);
        });

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
            public UniTask<ContentStreamingErrorCode> DownloadAsync(ContentPackDefinition definition, int rateLimitKbps, Action<long> onProgress, CancellationToken cancellationToken)
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
