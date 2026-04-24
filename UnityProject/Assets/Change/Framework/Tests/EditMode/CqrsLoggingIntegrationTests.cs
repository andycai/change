using System;
using Change.Framework.Cqrs;
using Change.Framework.Logging;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class CqrsLoggingIntegrationTests
    {
        private readonly struct TestCommand : ICommand
        {
        }

        private sealed class TestCommandHandler : ICommandHandler<TestCommand>
        {
            public void Handle(in TestCommand command)
            {
            }
        }

        private sealed class RecordingLogger : ILogger
        {
            public int InfoCalls;

            public void Debug(string message)
            {
            }

            public void Info(string message)
            {
                InfoCalls++;
            }

            public void Warn(string message)
            {
            }

            public void Error(string message)
            {
            }
        }

        private sealed class ThrowingInfoLogger : ILogger
        {
            public void Debug(string message)
            {
            }

            public void Info(string message)
            {
                throw new InvalidOperationException("logger failed");
            }

            public void Warn(string message)
            {
            }

            public void Error(string message)
            {
            }
        }

        [Test]
        public void RegisterCommand_UsesInjectedLoggerInfo()
        {
            var logger = new RecordingLogger();
            var bus = new CqrsBus(logger);

            bus.RegisterCommand(new TestCommandHandler());

            Assert.AreEqual(1, logger.InfoCalls);
        }

        [Test]
        public void RegisterCommand_WhenLoggerThrows_DoesNotPropagate()
        {
            var bus = new CqrsBus(new ThrowingInfoLogger());

            Assert.DoesNotThrow(() => bus.RegisterCommand(new TestCommandHandler()));
        }
    }
}
