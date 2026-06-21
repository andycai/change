using System;
using System.Collections.Generic;
using Change.Framework.Cqrs.Monitoring;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class MonitoringTests : ZeroGcTestBase
    {
        [Test]
        public void RecordExecution_AggregatesCountDurationAndGc()
        {
            var monitor = new CqrsPerformanceMonitor();
            monitor.RecordExecution(typeof(string), durationTicks: 10, gcBytes: 5);
            monitor.RecordExecution(typeof(string), durationTicks: 20, gcBytes: 15);

            var metrics = monitor.GetMetrics(typeof(string));

            Assert.AreEqual(2, metrics.ExecutionCount);
            Assert.AreEqual(30, metrics.TotalDurationTicks);
            Assert.AreEqual(20, metrics.TotalGcBytes);
            Assert.AreEqual(15, metrics.MaxGcBytes);
        }

        [Test]
        public void RecordExecution_AboveThreshold_RaisesAlert()
        {
            var monitor = new CqrsPerformanceMonitor(new DefaultThresholdPolicy(gcBytesThreshold: 100));
            PerformanceAlert? raised = null;
            monitor.OnThresholdExceeded += a => raised = a;

            monitor.RecordExecution(typeof(string), durationTicks: 1, gcBytes: 150);

            Assert.IsTrue(raised.HasValue);
            Assert.AreEqual(typeof(string), raised.Value.MessageType);
            Assert.AreEqual(150, raised.Value.GcBytes);
            Assert.AreEqual(100, raised.Value.Threshold);
        }

        [Test]
        public void RecordExecution_BelowThreshold_DoesNotRaise()
        {
            var monitor = new CqrsPerformanceMonitor(new DefaultThresholdPolicy(gcBytesThreshold: 100));
            var raised = false;
            monitor.OnThresholdExceeded += _ => raised = true;

            monitor.RecordExecution(typeof(string), durationTicks: 1, gcBytes: 50);

            Assert.IsFalse(raised);
        }

        [Test]
        public void RecordExecution_RepeatCallsForSameType_AllocatesZeroBytes()
        {
            var monitor = new CqrsPerformanceMonitor();
            monitor.RecordExecution(typeof(string), 1, 0); // ensure accumulator exists (first Add)

            AssertZeroGc(() => monitor.RecordExecution(typeof(string), 1, 0));
        }

        [Test]
        public void ClearMetrics_RemovesAllEntries()
        {
            var monitor = new CqrsPerformanceMonitor();
            monitor.RecordExecution(typeof(string), 1, 1);

            monitor.ClearMetrics();

            Assert.Throws<KeyNotFoundException>(() => monitor.GetMetrics(typeof(string)));
        }
    }
}
