using System;
using System.Collections.Generic;
using Change.Framework.Logging;
using NUnit.Framework;

namespace Change.Framework.Tests.Logging
{
    public class LogRouterResilienceTests
    {
        private sealed class RecordingSink : ILogSink
        {
            public readonly List<string> Entries = new List<string>();

            public void Write(LogLevel level, string message)
            {
                Entries.Add(level + ":" + message);
            }
        }

        private sealed class ThrowingSink : ILogSink
        {
            public int Calls;

            public void Write(LogLevel level, string message)
            {
                Calls++;
                throw new InvalidOperationException("sink failure");
            }
        }

        [Test]
        public void Write_WhenOneSinkThrows_StillWritesToLaterSink()
        {
            var router = new LogRouter();
            var first = new ThrowingSink();
            var second = new RecordingSink();
            router.AddSink(first, LogLevel.Debug);
            router.AddSink(second, LogLevel.Debug);
            router.Freeze();

            Assert.DoesNotThrow(() => router.Error("boom"));
            Assert.AreEqual(1, first.Calls);
            Assert.AreEqual(1, second.Entries.Count);
            Assert.AreEqual("Error:boom", second.Entries[0]);
        }

        [Test]
        public void Freeze_CanBeCalledMoreThanOnce()
        {
            var router = new LogRouter();

            Assert.DoesNotThrow(() => router.Freeze());
            Assert.DoesNotThrow(() => router.Freeze());
        }

        [Test]
        public void NullMessage_IsNormalizedToEmptyString()
        {
            var router = new LogRouter();
            var sink = new RecordingSink();
            router.AddSink(sink, LogLevel.Debug);
            router.Freeze();

            router.Warn(null);

            Assert.AreEqual(1, sink.Entries.Count);
            Assert.AreEqual("Warn:", sink.Entries[0]);
        }

        [Test]
        public void MultiSink_UsesPerSinkMinLevelFiltering()
        {
            var router = new LogRouter();
            var debugSink = new RecordingSink();
            var warnSink = new RecordingSink();
            router.AddSink(debugSink, LogLevel.Debug);
            router.AddSink(warnSink, LogLevel.Warn);
            router.Freeze();

            router.Info("i");
            router.Error("e");

            CollectionAssert.AreEqual(new[] { "Info:i", "Error:e" }, debugSink.Entries);
            CollectionAssert.AreEqual(new[] { "Error:e" }, warnSink.Entries);
        }
    }
}
