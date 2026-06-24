using System;
using System.Threading;
using Change.Framework.UI;
using NUnit.Framework;

namespace Change.Runtime.UI.Tests
{
    /// <summary>
    /// Tests for <see cref="CachedWindowEntry"/> covering cancellation,
    /// view-lifecycle separation, and idempotent disposal.
    ///
    /// Full integration tests for <see cref="WindowManager.ScheduleDelayedRelease"/>
    /// (the 30-second cache-expiry timer) require Task 21 (cache-hit wiring via
    /// <c>Close() -> AddToCache()</c>) to be complete first.  Until that wiring
    /// exists, the private <c>ScheduleDelayedRelease</c> method cannot be exercised
    /// through the public API.
    /// </summary>
    public class CachedWindowEntryTests
    {
        /// <summary>
        /// Dispose should cancel the ReleaseCts so that any awaiting
        /// <c>ScheduleDelayedRelease</c> task exits immediately.
        /// </summary>
        [Test]
        public void Dispose_CancelsCts()
        {
            var view = new FakeWindowView(new WindowId("TestWindow"));
            var releaseCts = new CancellationTokenSource();
            var entry = new CachedWindowEntry(view, releaseCts);

            entry.Dispose();

            Assert.IsTrue(releaseCts.IsCancellationRequested,
                "Dispose should cancel the release CTS.");
        }

        /// <summary>
        /// Dispose must NOT dispose the associated view.  The view lifecycle is
        /// managed separately by <c>WindowManager</c> — either through
        /// <c>ScheduleDelayedRelease</c> on normal expiry or
        /// <c>AddToCache</c> on LRU eviction.
        /// </summary>
        [Test]
        public void Dispose_DoesNotDisposeView()
        {
            var view = new FakeWindowView(new WindowId("TestWindow"));
            var releaseCts = new CancellationTokenSource();
            var entry = new CachedWindowEntry(view, releaseCts);

            entry.Dispose();

            Assert.AreEqual(0, view.DisposeCount,
                "CachedWindowEntry.Dispose must not dispose the view.");
        }

        /// <summary>
        /// Dispose must be safe to call multiple times — it should not throw
        /// <see cref="ObjectDisposedException"/> and should not double-dispose
        /// the ReleaseCts.
        /// </summary>
        [Test]
        public void Dispose_IsIdempotent()
        {
            var view = new FakeWindowView(new WindowId("TestWindow"));
            var releaseCts = new CancellationTokenSource();
            var entry = new CachedWindowEntry(view, releaseCts);

            entry.Dispose();

            // Second dispose must not throw.
            Assert.DoesNotThrow(() => entry.Dispose(),
                "Double-dispose must be safe (idempotent).");
        }
    }
}
