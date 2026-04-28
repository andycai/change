using System;
using Change.Framework.Collections;
using UnityEngine;

namespace Change.Runtime
{
    public sealed class FrameWorkScheduler
    {
        private readonly FrameBudgetPolicy _policy;
        private readonly RingBuffer<FrameWorkItem> _updateCritical;
        private readonly RingBuffer<FrameWorkItem> _updateImportant;
        private readonly RingBuffer<FrameWorkItem> _updateDeferred;
        private readonly RingBuffer<FrameWorkItem> _lateCritical;
        private readonly RingBuffer<FrameWorkItem> _lateImportant;
        private readonly RingBuffer<FrameWorkItem> _lateDeferred;
        private readonly RingBuffer<FrameWorkItem> _fixedCritical;
        private readonly RingBuffer<FrameWorkItem> _fixedImportant;
        private readonly RingBuffer<FrameWorkItem> _fixedDeferred;

        public FrameWorkScheduler(in FrameBudgetPolicy policy)
        {
            _policy = policy;
            int capacity = policy.QueueCapacityPerPhase;

            _updateCritical = new RingBuffer<FrameWorkItem>(capacity);
            _updateImportant = new RingBuffer<FrameWorkItem>(capacity);
            _updateDeferred = new RingBuffer<FrameWorkItem>(capacity);

            _lateCritical = new RingBuffer<FrameWorkItem>(capacity);
            _lateImportant = new RingBuffer<FrameWorkItem>(capacity);
            _lateDeferred = new RingBuffer<FrameWorkItem>(capacity);

            _fixedCritical = new RingBuffer<FrameWorkItem>(capacity);
            _fixedImportant = new RingBuffer<FrameWorkItem>(capacity);
            _fixedDeferred = new RingBuffer<FrameWorkItem>(capacity);
        }

        public void Enqueue(in FrameWorkItem item)
        {
            FrameWorkItem queued = item.WithEnqueuedFrame(Time.frameCount);
            RingBuffer<FrameWorkItem> queue = GetQueue(item.Phase, item.Priority);
            if (!queue.EnqueueNoResize(queued))
            {
                if (item.Priority == FrameTaskPriority.Critical)
                {
                    Debug.LogError($"Frame Critical queue is full for {item.Phase}. Executing immediately to avoid logic break.");
                    item.Callback();
                }
                else
                {
                    Debug.LogError($"Frame queue is full for {item.Phase}/{item.Priority}. Task '{item.Tag}' dropped.");
                }
            }
        }

        public FrameSchedulerRunResult RunPhase(FramePhase phase, int currentFrame, float budgetMs, float startTimeMs, Action<bool> onExceededChanged)
        {
            int executedCount = 0;
            int deferredCount = 0;
            int overdueCount = 0;
            int importantExecuted = 0;
            bool agedImportantSpilloverUsed = false;

            ExecuteCriticalQueue(phase, ref executedCount);

            bool IsExceeded() => (Time.realtimeSinceStartup * 1000f - startTimeMs) > budgetMs;

            if (!IsExceeded())
            {
                ExecuteImportantQueue(phase, currentFrame, ref executedCount, ref deferredCount, ref importantExecuted, ref agedImportantSpilloverUsed, budgetMs, startTimeMs, onExceededChanged);
                
                if (!IsExceeded())
                {
                    ExecuteDeferredQueueWhenBudgetAvailable(phase, currentFrame, ref executedCount, ref deferredCount, ref overdueCount, budgetMs, startTimeMs, onExceededChanged);
                }
                else
                {
                    deferredCount += GetQueue(phase, FrameTaskPriority.Deferred).Count;
                }
            }
            else
            {
                deferredCount += GetQueue(phase, FrameTaskPriority.Important).Count;
                ForceOverdueDeferred(phase, currentFrame, ref executedCount, ref deferredCount, ref overdueCount);
            }

            return new FrameSchedulerRunResult(executedCount, deferredCount, overdueCount);
        }

        private void ExecuteCriticalQueue(FramePhase phase, ref int executedCount)
        {
            RingBuffer<FrameWorkItem> queue = GetQueue(phase, FrameTaskPriority.Critical);
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                if (!queue.TryDequeue(out FrameWorkItem item))
                {
                    break;
                }

                InvokeCallbackSafely(item, ref executedCount);
            }
        }

