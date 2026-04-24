using System;
using System.Collections.Generic;
using Fun.Framework.Logging;
using NUnit.Framework;

namespace Fun.Framework.Tests.Logging
{
    public class LogRouterCoreTests
    {
        private sealed class RecordingSink : ILogSink
        {
            public readonly List<(LogLevel Level, string Message)> Entries = new List<(LogLevel, string)>();

            public void Write(LogLevel level, string message)
            {
                Entries.Add((level, message));
            }
        }

        [Test]
        public void AddSink_NullSink_ThrowsArgumentNullException()
        {
            var router = new LogRouter();

            Assert.Throws<ArgumentNullException>(() => router.AddSink(null, LogLevel.Debug));
        }

        [Test]
        public void Info_WhenLevelIsEnabled_ForwardsToSink()
        {
            var router = new LogRouter();
            var sink = new RecordingSink();
            router.AddSink(sink, LogLevel.Info);
            router.Freeze();

            router.Info("ready");

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreEqual(LogLevel.Info, sink.Entries[0].Level);
            Assert.AreEqual("ready", sink.Entries[0].Message);
        }

        [Test]
        public void Debug_WhenMinLevelIsInfo_DoesNotForward()
        {
            var router = new LogRouter();
            var sink = new RecordingSink();
            router.AddSink(sink, LogLevel.Info);
            router.Freeze();

            router.Debug("hidden");

            Assert.AreEqual(0, sink.Entries.Count);
        }

        [Test]
        public void AddSink_AfterFreeze_ThrowsInvalidOperationException()
        {
            var router = new LogRouter();
            router.Freeze();

            Assert.Throws<InvalidOperationException>(() => router.AddSink(new RecordingSink(), LogLevel.Debug));
        }

        [Test]
        public void NullLogger_AllMethods_DoNotThrow()
        {
            var logger = NullLogger.Instance;

            Assert.DoesNotThrow(() => logger.Debug("d"));
            Assert.DoesNotThrow(() => logger.Info("i"));
            Assert.DoesNotThrow(() => logger.Warn("w"));
            Assert.DoesNotThrow(() => logger.Error("e"));
        }
    }
}
