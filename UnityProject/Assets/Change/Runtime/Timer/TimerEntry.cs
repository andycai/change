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
        internal float interval;
        internal float elapsed;
        internal Action callback;
        internal CancellationTokenSource cts;
        internal bool scaled;
        internal readonly TimerKind kind;
        internal bool isDone;

        internal TimerEntry(float interval, Action callback, CancellationTokenSource cts, bool scaled, TimerKind kind)
        {
            this.interval = interval;
            this.callback = callback;
            this.cts = cts;
            this.scaled = scaled;
            this.kind = kind;
        }

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
