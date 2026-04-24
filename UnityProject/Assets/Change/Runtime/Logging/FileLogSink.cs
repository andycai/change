using System;
using System.Globalization;
using System.IO;
using Change.Framework.Logging;

namespace Change.Runtime.Logging
{
    public sealed class FileLogSink : ILogSink, IDisposable
    {
        private readonly StreamWriter _writer;
        private bool _disposed;

        public FileLogSink(string filePath, bool append = true)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            FilePath = filePath;

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var mode = append ? FileMode.Append : FileMode.Create;
            _writer = new StreamWriter(new FileStream(filePath, mode, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true,
            };
        }

        public string FilePath { get; }

        public void Write(LogLevel level, string message)
        {
            ThrowIfDisposed();

            var normalizedMessage = message ?? string.Empty;
            _writer.Write(DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
            _writer.Write(" [");
            _writer.Write(level);
            _writer.Write("] ");
            _writer.WriteLine(normalizedMessage);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FileLogSink));
            }
        }
    }
}
