using Fun.Framework.Logging;
using UnityEngine;

namespace Fun.Runtime.Logging
{
    public sealed class UnityLogSink : ILogSink
    {
        private readonly string _prefix;

        public UnityLogSink(string prefix = "")
        {
            _prefix = prefix ?? string.Empty;
        }

        public void Write(LogLevel level, string message)
        {
            var normalizedMessage = message ?? string.Empty;
            var output = string.IsNullOrEmpty(_prefix)
                ? $"[{level}] {normalizedMessage}"
                : $"{_prefix} [{level}] {normalizedMessage}";

            switch (level)
            {
                case LogLevel.Warn:
                    Debug.LogWarning(output);
                    break;
                case LogLevel.Error:
                    Debug.LogError(output);
                    break;
                default:
                    Debug.Log(output);
                    break;
            }
        }
    }
}
