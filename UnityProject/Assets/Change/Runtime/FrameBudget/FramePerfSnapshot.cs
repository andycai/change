namespace Change.Runtime
{
    public readonly struct FramePerfSnapshot
    {
        public FramePerfSnapshot(float p50Ms, float p95Ms, int sampleCount, int overBudgetPercent, bool shouldThrottle)
        {
            P50Ms = p50Ms;
            P95Ms = p95Ms;
            SampleCount = sampleCount;
            OverBudgetPercent = overBudgetPercent;
            ShouldThrottle = shouldThrottle;
        }

        public float P50Ms { get; }

        public float P95Ms { get; }

        public int SampleCount { get; }

        public int OverBudgetPercent { get; }

        public bool ShouldThrottle { get; }
    }
}
