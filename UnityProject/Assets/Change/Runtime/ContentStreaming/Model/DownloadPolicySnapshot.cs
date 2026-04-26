using System;

namespace Change.Runtime.ContentStreaming
{
    public enum ContentStreamingPauseReason : byte
    {
        None = 0,
        HighPressure = 1,
        BudgetExceeded = 2,
        NetworkPolicy = 3,
        ManualPause = 4,
    }

    public readonly struct DownloadPolicySnapshot
    {
        public DownloadPolicySnapshot(
            NetworkType networkType,
            bool isHighPressure,
            bool allowAutoDownloadOnWifi,
            bool allowAutoDownloadOnCellular,
            int cellularRateLimitKbps,
            long dailyBudgetRemainingBytes,
            int maxConcurrentDownloads)
        {
            if (cellularRateLimitKbps < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellularRateLimitKbps));
            }

            if (maxConcurrentDownloads < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConcurrentDownloads));
            }

            NetworkType = networkType;
            IsHighPressure = isHighPressure;
            CellularRateLimitKbps = cellularRateLimitKbps;
            DailyBudgetRemainingBytes = dailyBudgetRemainingBytes;
            MaxConcurrentDownloads = maxConcurrentDownloads;

            if (isHighPressure)
            {
                AllowAutoDownload = false;
                PauseReason = ContentStreamingPauseReason.HighPressure;
                return;
            }

            if (dailyBudgetRemainingBytes <= 0)
            {
                AllowAutoDownload = false;
                PauseReason = ContentStreamingPauseReason.BudgetExceeded;
                return;
            }

            AllowAutoDownload = networkType switch
            {
                NetworkType.Wifi => allowAutoDownloadOnWifi,
                NetworkType.Cellular => allowAutoDownloadOnCellular,
                _ => false,
            };

            PauseReason = AllowAutoDownload
                ? ContentStreamingPauseReason.None
                : ContentStreamingPauseReason.NetworkPolicy;
        }

        public NetworkType NetworkType { get; }

        public bool IsHighPressure { get; }

        public bool AllowAutoDownload { get; }

        public int CellularRateLimitKbps { get; }

        public long DailyBudgetRemainingBytes { get; }

        public int MaxConcurrentDownloads { get; }

        public ContentStreamingPauseReason PauseReason { get; }
    }
}
