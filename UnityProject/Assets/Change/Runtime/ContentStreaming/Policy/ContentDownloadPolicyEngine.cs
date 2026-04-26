namespace Change.Runtime.ContentStreaming
{
    public sealed class ContentDownloadPolicyEngine
    {
        public DownloadPolicySnapshot Evaluate(
            NetworkType networkType,
            bool isHighPressure,
            long dailyBudgetRemainingBytes,
            bool allowWifiAuto,
            bool allowCellularAuto,
            int cellularRateLimitKbps,
            int maxConcurrentDownloads)
        {
            return new DownloadPolicySnapshot(
                networkType,
                isHighPressure,
                allowWifiAuto,
                allowCellularAuto,
                cellularRateLimitKbps,
                dailyBudgetRemainingBytes,
                maxConcurrentDownloads);
        }
    }
}
