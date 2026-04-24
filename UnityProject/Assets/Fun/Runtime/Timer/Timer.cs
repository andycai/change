using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace Fun.Runtime
{
    internal enum TimerKind { Delay, Repeat, Frame }

    internal class TimerEntry : IDisposable
    {
        public float interval;
        public float elapsed;
        public Action callback;
        public CancellationTokenSource cts;
        public bool scaled;
        public TimerKind kind;
        public bool isDone;

        public void Dispose() => isDone = true;
    }

    public static class Timer
    {
        public static IDisposable Delay(float seconds, Action callback,
            CancellationToken ct = default, bool scaled = true)
        {
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

        public static IDisposable Repeat(float interval, Action callback,
            CancellationToken ct = default, bool scaled = true)
        {
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
            CancellationToken ct = default, bool scaled = true)
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

        public static TimerAwaiter DelayAsync(float seconds,
            CancellationToken ct = default, bool scaled = true)
        {
            return new TimerAwaiter(seconds, scaled, ct);
        }
    }

    public sealed class TimerAwaiter : INotifyCompletion
    {
        private readonly float _seconds;
        private readonly bool _scaled;
        private CancellationToken _ct;
        private Action _continuation;

        public TimerAwaiter(float seconds, bool scaled, CancellationToken ct)
        {
            _seconds = seconds;
            _scaled = scaled;
            _ct = ct;
        }

        public bool IsCompleted => false;

        public void GetResult() { }

        public void OnCompleted(Action continuation)
        {
            _continuation = continuation;
            Timer.Delay(_seconds, () =>
            {
                if (!_ct.IsCancellationRequested)
                    _continuation?.Invoke();
            }, _ct, _scaled);
        }
    }

    internal static class TimerDriver
    {
        private static GameObject _gameObject;
        private static readonly Dictionary<CancellationToken, TimerEntry> _delays = new();
        private static readonly Dictionary<CancellationToken, TimerEntry> _repeats = new();
        private static readonly Dictionary<CancellationToken, TimerEntry> _frames = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            _delays.Clear();
            _repeats.Clear();
            _frames.Clear();
            if (_gameObject != null)
            {
                UnityEngine.Object.Destroy(_gameObject);
                _gameObject = null;
            }
        }

        public static void EnsureExists()
        {
            if (_gameObject != null) return;
            _gameObject = new GameObject("[Fun.Timer]");
            _gameObject.hideFlags = HideFlags.HideInHierarchy;
            _gameObject.AddComponent<Driver>();
        }

        internal static void Add(TimerEntry entry, TimerKind kind)
        {
            switch (kind)
            {
                case TimerKind.Delay:
                    _delays[entry.cts.Token] = entry;
                    break;
                case TimerKind.Repeat:
                    _repeats[entry.cts.Token] = entry;
                    break;
                case TimerKind.Frame:
                    _frames[entry.cts.Token] = entry;
                    break;
            }
        }

        private sealed class Driver : MonoBehaviour
        {
            private void Update()
            {
                float dt = Time.deltaTime;
                float unscaledDt = Time.unscaledDeltaTime;

                Process(_delays, dt, unscaledDt, TimerKind.Delay);
                Process(_repeats, dt, unscaledDt, TimerKind.Repeat);
                Process(_frames, dt, unscaledDt, TimerKind.Frame);
            }

            private static void Process(Dictionary<CancellationToken, TimerEntry> dict, float dt, float unscaledDt, TimerKind kind)
            {
                List<CancellationToken> tokensToRemove = null;

                foreach (var kvp in dict)
                {
                    var entry = kvp.Value;

                    if (entry.isDone || entry.cts.IsCancellationRequested)
                    {
                        if (tokensToRemove == null) tokensToRemove = new List<CancellationToken>();
                        tokensToRemove.Add(kvp.Key);
                        continue;
                    }

                    if (entry.kind == TimerKind.Frame)
                    {
                        try
                        {
                            entry.callback?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[Fun.Timer] Callback threw: {ex.Message}");
                        }

                        if (entry.cts.IsCancellationRequested)
                        {
                            if (tokensToRemove == null) tokensToRemove = new List<CancellationToken>();
                            tokensToRemove.Add(kvp.Key);
                        }
                        continue;
                    }

                    float elapsed = entry.scaled ? (entry.elapsed + dt) : (entry.elapsed + unscaledDt);
                    entry.elapsed = elapsed;

                    if (elapsed >= entry.interval)
                    {
                        try
                        {
                            entry.callback?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[Fun.Timer] Callback threw: {ex.Message}");
                        }

                        if (entry.kind == TimerKind.Delay)
                        {
                            entry.isDone = true;
                            if (tokensToRemove == null) tokensToRemove = new List<CancellationToken>();
                            tokensToRemove.Add(kvp.Key);
                        }
                        else
                        {
                            entry.elapsed = 0f;
                        }
                    }
                }

                if (tokensToRemove != null)
                {
                    foreach (var token in tokensToRemove)
                        dict.Remove(token);
                }
            }
        }
    }
}
