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

            var entry = CreateEntry(seconds, callback, ct, scaled, TimerKind.Delay);
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

            if (interval <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), interval,
                    "Repeat interval must be greater than zero. Use EveryFrame for per-frame callbacks.");
            }

            TimerDriver.EnsureExists();

            var entry = CreateEntry(interval, callback, ct, scaled, TimerKind.Repeat);
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
            var entry = new TimerEntry(
                0f,
                () => onFrame(scaled ? Time.deltaTime : Time.unscaledDeltaTime, cts.Token),
                cts,
                scaled,
                TimerKind.Frame);

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

        private static TimerEntry CreateEntry(
            float interval,
            Action callback,
            CancellationToken ct,
            bool scaled,
            TimerKind kind)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            return new TimerEntry(interval, callback, cts, scaled, kind);
        }
    }
}
