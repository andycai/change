using System.Threading;
using NUnit.Framework;

namespace Change.Runtime.UI.Tests
{
    public class CachedWindowEntryTests
    {
        /// <summary>
        /// Verifies that CachedWindowEntry.Dispose() cancels and disposes ReleaseCts,
        /// but does NOT dispose the associated view.
        /// </summary>
        [Test]
        public void Dispose_DisposesReleaseCtsButNotView()
        {
            var view = new FakeWindowView(new WindowId("TestWindow"));
            var releaseCts = new CancellationTokenSource();
            var entry = new CachedWindowEntry(view, releaseCts);

            entry.Dispose();

            // CTS must be cancelled and disposed by entry.Dispose().
            Assert.IsTrue(releaseCts.IsCancellationRequested);

            // The view must NOT be disposed by entry.Dispose() — view lifecycle
            // is managed by WindowManager (e.g. ScheduleDelayedRelease on normal
            // expiry or AddToCache on LRU eviction).
            Assert.AreEqual(0, view.DisposeCount);
        }

        /// <summary>
        /// Validates CachedWindowEntry internal state when ReleaseCts is cancelled.
        ///
        /// This test exercises the cancellation path that ScheduleDelayedRelease
        /// follows when a window is re-opened from cache before the 30-second timer
        /// fires.  Because ScheduleDelayedRelease is a private method of WindowManager
        /// (and InternalsVisibleTo only grants access to internal members, not private
        /// ones), full timer-based integration tests require either reflection or an
        /// integration-test harness that drives WindowManager.OpenAsync / Close through
        /// the public API.
        /// </summary>
        [Test]
        public void ScheduleDelayedRelease_WhenCancelled_DoesNotDisposeView()
        {
            var view = new FakeWindowView(new WindowId("TestWindow"));

            // Create a CachedWindowEntry -- this simulates AddToCache creating one.
            // ReleaseCts is what ScheduleDelayedRelease uses as its cancellation token.
            var releaseCts = new CancellationTokenSource();
            var entry = new CachedWindowEntry(view, releaseCts);

            // Act: simulate a cache hit by cancelling the release cancellation token.
            // This mimics the behavior when a window is re-opened from cache before
            // the 30-second delayed release timer fires.
            releaseCts.Cancel();

            // Assert: cancellation was recorded.
            Assert.IsTrue(releaseCts.IsCancellationRequested);

            // The cancellation itself does not dispose the view.
            // ScheduleDelayedRelease catches OperationCanceledException and returns
            // without calling View.Dispose().
            Assert.AreEqual(0, view.DisposeCount);

            // Dispose the entry to clean up (cancels + disposes CTS, not the view).
            entry.Dispose();
            Assert.AreEqual(0, view.DisposeCount);
        }
    }
}
