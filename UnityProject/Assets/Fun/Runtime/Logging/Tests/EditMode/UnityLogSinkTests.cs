using Fun.Framework.Logging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fun.Runtime.Logging.Tests
{
    public class UnityLogSinkTests
    {
        [Test]
        public void Write_WarnLevel_UsesUnityWarningChannel()
        {
            var sink = new UnityLogSink("[Game]");

            LogAssert.Expect(LogType.Warning, "[Game] [Warn] be-careful");
            sink.Write(LogLevel.Warn, "be-careful");
        }

        [Test]
        public void Write_ErrorLevel_UsesUnityErrorChannel()
        {
            var sink = new UnityLogSink();

            LogAssert.Expect(LogType.Error, "[Error] failed");
            sink.Write(LogLevel.Error, "failed");
        }
    }
}
