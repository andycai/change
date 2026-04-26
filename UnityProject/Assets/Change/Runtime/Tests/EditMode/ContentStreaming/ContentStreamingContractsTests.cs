using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class ContentStreamingContractsTests
    {
        [Test]
        public void ContentPackDefinition_WhenPackIdEmpty_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                _ = new ContentPackDefinition(
                    string.Empty,
                    version: "1.0.0",
                    sizeBytes: 1024,
                    priority: 10,
                    expireAtUtcTicks: DateTime.UtcNow.Ticks + TimeSpan.FromDays(1).Ticks,
                    requiresWifi: false));
        }

        [Test]
        public void DownloadPolicySnapshot_WhenCellularAndOverBudget_DisallowsAutoDownload()
        {
            var snapshot = new DownloadPolicySnapshot(
                NetworkType.Cellular,
                isHighPressure: false,
                allowAutoDownloadOnWifi: true,
                allowAutoDownloadOnCellular: true,
                cellularRateLimitKbps: 256,
                dailyBudgetRemainingBytes: 0,
                maxConcurrentDownloads: 2);

            Assert.IsFalse(snapshot.AllowAutoDownload);
            Assert.AreEqual(ContentStreamingPauseReason.BudgetExceeded, snapshot.PauseReason);
        }

        [Test]
        public void DownloadTaskSnapshot_WithProgress_UpdatesBytesAndRate()
        {
            var task = DownloadTaskSnapshot.CreateQueued("skin_pack_a", totalBytes: 5000, priority: 20);
            var updated = task.WithProgress(downloadedBytes: 2000, rateKbps: 120);

            Assert.AreEqual(DownloadTaskState.Downloading, updated.State);
            Assert.AreEqual(2000, updated.DownloadedBytes);
            Assert.AreEqual(120, updated.RateKbps);
        }

        [Test]
        public void DownloadTaskSnapshot_WithState_WhenFailedAndErrorNone_ThrowsArgumentException()
        {
            var task = DownloadTaskSnapshot.CreateQueued("skin_pack_a", totalBytes: 5000, priority: 20);

            Assert.Throws<ArgumentException>(() =>
                _ = task.WithState(DownloadTaskState.FailedTerminal, ContentStreamingErrorCode.None));
        }

        [Test]
        public void DownloadTaskSnapshot_WithState_NonDownloadingState_ClearsStaleRate()
        {
            var task = DownloadTaskSnapshot.CreateQueued("skin_pack_a", totalBytes: 5000, priority: 20)
                .WithProgress(downloadedBytes: 1000, rateKbps: 150);

            var paused = task.WithState(DownloadTaskState.Paused, ContentStreamingErrorCode.NetworkTimeout);

            Assert.AreEqual(DownloadTaskState.Paused, paused.State);
            Assert.AreEqual(0, paused.RateKbps);
            Assert.AreEqual(ContentStreamingErrorCode.None, paused.ErrorCode);
        }

        [Test]
        public void ContentPackDefinition_Dependencies_RoundtripAndDefaultsToEmpty()
        {
            var packWithDefault = new ContentPackDefinition(
                packId: "pack_a",
                version: "1.0.0",
                sizeBytes: 1024,
                priority: 1,
                expireAtUtcTicks: DateTime.UtcNow.Ticks + TimeSpan.FromHours(1).Ticks,
                requiresWifi: false);

            Assert.IsNotNull(packWithDefault.Dependencies);
            Assert.AreEqual(0, packWithDefault.Dependencies.Count);

            IReadOnlyList<string> dependencies = new List<string> { "shared_a", "shared_b" };
            var packWithDependencies = new ContentPackDefinition(
                packId: "pack_b",
                version: "1.0.1",
                sizeBytes: 2048,
                priority: 2,
                expireAtUtcTicks: DateTime.UtcNow.Ticks + TimeSpan.FromHours(1).Ticks,
                requiresWifi: true,
                dependencies: dependencies);

            Assert.AreEqual(2, packWithDependencies.Dependencies.Count);
            Assert.AreEqual("shared_a", packWithDependencies.Dependencies[0]);
            Assert.AreEqual("shared_b", packWithDependencies.Dependencies[1]);
        }
    }
}
