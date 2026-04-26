using System;
using NUnit.Framework;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class ContentDownloadEventHubTests
    {
        [Test]
        public void PublishStateChange_IncrementsSequenceMonotonically()
        {
            var hub = new ContentDownloadEventHub();
            var snapshot = DownloadTaskSnapshot.CreateQueued("pack-a", 1000, 10);

            var first = hub.NextState(snapshot.WithState(DownloadTaskState.Downloading));
            var second = hub.NextState(first.WithState(DownloadTaskState.Verifying));

            Assert.Greater(second.Sequence, first.Sequence);
        }

        [Test]
        public void TryPublishProgress_RespectsThrottleWindow()
        {
            var clock = new FakeClock(1000);
            var hub = new ContentDownloadEventHub(clock, progressIntervalMs: 200);
            var snapshot = DownloadTaskSnapshot.CreateQueued("pack-b", 1000, 10);

            Assert.IsTrue(hub.TryPublishProgress(snapshot.WithProgress(100, 10), out _));
            Assert.IsFalse(hub.TryPublishProgress(snapshot.WithProgress(150, 12), out _));

            clock.UtcNowTicks += TimeSpan.FromMilliseconds(210).Ticks;
            Assert.IsTrue(hub.TryPublishProgress(snapshot.WithProgress(250, 15), out _));
        }

        private sealed class FakeClock : IClock
        {
            public FakeClock(long ticks)
            {
                UtcNowTicks = ticks;
            }

            public long UtcNowTicks { get; set; }
        }
    }
}
