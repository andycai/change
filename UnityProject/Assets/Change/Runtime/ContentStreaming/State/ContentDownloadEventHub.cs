using System;

namespace Change.Runtime.ContentStreaming
{
    public sealed class ContentDownloadEventHub
    {
        private readonly IClock _clock;
        private readonly long _progressIntervalTicks;
        private long _sequence;
        private long _lastProgressPublishTicks;

        public ContentDownloadEventHub()
            : this(new SystemClock(), 200)
        {
        }

        public ContentDownloadEventHub(IClock clock, int progressIntervalMs)
        {
            if (progressIntervalMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(progressIntervalMs));
            }

            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _progressIntervalTicks = TimeSpan.FromMilliseconds(progressIntervalMs).Ticks;
            _lastProgressPublishTicks = long.MinValue;
        }

        public DownloadTaskSnapshot NextState(in DownloadTaskSnapshot snapshot)
        {
            _sequence++;

            return new DownloadTaskSnapshot(
                snapshot.PackId,
                snapshot.State,
                snapshot.DownloadedBytes,
                snapshot.TotalBytes,
                snapshot.Priority,
                snapshot.RetryCount,
                snapshot.RateKbps,
                snapshot.ErrorCode,
                _sequence);
        }

        public bool TryPublishProgress(in DownloadTaskSnapshot snapshot, out DownloadTaskSnapshot sequenced)
        {
            var now = _clock.UtcNowTicks;
            if (_lastProgressPublishTicks != long.MinValue &&
                now - _lastProgressPublishTicks < _progressIntervalTicks)
            {
                sequenced = default;
                return false;
            }

            _lastProgressPublishTicks = now;
            sequenced = NextState(snapshot);
            return true;
        }

        private sealed class SystemClock : IClock
        {
            public long UtcNowTicks => DateTime.UtcNow.Ticks;
        }
    }
}
