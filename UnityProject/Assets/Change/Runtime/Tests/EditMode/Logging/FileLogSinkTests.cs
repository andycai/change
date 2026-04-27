using System;
using System.IO;
using Change.Framework.Logging;
using NUnit.Framework;

namespace Change.Runtime.Logging.Tests
{
    public class FileLogSinkTests
    {
        [Test]
        public void Write_CreatesDirectoryAndWritesFormattedMessage()
        {
            var root = Path.Combine(Path.GetTempPath(), "fun-runtime-logging-tests", Guid.NewGuid().ToString("N"));
            var filePath = Path.Combine(root, "logs", "runtime.log");

            try
            {
                using (var sink = new FileLogSink(filePath, append: false))
                {
                    sink.Write(LogLevel.Info, "hello");
                }

                Assert.That(File.Exists(filePath), Is.True);

                var content = File.ReadAllText(filePath);
                Assert.That(content, Does.Contain("[Info] hello"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Write_AfterDispose_ThrowsObjectDisposedException()
        {
            var root = Path.Combine(Path.GetTempPath(), "fun-runtime-logging-tests", Guid.NewGuid().ToString("N"));
            var filePath = Path.Combine(root, "runtime.log");

            try
            {
                var sink = new FileLogSink(filePath, append: false);
                sink.Dispose();

                Assert.Throws<ObjectDisposedException>(() => sink.Write(LogLevel.Error, "boom"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }
    }
}
