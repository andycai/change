using Change.Framework.Cqrs;
using Change.Framework.Cqrs.Monitoring;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    /// <summary>
    /// Verifies that the monitoring wiring in CqrsBus.Send/Query/Publish records
    /// metrics correctly when ENABLE_CQRS_MONITORING is defined.
    /// In the default OFF build (no symbol), this file compiles to an empty class.
    /// </summary>
#if ENABLE_CQRS_MONITORING
    public class MonitoringWiringTests
    {
        private readonly struct TrackedCommand : ICommand { }
        private sealed class TrackedHandler : ICommandHandler<TrackedCommand>
        {
            public void Handle(in TrackedCommand command) { }
        }

        [Test]
        public void Send_WithMonitoring_RecordsExecution()
        {
            var monitor = new CqrsPerformanceMonitor();
            CqrsBus.SetActiveMonitor(monitor);

            try
            {
                var bus = new CqrsBus();
                bus.RegisterCommand(new TrackedHandler());
                bus.Send(new TrackedCommand());

                var metrics = monitor.GetMetrics(typeof(TrackedCommand));
                Assert.AreEqual(1, metrics.ExecutionCount);
            }
            finally
            {
                CqrsBus.SetActiveMonitor(null);
            }
        }

        private readonly struct TrackedQuery : IQuery<int> { }
        private sealed class TrackedQueryHandler : IQueryHandler<TrackedQuery, int>
        {
            public int Handle(in TrackedQuery query) => 42;
        }

        [Test]
        public void Query_WithMonitoring_RecordsExecution()
        {
            var monitor = new CqrsPerformanceMonitor();
            CqrsBus.SetActiveMonitor(monitor);

            try
            {
                var bus = new CqrsBus();
                bus.RegisterQuery(new TrackedQueryHandler());
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
        public void Publish_WithMonitoring_RecordsExecution()
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
    }
#endif
}
