using System;

namespace Change.Framework.Cqrs.Monitoring
{
    /// <summary>
    /// Aggregated metrics for a single message type. readonly struct so snapshot
    /// reads are value copies with no allocation.
    /// </summary>
    public readonly struct PerformanceMetrics
    {
        public PerformanceMetrics(
            Type messageType, int executionCount, long totalDurationTicks,
            long totalGcBytes, long maxGcBytes)
        {
            MessageType = messageType;
            ExecutionCount = executionCount;
            TotalDurationTicks = totalDurationTicks;
            TotalGcBytes = totalGcBytes;
            MaxGcBytes = maxGcBytes;
        }

        public Type MessageType { get; }
        public int ExecutionCount { get; }
        public long TotalDurationTicks { get; }
        public long TotalGcBytes { get; }
        public long MaxGcBytes { get; }
    }

    /// <summary>
    /// Raised when a single execution exceeds the configured threshold.
    /// </summary>
    public readonly struct PerformanceAlert
    {
        public PerformanceAlert(Type messageType, long gcBytes, long threshold, long ticks)
        {
            MessageType = messageType;
            GcBytes = gcBytes;
            Threshold = threshold;
            TimestampTicks = ticks;
        }

        public Type MessageType { get; }
        public long GcBytes { get; }
        public long Threshold { get; }
        public long TimestampTicks { get; }
    }
}
