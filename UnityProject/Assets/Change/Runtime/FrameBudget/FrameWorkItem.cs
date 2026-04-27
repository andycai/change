using System;

namespace Change.Runtime
{
    public readonly struct FrameWorkItem
    {
        private FrameWorkItem(FramePhase phase, FrameTaskPriority priority, string tag, Action callback, int enqueuedFrame)
        {
            Phase = phase;
            Priority = priority;
            Tag = tag;
            Callback = callback;
            EnqueuedFrame = enqueuedFrame;
        }

        public FramePhase Phase { get; }

        public FrameTaskPriority Priority { get; }

        public string Tag { get; }

        public Action Callback { get; }

        public int EnqueuedFrame { get; }

        public static FrameWorkItem Create(FramePhase phase, FrameTaskPriority priority, string tag, Action callback)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentException("Tag is required.", nameof(tag));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return new FrameWorkItem(phase, priority, tag, callback, enqueuedFrame: -1);
        }

        internal FrameWorkItem WithEnqueuedFrame(int frame)
        {
            return new FrameWorkItem(Phase, Priority, Tag, Callback, frame);
        }
    }
}
