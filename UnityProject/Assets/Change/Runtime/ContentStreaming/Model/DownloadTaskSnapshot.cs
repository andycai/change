using System;

namespace Change.Runtime.ContentStreaming
{
    public readonly struct DownloadTaskSnapshot
    {
        public DownloadTaskSnapshot(
            string packId,
            DownloadTaskState state,
            long downloadedBytes,
            long totalBytes,
            int priority,
            int retryCount,
            int rateKbps,
            ContentStreamingErrorCode errorCode,
            long sequence)
        {
            if (string.IsNullOrWhiteSpace(packId))
            {
                throw new ArgumentException("packId is required.", nameof(packId));
            }

            if (downloadedBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(downloadedBytes));
            }

            if (totalBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalBytes));
            }

            if (downloadedBytes > totalBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(downloadedBytes));
            }

            if (priority < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(priority));
            }

            if (retryCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(retryCount));
            }

            if (rateKbps < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rateKbps));
            }

            PackId = packId;
            State = state;
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
            Priority = priority;
            RetryCount = retryCount;
            RateKbps = rateKbps;
            ErrorCode = errorCode;
            Sequence = sequence;
        }

        public string PackId { get; }

        public DownloadTaskState State { get; }

        public long DownloadedBytes { get; }

        public long TotalBytes { get; }

        public int Priority { get; }

        public int RetryCount { get; }

        public int RateKbps { get; }

        public ContentStreamingErrorCode ErrorCode { get; }

        public long Sequence { get; }

        public static DownloadTaskSnapshot CreateQueued(string packId, long totalBytes, int priority)
        {
            if (string.IsNullOrWhiteSpace(packId))
            {
                throw new ArgumentException("packId is required.", nameof(packId));
            }

            if (totalBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalBytes));
            }

            if (priority < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(priority));
            }

            return new DownloadTaskSnapshot(
                packId,
                DownloadTaskState.Queued,
                downloadedBytes: 0,
                totalBytes,
                priority,
                retryCount: 0,
                rateKbps: 0,
                ContentStreamingErrorCode.None,
                sequence: 0);
        }

        public DownloadTaskSnapshot WithProgress(long downloadedBytes, int rateKbps)
        {
            if (downloadedBytes < 0 || downloadedBytes > TotalBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(downloadedBytes));
            }

            if (rateKbps < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rateKbps));
            }

            return new DownloadTaskSnapshot(
                PackId,
                DownloadTaskState.Downloading,
                downloadedBytes,
                TotalBytes,
                Priority,
                RetryCount,
                rateKbps,
                ContentStreamingErrorCode.None,
                Sequence + 1);
        }

        public DownloadTaskSnapshot WithState(
            DownloadTaskState state,
            ContentStreamingErrorCode errorCode = ContentStreamingErrorCode.None)
        {
            return new DownloadTaskSnapshot(
                PackId,
                state,
                DownloadedBytes,
                TotalBytes,
                Priority,
                RetryCount,
                RateKbps,
                errorCode,
                Sequence + 1);
        }
    }
}
