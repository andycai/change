using System;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    /// <summary>
    /// Shared zero-GC measurement harness for CQRS dispatch tests.
    /// </summary>
    /// <remarks>
    /// <para><b>Measurement API:</b> uses <see cref="GC.GetTotalMemory"/> snapshots before/after a
    /// measured loop. <c>GC.GetAllocatedBytesForCurrentThread</c> was tried first but is
    /// non-functional in this Unity 2022.3 Mono EditMode build (returns a constant regardless of
    /// allocation, verified against a 1 MB allocation). <see cref="GC.GetTotalMemory"/> measures the
    /// live heap, so it detects allocations whose references survive the measured window. To prove a
    /// dispatch path is zero-allocation, the loop must introduce no new rooted references.</para>
    /// <para><b>Negative tests</b> must accumulate allocations that stay rooted (e.g. append to a
    /// list); a single overwritten reference would be reclaimed and read as zero growth.</para>
    /// </remarks>
    public abstract class ZeroGcTestBase
    {
        protected const int WarmupIterations = 1000;
        protected const int MeasuredIterations = 100000;

        /// <summary>
        /// Asserts that invoking <paramref name="action"/> does not grow the live heap across a
        /// measured loop. Warm-up eliminates JIT allocations; the snapshots force full collections.
        /// </summary>
        protected void AssertZeroGc(Action action, string message = null)
        {
            for (var i = 0; i < WarmupIterations; i++)
            {
                action();
            }

            ForceFullGc();
            var before = GC.GetTotalMemory(true);
            for (var i = 0; i < MeasuredIterations; i++)
            {
                action();
            }
            var after = GC.GetTotalMemory(true);

            Assert.AreEqual(before, after,
                message ?? "Live heap grew across the measured loop; expected zero-allocation dispatch.");
        }

        /// <summary>
        /// Returns the live-heap delta for a single invocation (no assertion), for debugging.
        /// </summary>
        protected long MeasureGcBytes(Action action)
        {
            for (var i = 0; i < WarmupIterations; i++)
            {
                action();
            }

            ForceFullGc();
            var before = GC.GetTotalMemory(true);
            action();
            var after = GC.GetTotalMemory(true);
            return after - before;
        }

        protected static void ForceFullGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
