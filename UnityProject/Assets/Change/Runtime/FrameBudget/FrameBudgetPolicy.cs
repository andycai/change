using System;

namespace Change.Runtime
{
    public readonly struct FrameBudgetPolicy
    {
        public static FrameBudgetPolicy Default => new(
            updateBudgetMs: 2.5f,
            lateUpdateBudgetMs: 1.0f,
            fixedUpdateBudgetMs: 0.5f,
            maxDeferredFrames: 3,
            overBudgetWindowFrames: 30,
            overBudgetPercentThreshold: 20,
            importantMaxPerFrame: 16,
            queueCapacityPerPhase: 256);

        public FrameBudgetPolicy(
            float updateBudgetMs,
            float lateUpdateBudgetMs,
            float fixedUpdateBudgetMs,
            int maxDeferredFrames,
            int overBudgetWindowFrames,
            int overBudgetPercentThreshold,
            int importantMaxPerFrame,
            int queueCapacityPerPhase)
        {
            if (updateBudgetMs <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(updateBudgetMs));
            }

            if (lateUpdateBudgetMs <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(lateUpdateBudgetMs));
            }

            if (fixedUpdateBudgetMs <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedUpdateBudgetMs));
            }

            if (maxDeferredFrames < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDeferredFrames));
            }

            if (overBudgetWindowFrames < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(overBudgetWindowFrames));
            }

            if (overBudgetPercentThreshold < 1 || overBudgetPercentThreshold > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(overBudgetPercentThreshold));
            }

            if (importantMaxPerFrame < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(importantMaxPerFrame));
            }

            if (queueCapacityPerPhase < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(queueCapacityPerPhase));
            }

            UpdateBudgetMs = updateBudgetMs;
            LateUpdateBudgetMs = lateUpdateBudgetMs;
            FixedUpdateBudgetMs = fixedUpdateBudgetMs;
            MaxDeferredFrames = maxDeferredFrames;
            OverBudgetWindowFrames = overBudgetWindowFrames;
            OverBudgetPercentThreshold = overBudgetPercentThreshold;
            ImportantMaxPerFrame = importantMaxPerFrame;
            QueueCapacityPerPhase = queueCapacityPerPhase;
        }

        public float UpdateBudgetMs { get; }

        public float LateUpdateBudgetMs { get; }

        public float FixedUpdateBudgetMs { get; }

        public int MaxDeferredFrames { get; }

        public int OverBudgetWindowFrames { get; }

        public int OverBudgetPercentThreshold { get; }

        public int ImportantMaxPerFrame { get; }

        public int QueueCapacityPerPhase { get; }

        public float GetPhaseBudgetMs(FramePhase phase)
        {
            return phase switch
            {
                FramePhase.Update => UpdateBudgetMs,
                FramePhase.LateUpdate => LateUpdateBudgetMs,
                FramePhase.FixedUpdate => FixedUpdateBudgetMs,
                _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown frame phase."),
            };
        }
    }
}
