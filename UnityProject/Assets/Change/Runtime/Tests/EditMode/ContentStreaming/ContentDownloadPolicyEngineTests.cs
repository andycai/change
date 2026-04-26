using NUnit.Framework;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class ContentDownloadPolicyEngineTests
    {
        [Test]
        public void Evaluate_WhenWifiAndNotHighPressure_AllowsAutoDownload()
        {
            var engine = new ContentDownloadPolicyEngine();

            var policy = engine.Evaluate(
                NetworkType.Wifi,
                isHighPressure: false,
                dailyBudgetRemainingBytes: 1024,
                allowWifiAuto: true,
                allowCellularAuto: true,
                cellularRateLimitKbps: 256,
                maxConcurrentDownloads: 3);

            Assert.IsTrue(policy.AllowAutoDownload);
            Assert.AreEqual(ContentStreamingPauseReason.None, policy.PauseReason);
        }

        [Test]
        public void Evaluate_WhenHighPressure_DisallowsAndMarksPauseReason()
        {
            var engine = new ContentDownloadPolicyEngine();

            var policy = engine.Evaluate(
                NetworkType.Wifi,
                isHighPressure: true,
                dailyBudgetRemainingBytes: 1024,
                allowWifiAuto: true,
                allowCellularAuto: true,
                cellularRateLimitKbps: 256,
                maxConcurrentDownloads: 3);

            Assert.IsFalse(policy.AllowAutoDownload);
            Assert.AreEqual(ContentStreamingPauseReason.HighPressure, policy.PauseReason);
        }

        [Test]
        public void Evaluate_WhenCellular_UsesConfiguredRateLimit()
        {
            var engine = new ContentDownloadPolicyEngine();

            var policy = engine.Evaluate(
                NetworkType.Cellular,
                isHighPressure: false,
                dailyBudgetRemainingBytes: 4096,
                allowWifiAuto: true,
                allowCellularAuto: true,
                cellularRateLimitKbps: 192,
                maxConcurrentDownloads: 2);

            Assert.AreEqual(192, policy.CellularRateLimitKbps);
            Assert.IsTrue(policy.AllowAutoDownload);
        }
    }
}