        private void ExecuteImportantQueue(
            FramePhase phase,
            int currentFrame,
            ref int executedCount,
            ref int deferredCount,
            ref int importantExecuted,
            ref bool agedImportantSpilloverUsed,
            float budgetMs,
            float startTimeMs,
            Action<bool> onExceededChanged)
        {
            RingBuffer<FrameWorkItem> queue = GetQueue(phase, FrameTaskPriority.Important);
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                if (importantExecuted >= _policy.ImportantMaxPerFrame)
                {
                    // Check age for spillover before stopping
                    if (!queue.TryPeek(out FrameWorkItem nextItem)) break;
                    int age = currentFrame - nextItem.EnqueuedFrame;
                    if (age < 1 || agedImportantSpilloverUsed)
                    {
                        break;
                    }
                    // Continue to spillover check below
                }

                if ((Time.realtimeSinceStartup * 1000f - startTimeMs) > budgetMs)
                {
                    onExceededChanged?.Invoke(true);
                    break;
                }

                if (!queue.TryDequeue(out FrameWorkItem item))
                {
                    break;
                }

                if (importantExecuted >= _policy.ImportantMaxPerFrame)
                {
                    // This must be a spillover item (age >= 1 and spillover not used)
                    agedImportantSpilloverUsed = true;
                }

                InvokeCallbackSafely(item, ref executedCount);
                importantExecuted++;
            }

            deferredCount += queue.Count;
        }

        private void ExecuteDeferredQueueWhenBudgetAvailable(
            FramePhase phase,
            int currentFrame,
            ref int executedCount,
            ref int deferredCount,
            ref int overdueCount,
            float budgetMs,
            float startTimeMs,
            Action<bool> onExceededChanged)
        {
            RingBuffer<FrameWorkItem> queue = GetQueue(phase, FrameTaskPriority.Deferred);
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                if (!queue.TryDequeue(out FrameWorkItem item))
                {
                    break;
                }

                bool isOverdue = currentFrame - item.EnqueuedFrame > _policy.MaxDeferredFrames;
                if (isOverdue)
                {
                    InvokeCallbackSafely(item, ref executedCount);
                    overdueCount++;
                }
                else
                {
                    Requeue(item);
                }
            }
            deferredCount += queue.Count;
        }

        private void ForceOverdueDeferred(
            FramePhase phase,
            int currentFrame,
            ref int executedCount,
            ref int deferredCount,
            ref int overdueCount)
        {
            RingBuffer<FrameWorkItem> queue = GetQueue(phase, FrameTaskPriority.Deferred);
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                if (!queue.TryDequeue(out FrameWorkItem item))
                {
                    break;
                }

                if (currentFrame - item.EnqueuedFrame > _policy.MaxDeferredFrames)
                {
                    InvokeCallbackSafely(item, ref executedCount);
                    overdueCount++;
                }
                else
                {
                    Requeue(item);
                    deferredCount++;
                }
            }
        }

        private void Requeue(in FrameWorkItem item)
        {
            RingBuffer<FrameWorkItem> queue = GetQueue(item.Phase, item.Priority);
            if (!queue.EnqueueNoResize(item))
            {
                Debug.LogError($"Frame queue overflow during requeue for {item.Phase}/{item.Priority}. Task '{item.Tag}' dropped.");
            }
        }

        private RingBuffer<FrameWorkItem> GetQueue(FramePhase phase, FrameTaskPriority priority)
        {
            switch (phase)
            {
                case FramePhase.Update:
                    switch (priority)
                    {
                        case FrameTaskPriority.Critical: return _updateCritical;
                        case FrameTaskPriority.Important: return _updateImportant;
                        case FrameTaskPriority.Deferred: return _updateDeferred;
                    }
                    break;
                case FramePhase.LateUpdate:
                    switch (priority)
                    {
                        case FrameTaskPriority.Critical: return _lateCritical;
                        case FrameTaskPriority.Important: return _lateImportant;
                        case FrameTaskPriority.Deferred: return _lateDeferred;
                    }
                    break;
                case FramePhase.FixedUpdate:
                    switch (priority)
                    {
                        case FrameTaskPriority.Critical: return _fixedCritical;
                        case FrameTaskPriority.Important: return _fixedImportant;
                        case FrameTaskPriority.Deferred: return _fixedDeferred;
                    }
                    break;
            }

            throw new ArgumentOutOfRangeException(nameof(phase), phase, $"Unknown queue mapping for {phase}/{priority}.");
        }

        private static void InvokeCallbackSafely(in FrameWorkItem item, ref int executedCount)
        {
            try
            {
                item.Callback();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Frame work item callback threw. Phase={item.Phase}, Priority={item.Priority}, Tag={item.Tag}, Exception={exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                executedCount++;
            }
        }
    }
}
