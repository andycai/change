using System;

namespace Change.Runtime
{
    public sealed class FramePerfRecorder
    {
        private readonly float[] _samples;
        private readonly float[] _sortedScratch;
        private readonly bool[] _overBudgetMarks;
        private readonly int _thresholdPercent;
        private int _index;
        private int _count;
        private FramePerfSnapshot _cachedSnapshot;
        private int _framesSinceLastSnapshot;
        private const int SnapshotInterval = 15;

        public FramePerfRecorder(int windowSize, int overBudgetThresholdPercent)
        {
            if (windowSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(windowSize));
            }

            if (overBudgetThresholdPercent < 1 || overBudgetThresholdPercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(overBudgetThresholdPercent));
            }

            _samples = new float[windowSize];
            _sortedScratch = new float[windowSize];
            _overBudgetMarks = new bool[windowSize];
            _thresholdPercent = overBudgetThresholdPercent;
            _framesSinceLastSnapshot = SnapshotInterval; // Force first compute
        }

        public void RecordFrame(float totalMs, bool isOverBudget)
        {
            if (float.IsNaN(totalMs) || float.IsInfinity(totalMs) || totalMs < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(totalMs));
            }

            _samples[_index] = totalMs;
            _overBudgetMarks[_index] = isOverBudget;
            _index = (_index + 1) % _samples.Length;
            _count = Math.Min(_count + 1, _samples.Length);
            _framesSinceLastSnapshot++;
        }

        public FramePerfSnapshot CreateSnapshot()
        {
            if (_count == 0)
            {
                return new FramePerfSnapshot(0f, 0f, 0, 0, false);
            }

            if (_framesSinceLastSnapshot < SnapshotInterval && _cachedSnapshot.SampleCount > 0)
            {
                return _cachedSnapshot;
            }

            int overBudgetCount = 0;
            int oldestIndex = (_index - _count + _samples.Length) % _samples.Length;

            for (int i = 0; i < _count; i++)
            {
                int index = (oldestIndex + i) % _samples.Length;
                _sortedScratch[i] = _samples[index];

                if (_overBudgetMarks[index])
                {
                    overBudgetCount++;
                }
            }

            Array.Sort(_sortedScratch, 0, _count);

            float p50 = _sortedScratch[GetNearestRankIndex(_count, 0.50)];
            float p95 = _sortedScratch[GetNearestRankIndex(_count, 0.95)];
            int overBudgetPercent = (int)Math.Round(overBudgetCount * 100.0 / _count, MidpointRounding.AwayFromZero);

            _cachedSnapshot = new FramePerfSnapshot(
                p50Ms: p50,
                p95Ms: p95,
                sampleCount: _count,
                overBudgetPercent: overBudgetPercent,
                shouldThrottle: overBudgetPercent >= _thresholdPercent);

            _framesSinceLastSnapshot = 0;
            return _cachedSnapshot;
        }

        private static int GetNearestRankIndex(int sampleCount, double percentile)
        {
            int oneBasedRank = (int)Math.Ceiling(percentile * sampleCount);
            return Math.Clamp(oneBasedRank - 1, 0, sampleCount - 1);
        }
    }
}
