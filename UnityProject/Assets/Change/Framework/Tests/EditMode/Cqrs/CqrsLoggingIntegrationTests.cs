using System;
using Change.Framework.Cqrs;
using Change.Framework.Logging;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class CqrsLoggingIntegrationTests
    {
        private readonly struct TestEvent : IEvent { }

        private sealed class TestEventHandler : IEventHandler<TestEvent>
        {
            public void Handle(in TestEvent _) { }
        }

        private sealed class RecordingLogger : ILogger
        {
            public int InfoCalls;
            public void Debug(string message) { }
            public void Info(string message) { InfoCalls++; }
            public void Warn(string message) { }
            public void Error(string message) { }
        }

        private sealed class ThrowingInfoLogger : ILogger
        {
            public void Debug(string message) { }
            public void Info(string message) { throw new InvalidOperationException("logger failed"); }
            public void Warn(string message) { }
            public void Error(string message) { }
        }

        [Test]
        public void Subscribe_UsesInjectedLoggerInfo()
        {
            var logger = new RecordingLogger();
            var bus = new CqrsBus(logger);

            bus.Subscribe(new TestEventHandler());

            Assert.AreEqual(1, logger.InfoCalls);
        }

        [Test]
        public void Subscribe_WhenLoggerThrows_DoesNotPropagate()
        {
            var bus = new CqrsBus(new ThrowingInfoLogger());

            Assert.DoesNotThrow(() => bus.Subscribe(new TestEventHandler()));
        }
    }
}
