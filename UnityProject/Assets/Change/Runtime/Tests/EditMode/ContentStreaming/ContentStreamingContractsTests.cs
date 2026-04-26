using System;
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
    }
}
