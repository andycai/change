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
                throw new InvalidOperationException($"Frame queue is full for {item.Phase}/{item.Priority}.");
            }
        }

        public FrameSchedulerRunResult RunPhase(FramePhase phase, int currentFrame, float remainingBudgetMs)
        {
            int executedCount = 0;
            int deferredCount = 0;
            int overdueCount = 0;
            int importantExecuted = 0;
            bool agedImportantSpilloverUsed = false;

            ExecuteCriticalQueue(phase, ref executedCount);

            if (remainingBudgetMs > 0f)
            {
                ExecuteImportantQueue(phase, currentFrame, ref executedCount, ref deferredCount, ref importantExecuted, ref agedImportantSpilloverUsed);
                ExecuteDeferredQueueWhenBudgetAvailable(phase, currentFrame, ref executedCount, ref deferredCount, ref overdueCount);
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
            ref bool agedImportantSpilloverUsed)
        {
            RingBuffer<FrameWorkItem> queue = GetQueue(phase, FrameTaskPriority.Important);
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                if (!queue.TryDequeue(out FrameWorkItem item))
                {
                    break;
                }

                if (importantExecuted >= _policy.ImportantMaxPerFrame)
                {
                    int age = currentFrame - item.EnqueuedFrame;
                    if (age < 1 || agedImportantSpilloverUsed)
                    {
                        Requeue(item);
                        deferredCount++;
                        continue;
                    }

                    agedImportantSpilloverUsed = true;
                }

                InvokeCallbackSafely(item, ref executedCount);
                importantExecuted++;
            }
        }

        private void ExecuteDeferredQueueWhenBudgetAvailable(
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

                bool isOverdue = currentFrame - item.EnqueuedFrame > _policy.MaxDeferredFrames;
                if (isOverdue)
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
                throw new InvalidOperationException($"Frame queue is full for {item.Phase}/{item.Priority}.");
            }
        }

        private RingBuffer<FrameWorkItem> GetQueue(FramePhase phase, FrameTaskPriority priority)
        {
            return (phase, priority) switch
            {
                (FramePhase.Update, FrameTaskPriority.Critical) => _updateCritical,
                (FramePhase.Update, FrameTaskPriority.Important) => _updateImportant,
                (FramePhase.Update, FrameTaskPriority.Deferred) => _updateDeferred,
                (FramePhase.LateUpdate, FrameTaskPriority.Critical) => _lateCritical,
                (FramePhase.LateUpdate, FrameTaskPriority.Important) => _lateImportant,
                (FramePhase.LateUpdate, FrameTaskPriority.Deferred) => _lateDeferred,
                (FramePhase.FixedUpdate, FrameTaskPriority.Critical) => _fixedCritical,
                (FramePhase.FixedUpdate, FrameTaskPriority.Important) => _fixedImportant,
                (FramePhase.FixedUpdate, FrameTaskPriority.Deferred) => _fixedDeferred,
                _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, $"Unknown queue mapping for {phase}/{priority}."),
            };
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
