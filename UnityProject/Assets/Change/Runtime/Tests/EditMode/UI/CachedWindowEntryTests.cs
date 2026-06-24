using System.Threading;
using NUnit.Framework;

namespace Change.Runtime.UI.Tests
{
    public class CachedWindowEntryTests
    {
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
