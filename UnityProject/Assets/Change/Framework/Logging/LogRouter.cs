using System;
using System.Collections.Generic;

namespace Change.Framework.Logging
{
    public sealed class LogRouter : ILogger
    {
        private readonly struct SinkRegistration
        {
            public SinkRegistration(ILogSink sink, LogLevel minLevel)
            {
                Sink = sink;
                MinLevel = minLevel;
            }

            public ILogSink Sink { get; }
            public LogLevel MinLevel { get; }
        }

        private readonly List<SinkRegistration> _registrations = new List<SinkRegistration>();
        private bool _isFrozen;

        public void AddSink(ILogSink sink, LogLevel minLevel)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            if (_isFrozen)
            {
                throw new InvalidOperationException("Log router is frozen.");
            }

            _registrations.Add(new SinkRegistration(sink, minLevel));
        }

        public void Freeze()
        {
            _isFrozen = true;
        }

        public void Debug(string message)
        {
            Write(LogLevel.Debug, message);
        }

        public void Info(string message)
        {
            Write(LogLevel.Info, message);
        }

        public void Warn(string message)
        {
            Write(LogLevel.Warn, message);
        }

        public void Error(string message)
        {
            Write(LogLevel.Error, message);
        }

        private void Write(LogLevel level, string message)
        {
            var normalizedMessage = message ?? string.Empty;

            for (var i = 0; i < _registrations.Count; i++)
            {
                var registration = _registrations[i];
                if (level < registration.MinLevel)
                {
                    continue;
                }

                try
                {
                    registration.Sink.Write(level, normalizedMessage);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
