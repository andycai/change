using Change.Framework.Cqrs;
using Change.Framework.Cqrs.Monitoring;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    /// <summary>
    /// Verifies that CqrsBus records dispatch metrics into the active monitor when
    /// ENABLE_CQRS_MONITORING is defined. In the default OFF build (no symbol), this
    /// file compiles to an empty class.
    /// </summary>
#if ENABLE_CQRS_MONITORING
    public class MonitoringWiringTests
    {
        private sealed class Sink { public int Value; }

        private readonly struct TrackedCommand : ICommand
        {
            private readonly Sink _sink;
            public TrackedCommand(Sink sink) { _sink = sink; }
            public void Execute() { _sink.Value++; }
        }

        [Test]
        public void Send_WithActiveMonitor_RecordsExecution()
        {
            var monitor = new CqrsPerformanceMonitor();
            CqrsBus.SetActiveMonitor(monitor);
            try
            {
                var bus = new CqrsBus();
                var sink = new Sink();

                bus.Send(new TrackedCommand(sink));

                Assert.AreEqual(1, sink.Value);
                var metrics = monitor.GetMetrics(typeof(TrackedCommand));
                Assert.AreEqual(1, metrics.ExecutionCount);
            }
            finally
            {
                CqrsBus.SetActiveMonitor(null);
            }
        }

        private readonly struct TrackedQuery : IQuery<int>
        {
            public int Query() => 42;
        }

        [Test]
        public void Ask_WithActiveMonitor_RecordsExecution()
        {
            var monitor = new CqrsPerformanceMonitor();
            CqrsBus.SetActiveMonitor(monitor);
            try
            {
                var bus = new CqrsBus();

                var result = bus.Ask<TrackedQuery, int>(new TrackedQuery());

                Assert.AreEqual(42, result);
                var metrics = monitor.GetMetrics(typeof(TrackedQuery));
                Assert.AreEqual(1, metrics.ExecutionCount);
            }
            finally
            {
                CqrsBus.SetActiveMonitor(null);
            }
        }

        private readonly struct TrackedEvent : IEvent { }
        private sealed class TrackedEventHandler : IEventHandler<TrackedEvent>
        {
            public int Count;
            public void Handle(in TrackedEvent @event) { Count++; }
        }

        [Test]
        public void Publish_WithActiveMonitor_RecordsExecution()
        {
            var monitor = new CqrsPerformanceMonitor();
            CqrsBus.SetActiveMonitor(monitor);
            try
            {
                var bus = new CqrsBus();
                var handler = new TrackedEventHandler();

                bus.Subscribe(handler);
                bus.Publish(new TrackedEvent());

                Assert.AreEqual(1, handler.Count);
                var metrics = monitor.GetMetrics(typeof(TrackedEvent));
                Assert.AreEqual(1, metrics.ExecutionCount);
            }
            finally
            {
                CqrsBus.SetActiveMonitor(null);
            }
        }

        [Test]
        public void Publish_WhenHandlerThrows_StillRecordsExecution()
        {
            var monitor = new CqrsPerformanceMonitor();
            CqrsBus.SetActiveMonitor(monitor);
            try
            {
                var bus = new CqrsBus();
                bus.Subscribe<TrackedEvent>(static _ => throw new System.InvalidOperationException("boom"));

                Assert.Throws<System.AggregateException>(() => bus.Publish(new TrackedEvent()));

                // Recording happens before the aggregate throw, so the failed publish is still measured.
                var metrics = monitor.GetMetrics(typeof(TrackedEvent));
                Assert.AreEqual(1, metrics.ExecutionCount);
            }
            finally
            {
                CqrsBus.SetActiveMonitor(null);
            }
        }

        [Test]
        public void Dispatch_WithNoActiveMonitor_DoesNotRecordOrThrow()
        {
            CqrsBus.SetActiveMonitor(null);
            var bus = new CqrsBus();
            var sink = new Sink();

            Assert.DoesNotThrow(() => bus.Send(new TrackedCommand(sink)));
            Assert.AreEqual(1, sink.Value);
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => new CqrsPerformanceMonitor().GetMetrics(typeof(TrackedCommand)));
        }
    }
#endif
}
