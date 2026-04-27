using System;
using NUnit.Framework;

namespace Change.Runtime.Tests.EditMode.FrameBudget
{
    public sealed class FramePerfRecorderTests
    {
        [Test]
        public void CreateSnapshot_WithNoSamples_ReturnsZeroSnapshot()
        {
            var recorder = new FramePerfRecorder(windowSize: 10, overBudgetThresholdPercent: 20);

            FramePerfSnapshot snapshot = recorder.CreateSnapshot();

            Assert.That(snapshot.SampleCount, Is.EqualTo(0));
            Assert.That(snapshot.P50Ms, Is.EqualTo(0f));
            Assert.That(snapshot.P95Ms, Is.EqualTo(0f));
            Assert.That(snapshot.OverBudgetPercent, Is.EqualTo(0));
            Assert.That(snapshot.ShouldThrottle, Is.False);
        }

        [Test]
        public void CreateSnapshot_ComputesP95AndOverBudgetPercentAndThrottle()
        {
            var recorder = new FramePerfRecorder(windowSize: 10, overBudgetThresholdPercent: 20);

            recorder.RecordFrame(1f, isOverBudget: false);
            recorder.RecordFrame(2f, isOverBudget: false);
            recorder.RecordFrame(3f, isOverBudget: true);
            recorder.RecordFrame(4f, isOverBudget: true);
            recorder.RecordFrame(5f, isOverBudget: false);

            FramePerfSnapshot snapshot = recorder.CreateSnapshot();

            Assert.That(snapshot.SampleCount, Is.EqualTo(5));
            Assert.That(snapshot.P50Ms, Is.EqualTo(3f));
            Assert.That(snapshot.P95Ms, Is.EqualTo(5f));
            Assert.That(snapshot.OverBudgetPercent, Is.EqualTo(40));
            Assert.That(snapshot.ShouldThrottle, Is.True);
        }

        [Test]
        public void RecordFrame_WhenWindowRolls_DropsOldestSamples()
        {
            var recorder = new FramePerfRecorder(windowSize: 3, overBudgetThresholdPercent: 50);

            recorder.RecordFrame(1f, isOverBudget: false);
            recorder.RecordFrame(2f, isOverBudget: false);
            recorder.RecordFrame(3f, isOverBudget: true);
            recorder.RecordFrame(10f, isOverBudget: true);

            FramePerfSnapshot snapshot = recorder.CreateSnapshot();

            Assert.That(snapshot.SampleCount, Is.EqualTo(3));
            Assert.That(snapshot.P50Ms, Is.EqualTo(3f));
            Assert.That(snapshot.P95Ms, Is.EqualTo(10f));
            Assert.That(snapshot.OverBudgetPercent, Is.EqualTo(67));
            Assert.That(snapshot.ShouldThrottle, Is.True);
        }

        [Test]
        public void Constructor_WithInvalidArguments_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new FramePerfRecorder(0, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new FramePerfRecorder(10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new FramePerfRecorder(10, 101));
        }

        [Test]
        public void CreateSnapshot_WhenPercentEqualsThreshold_ShouldThrottle()
        {
            var recorder = new FramePerfRecorder(windowSize: 5, overBudgetThresholdPercent: 40);

            recorder.RecordFrame(1f, isOverBudget: false);
            recorder.RecordFrame(2f, isOverBudget: true);
            recorder.RecordFrame(3f, isOverBudget: false);
            recorder.RecordFrame(4f, isOverBudget: true);
            recorder.RecordFrame(5f, isOverBudget: false);

            FramePerfSnapshot snapshot = recorder.CreateSnapshot();

            Assert.That(snapshot.OverBudgetPercent, Is.EqualTo(40));
            Assert.That(snapshot.ShouldThrottle, Is.True);
        }

        [Test]
        public void RecordFrame_WithInvalidTotalMs_Throws()
        {
            var recorder = new FramePerfRecorder(windowSize: 4, overBudgetThresholdPercent: 50);

            Assert.Throws<ArgumentOutOfRangeException>(() => recorder.RecordFrame(float.NaN, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => recorder.RecordFrame(float.PositiveInfinity, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => recorder.RecordFrame(float.NegativeInfinity, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => recorder.RecordFrame(-0.001f, false));
        }
    }
}
