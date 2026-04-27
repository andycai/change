using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Change.Runtime.Tests.EditMode.FrameBudget
{
    public sealed class FrameWorkSchedulerTests
    {
        [Test]
        public void RunPhase_WhenBudgetZero_ExecutesCriticalOnly()
        {
            var scheduler = new FrameWorkScheduler(FrameBudgetPolicy.Default);
            int criticalCount = 0;
            int deferredCount = 0;

            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Critical,
                "critical",
                () => criticalCount++));
            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Deferred,
                "deferred",
                () => deferredCount++));

            int currentFrame = Time.frameCount;
            FrameSchedulerRunResult result = scheduler.RunPhase(
                FramePhase.Update,
                currentFrame,
                remainingBudgetMs: 0f);

            Assert.AreEqual(1, criticalCount);
            Assert.AreEqual(0, deferredCount);
            Assert.AreEqual(1, result.DeferredCount);
            Assert.AreEqual(0, result.OverdueCount);
        }

        [Test]
        public void RunPhase_WhenDeferredOverdue_ForcedAndCountedOverdue()
        {
            var policy = new FrameBudgetPolicy(
                updateBudgetMs: 2.5f,
                lateUpdateBudgetMs: 1f,
                fixedUpdateBudgetMs: 0.5f,
                maxDeferredFrames: 1,
                overBudgetWindowFrames: 30,
                overBudgetPercentThreshold: 20,
                importantMaxPerFrame: 16,
                queueCapacityPerPhase: 32);
            var scheduler = new FrameWorkScheduler(policy);
            int deferredCount = 0;

            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Deferred,
                "deferred",
                () => deferredCount++));

            int enqueuedFrame = Time.frameCount;

            _ = scheduler.RunPhase(FramePhase.Update, enqueuedFrame + 1, remainingBudgetMs: 0f);
            FrameSchedulerRunResult result = scheduler.RunPhase(
                FramePhase.Update,
                enqueuedFrame + 2,
                remainingBudgetMs: 0f);

            Assert.AreEqual(1, deferredCount);
            Assert.AreEqual(1, result.OverdueCount);
        }

        [Test]
        public void RunPhase_WhenImportantOverLimit_DefersRemainingImportant()
        {
            var policy = new FrameBudgetPolicy(
                updateBudgetMs: 2.5f,
                lateUpdateBudgetMs: 1f,
                fixedUpdateBudgetMs: 0.5f,
                maxDeferredFrames: 3,
                overBudgetWindowFrames: 30,
                overBudgetPercentThreshold: 20,
                importantMaxPerFrame: 1,
                queueCapacityPerPhase: 32);
            var scheduler = new FrameWorkScheduler(policy);
            int importantCount = 0;

            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Important,
                "important-1",
                () => importantCount++));
            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Important,
                "important-2",
                () => importantCount++));

            int currentFrame = Time.frameCount;
            FrameSchedulerRunResult result = scheduler.RunPhase(
                FramePhase.Update,
                currentFrame,
                remainingBudgetMs: 1f);

            Assert.AreEqual(1, importantCount);
            Assert.AreEqual(1, result.DeferredCount);
            Assert.AreEqual(1, result.ExecutedCount);
        }

        [Test]
        public void RunPhase_WhenDeferredNotOverdueAndBudgetAvailable_KeepsDeferred()
        {
            var scheduler = new FrameWorkScheduler(FrameBudgetPolicy.Default);
            int deferredCount = 0;

            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Deferred,
                "deferred",
                () => deferredCount++));

            int currentFrame = Time.frameCount;
            FrameSchedulerRunResult result = scheduler.RunPhase(
                FramePhase.Update,
                currentFrame,
                remainingBudgetMs: 1f);

            Assert.AreEqual(0, deferredCount);
            Assert.AreEqual(1, result.DeferredCount);
            Assert.AreEqual(0, result.OverdueCount);
        }

        [Test]
        public void RunPhase_WhenCallbackThrows_ContinuesProcessingRemainingItems()
        {
            var scheduler = new FrameWorkScheduler(FrameBudgetPolicy.Default);
            int firstCallCount = 0;
            int secondCallCount = 0;

            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Critical,
                "critical-throws",
                () =>
                {
                    firstCallCount++;
                    throw new InvalidOperationException("test exception");
                }));
            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Critical,
                "critical-second",
                () => secondCallCount++));

            LogAssert.Expect(
                LogType.Error,
                new Regex("Phase=Update, Priority=Critical, Tag=critical-throws, Exception=InvalidOperationException: test exception"));

            Assert.DoesNotThrow(() => _ = scheduler.RunPhase(
                FramePhase.Update,
                Time.frameCount,
                remainingBudgetMs: 1f));

            Assert.AreEqual(1, firstCallCount);
            Assert.AreEqual(1, secondCallCount);
        }

        [Test]
        public void RunPhase_WhenImportantAgeIsOneFrame_ExecutesDespitePerFrameCap()
        {
            var policy = new FrameBudgetPolicy(
                updateBudgetMs: 2.5f,
                lateUpdateBudgetMs: 1f,
                fixedUpdateBudgetMs: 0.5f,
                maxDeferredFrames: 3,
                overBudgetWindowFrames: 30,
                overBudgetPercentThreshold: 20,
                importantMaxPerFrame: 1,
                queueCapacityPerPhase: 32);
            var scheduler = new FrameWorkScheduler(policy);

            int important1Count = 0;
            int important2Count = 0;
            int important3Count = 0;

            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Important,
                "important-1",
                () => important1Count++));
            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Important,
                "important-2",
                () => important2Count++));
            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Important,
                "important-3",
                () => important3Count++));

            int baseFrame = Time.frameCount;
            _ = scheduler.RunPhase(FramePhase.Update, baseFrame, remainingBudgetMs: 1f);
            FrameSchedulerRunResult result = scheduler.RunPhase(
                FramePhase.Update,
                baseFrame + 1,
                remainingBudgetMs: 1f);

            Assert.AreEqual(1, important1Count);
            Assert.AreEqual(1, important2Count);
            Assert.AreEqual(1, important3Count);
            Assert.AreEqual(2, result.ExecutedCount);
            Assert.AreEqual(0, result.DeferredCount);
        }

        [Test]
        public void RunPhase_WhenAgedImportantExceedsCap_OnlyOneAgedItemIsForcedPerRun()
        {
            var policy = new FrameBudgetPolicy(
                updateBudgetMs: 2.5f,
                lateUpdateBudgetMs: 1f,
                fixedUpdateBudgetMs: 0.5f,
                maxDeferredFrames: 3,
                overBudgetWindowFrames: 30,
                overBudgetPercentThreshold: 20,
                importantMaxPerFrame: 1,
                queueCapacityPerPhase: 32);
            var scheduler = new FrameWorkScheduler(policy);

            int important1Count = 0;
            int important2Count = 0;
            int important3Count = 0;
            int important4Count = 0;

            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Important,
                "important-1",
                () => important1Count++));
            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Important,
                "important-2",
                () => important2Count++));
            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Important,
                "important-3",
                () => important3Count++));
            scheduler.Enqueue(FrameWorkItem.Create(
                FramePhase.Update,
                FrameTaskPriority.Important,
                "important-4",
                () => important4Count++));

            int baseFrame = Time.frameCount;
            _ = scheduler.RunPhase(FramePhase.Update, baseFrame, remainingBudgetMs: 1f);
            FrameSchedulerRunResult result = scheduler.RunPhase(
                FramePhase.Update,
                baseFrame + 1,
                remainingBudgetMs: 1f);

            Assert.AreEqual(1, important1Count);
            Assert.AreEqual(1, important2Count);
            Assert.AreEqual(1, important3Count);
            Assert.AreEqual(0, important4Count);
            Assert.AreEqual(2, result.ExecutedCount);
            Assert.AreEqual(1, result.DeferredCount);
        }
    }
}
