using System;
using System.Threading;

namespace Change.Runtime
{
    internal enum TimerKind
    {
        Delay,
        Repeat,
        Frame
    }

    internal sealed class TimerEntry : IDisposable
    {
        public float interval;
        public float elapsed;
        public Action callback;
        public CancellationTokenSource cts;
        public bool scaled;
        public TimerKind kind;
        public bool isDone;

        public void Dispose()
        {
            if (isDone)
            {
                return;
            }

            isDone = true;
            cts?.Cancel();
        }
    }
}
