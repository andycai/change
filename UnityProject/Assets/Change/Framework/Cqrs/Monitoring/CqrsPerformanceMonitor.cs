using System;
using System.Collections.Generic;

namespace Change.Framework.Cqrs.Monitoring
{
    /// <summary>
    /// Aggregates per-message-type execution metrics on the dispatching (main) thread.
    /// </summary>
    /// <remarks>
    /// <para><b>Hot path:</b> <see cref="RecordExecution"/> mutates an existing
    /// <see cref="MetricsAccumulator"/> in place (no boxing, no string key) — no new surviving
    /// references on repeated calls for the same type. The first occurrence of a type pays a
    /// one-time dictionary Add (the accumulator instance).</para>
    /// <para><b>Thread model:</b> single-threaded (main thread), matching <c>CqrsBus</c>
    /// registration/dispatch assumptions. No locking.</para>
    /// </remarks>
    public sealed class CqrsPerformanceMonitor
    {
        private readonly Dictionary<Type, MetricsAccumulator> _accumulators = new();
        private readonly IPerformanceThresholdPolicy _threshold;

        public CqrsPerformanceMonitor()
            : this(new DefaultThresholdPolicy())
        {
        }

        public CqrsPerformanceMonitor(IPerformanceThresholdPolicy threshold)
        {
            _threshold = threshold ?? new DefaultThresholdPolicy();
        }

        /// <summary>Raised (on the dispatching thread) when a single execution exceeds the threshold.</summary>
        public event Action<PerformanceAlert> OnThresholdExceeded;

        /// <summary>
        /// Records one dispatch. Introduces no new surviving references on repeated calls
        /// for the same type (in-place field mutation).
        /// </summary>
        public void RecordExecution(Type messageType, long durationTicks, long gcBytes)
        {
            if (!_accumulators.TryGetValue(messageType, out var acc))
            {
                acc = new MetricsAccumulator { MessageType = messageType };
                _accumulators[messageType] = acc;
            }

            acc.ExecutionCount++;
            acc.TotalDurationTicks += durationTicks;
            acc.TotalGcBytes += gcBytes;
            if (gcBytes > acc.MaxGcBytes)
            {
                acc.MaxGcBytes = gcBytes;
            }

            if (_threshold.ShouldAlert(gcBytes))
            {
                OnThresholdExceeded?.Invoke(
                    new PerformanceAlert(messageType, gcBytes, _threshold.Threshold, durationTicks));
            }
        }

        public PerformanceMetrics GetMetrics(Type messageType)
        {
            if (!_accumulators.TryGetValue(messageType, out var acc))
            {
                throw new KeyNotFoundException($"No metrics for {messageType.FullName}");
            }
            return new PerformanceMetrics(
                acc.MessageType, acc.ExecutionCount, acc.TotalDurationTicks,
                acc.TotalGcBytes, acc.MaxGcBytes);
        }

        public IReadOnlyCollection<PerformanceMetrics> GetAllMetrics()
        {
            var list = new List<PerformanceMetrics>(_accumulators.Count);
            foreach (var kvp in _accumulators)
            {
                var acc = kvp.Value;
                list.Add(new PerformanceMetrics(
                    acc.MessageType, acc.ExecutionCount, acc.TotalDurationTicks,
                    acc.TotalGcBytes, acc.MaxGcBytes));
            }
            return list;
        }

        public void ClearMetrics() => _accumulators.Clear();

        private sealed class MetricsAccumulator
        {
            public Type MessageType;
            public int ExecutionCount;
            public long TotalDurationTicks;
            public long TotalGcBytes;
            public long MaxGcBytes;
        }
    }
}
