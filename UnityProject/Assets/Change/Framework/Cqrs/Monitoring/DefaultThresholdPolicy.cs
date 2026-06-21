namespace Change.Framework.Cqrs.Monitoring
{
    /// <summary>
    /// Default policy: alert when a single execution allocates more than the configured
    /// byte budget (default 100 bytes, per FRD threshold guidance).
    /// </summary>
    public sealed class DefaultThresholdPolicy : IPerformanceThresholdPolicy
    {
        private readonly long _gcBytesThreshold;

        public DefaultThresholdPolicy(long gcBytesThreshold = 100)
        {
            _gcBytesThreshold = gcBytesThreshold;
        }

        public long Threshold => _gcBytesThreshold;

        public bool ShouldAlert(long gcBytes) => gcBytes > _gcBytesThreshold;
    }
}
