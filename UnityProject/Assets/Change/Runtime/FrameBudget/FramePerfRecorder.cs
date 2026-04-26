using System;

namespace Change.Runtime
{
    public sealed class FramePerfRecorder
    {
        private readonly float[] _samples;
        private readonly bool[] _overBudgetMarks;
        private readonly int _thresholdPercent;
        private int _index;
        private int _count;

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
            _overBudgetMarks = new bool[windowSize];
            _thresholdPercent = overBudgetThresholdPercent;
        }

        public void RecordFrame(float totalMs, bool isOverBudget)
        {
            _samples[_index] = totalMs;
            _overBudgetMarks[_index] = isOverBudget;
            _index = (_index + 1) % _samples.Length;
            _count = Math.Min(_count + 1, _samples.Length);
        }

        public FramePerfSnapshot CreateSnapshot()
        {
            if (_count == 0)
            {
                return new FramePerfSnapshot(0f, 0f, 0, 0, false);
            }

            var sorted = new float[_count];
            int overBudgetCount = 0;
            int oldestIndex = (_index - _count + _samples.Length) % _samples.Length;

            for (int i = 0; i < _count; i++)
            {
                int index = (oldestIndex + i) % _samples.Length;
                sorted[i] = _samples[index];

                if (_overBudgetMarks[index])
                {
                    overBudgetCount++;
                }
            }

            Array.Sort(sorted);

            float p50 = sorted[GetNearestRankIndex(_count, 0.50)];
            float p95 = sorted[GetNearestRankIndex(_count, 0.95)];
            int overBudgetPercent = (int)Math.Round(overBudgetCount * 100.0 / _count, MidpointRounding.AwayFromZero);

            return new FramePerfSnapshot(
                p50Ms: p50,
                p95Ms: p95,
                sampleCount: _count,
                overBudgetPercent: overBudgetPercent,
                shouldThrottle: overBudgetPercent >= _thresholdPercent);
        }

        private static int GetNearestRankIndex(int sampleCount, double percentile)
        {
            int oneBasedRank = (int)Math.Ceiling(percentile * sampleCount);
            return Math.Clamp(oneBasedRank - 1, 0, sampleCount - 1);
        }
    }
}
