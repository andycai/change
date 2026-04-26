using System.Collections.Generic;
using NUnit.Framework;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class DownloadTaskSchedulerTests
    {
        [Test]
        public void DequeueReadyTasks_PicksHigherPriorityFirst()
        {
            var scheduler = new DownloadTaskScheduler();

            scheduler.Enqueue(DownloadTaskSnapshot.CreateQueued("low", 1000, priority: 1));
            scheduler.Enqueue(DownloadTaskSnapshot.CreateQueued("high", 1000, priority: 100));

            var ready = new List<DownloadTaskSnapshot>();
            scheduler.DequeueReadyTasks(maxCount: 1, ready);

            Assert.AreEqual(1, ready.Count);
            Assert.AreEqual("high", ready[0].PackId);
        }

        [Test]
        public void DequeueReadyTasks_WhenPaused_ReturnsNone()
        {
            var scheduler = new DownloadTaskScheduler();
            scheduler.Enqueue(DownloadTaskSnapshot.CreateQueued("pack", 1000, priority: 10));
            scheduler.SetPaused(true);

            var ready = new List<DownloadTaskSnapshot>();
            scheduler.DequeueReadyTasks(maxCount: 2, ready);

            Assert.AreEqual(0, ready.Count);
        }

        [Test]
        public void MarkTransientFailure_IncrementsRetryCountAndRequeues()
        {
            var scheduler = new DownloadTaskScheduler();
            var task = DownloadTaskSnapshot.CreateQueued("retry-pack", 500, priority: 10);
            scheduler.Enqueue(task);

            scheduler.MarkTransientFailure(task.PackId);

            Assert.IsTrue(scheduler.TryGet(task.PackId, out var retryTask));
            Assert.AreEqual(DownloadTaskState.Queued, retryTask.State);
            Assert.AreEqual(1, retryTask.RetryCount);
        }
    }
}
