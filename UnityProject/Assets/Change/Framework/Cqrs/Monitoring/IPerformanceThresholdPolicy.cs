namespace Change.Framework.Cqrs.Monitoring
{
    /// <summary>
    /// Decides whether a per-execution GC delta exceeds the acceptable threshold.
    /// Pluggable so teams can tune thresholds per context.
    /// </summary>
    public interface IPerformanceThresholdPolicy
    {
        /// <summary>The configured byte limit, exposed for alert payloads.</summary>
        long Threshold { get; }

        bool ShouldAlert(long gcBytes);
    }
}
