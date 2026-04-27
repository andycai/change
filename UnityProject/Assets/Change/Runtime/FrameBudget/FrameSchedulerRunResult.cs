namespace Change.Runtime
{
    public readonly struct FrameSchedulerRunResult
    {
        public FrameSchedulerRunResult(int executedCount, int deferredCount, int overdueCount)
        {
            ExecutedCount = executedCount;
            DeferredCount = deferredCount;
            OverdueCount = overdueCount;
        }

        public int ExecutedCount { get; }

        public int DeferredCount { get; }

        public int OverdueCount { get; }
    }
}
