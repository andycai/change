using System;
using System.Threading;
using UnityEngine;

namespace Change.Runtime
{
    public static class Timer
    {
        public static IDisposable Delay(
            float seconds,
            Action callback,
            CancellationToken ct = default,
            bool scaled = true)
        {
            ValidateDuration(seconds, nameof(seconds));
            TimerDriver.EnsureExists();

            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var entry = new TimerEntry
            {
                interval = seconds,
                elapsed = 0f,
                callback = callback,
                cts = cts,
                scaled = scaled,
                kind = TimerKind.Delay,
                isDone = false
            };

            TimerDriver.Add(entry, TimerKind.Delay);
            return entry;
        }

        public static IDisposable Repeat(
            float interval,
            Action callback,
            CancellationToken ct = default,
            bool scaled = true)
        {
            ValidateDuration(interval, nameof(interval));
            TimerDriver.EnsureExists();

            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var entry = new TimerEntry
            {
                interval = interval,
                elapsed = 0f,
                callback = callback,
                cts = cts,
                scaled = scaled,
                kind = TimerKind.Repeat,
                isDone = false
            };

            TimerDriver.Add(entry, TimerKind.Repeat);
            return entry;
        }

        public static IDisposable EveryFrame(
            Action<float, CancellationToken> onFrame,
            CancellationToken ct = default,
            bool scaled = true)
        {
            TimerDriver.EnsureExists();

            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var entry = new TimerEntry
            {
                interval = 0f,
                elapsed = 0f,
                callback = () => onFrame(scaled ? Time.deltaTime : Time.unscaledDeltaTime, cts.Token),
                cts = cts,
                scaled = scaled,
                kind = TimerKind.Frame,
                isDone = false
            };

            TimerDriver.Add(entry, TimerKind.Frame);
            return entry;
        }

        public static TimerAwaiter DelayAsync(
            float seconds,
            CancellationToken ct = default,
            bool scaled = true)
        {
            ValidateDuration(seconds, nameof(seconds));
            return new TimerAwaiter(seconds, scaled, ct);
        }

        internal static void ValidateDuration(float value, string paramName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(paramName, value,
                    "Timer duration must be finite and greater than or equal to zero.");
            }
        }
    }
}
