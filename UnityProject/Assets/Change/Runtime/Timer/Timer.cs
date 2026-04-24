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

        public void Dispose()
        {
            if (isDone) return;
            isDone = true;
            cts?.Cancel();
        }
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
        private readonly CancellationToken _ct;
        private Action _continuation;
        private IDisposable _delayHandle;
        private IDisposable _cancelPollHandle;
        private int _isCompleted;

        public TimerAwaiter(float seconds, bool scaled, CancellationToken ct)
        {
            _seconds = seconds;
            _scaled = scaled;
            _ct = ct;
        }

        public TimerAwaiter GetAwaiter() => this;

        public bool IsCompleted => _ct.IsCancellationRequested;

        public void GetResult() => _ct.ThrowIfCancellationRequested();

        public void OnCompleted(Action continuation)
        {
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));
            if (_ct.IsCancellationRequested)
            {
                continuation();
                return;
            }

            _continuation = continuation;
            _delayHandle = Timer.Delay(_seconds, Complete, CancellationToken.None, _scaled);
            _cancelPollHandle = Timer.EveryFrame((_, _) =>
            {
                if (_ct.IsCancellationRequested)
                {
                    Complete();
                }
            }, CancellationToken.None, scaled: false);

            if (_ct.IsCancellationRequested)
            {
                Complete();
            }
        }

        private void Complete()
        {
            if (Interlocked.Exchange(ref _isCompleted, 1) != 0) return;

            _delayHandle?.Dispose();
            _cancelPollHandle?.Dispose();
            _delayHandle = null;
            _cancelPollHandle = null;

            InvokeContinuation();
        }

        private void InvokeContinuation()
        {
            var continuation = _continuation;
            _continuation = null;
            continuation?.Invoke();
        }
    }

    internal static class TimerDriver
    {
        private static GameObject _gameObject;
        private static readonly Dictionary<CancellationToken, TimerEntry> _delays = new();
        private static readonly Dictionary<CancellationToken, TimerEntry> _repeats = new();
        private static readonly Dictionary<CancellationToken, TimerEntry> _frames = new();
        private static readonly List<TimerEntry> _pendingDelays = new();
        private static readonly List<TimerEntry> _pendingRepeats = new();
        private static readonly List<TimerEntry> _pendingFrames = new();
        private static bool _isProcessingDelays;
        private static bool _isProcessingRepeats;
        private static bool _isProcessingFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            ClearAndDispose(_delays);
            ClearAndDispose(_repeats);
            ClearAndDispose(_frames);
            ClearAndDisposePending(_pendingDelays);
            ClearAndDisposePending(_pendingRepeats);
            ClearAndDisposePending(_pendingFrames);
            _isProcessingDelays = false;
            _isProcessingRepeats = false;
            _isProcessingFrames = false;
            if (_gameObject != null)
            {
                UnityEngine.Object.Destroy(_gameObject);
                _gameObject = null;
            }
        }

        public static void EnsureExists()
        {
            if (_gameObject != null) return;
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("[Fun.Timer] Timer can only be used in Play Mode.");
            }

            _gameObject = new GameObject("[Fun.Timer]");
            _gameObject.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(_gameObject);
            _gameObject.AddComponent<Driver>();
        }

        internal static void Add(TimerEntry entry, TimerKind kind)
        {
            switch (kind)
            {
                case TimerKind.Delay:
                    AddOrQueue(_delays, _pendingDelays, ref _isProcessingDelays, entry);
                    break;
                case TimerKind.Repeat:
                    AddOrQueue(_repeats, _pendingRepeats, ref _isProcessingRepeats, entry);
                    break;
                case TimerKind.Frame:
                    AddOrQueue(_frames, _pendingFrames, ref _isProcessingFrames, entry);
                    break;
            }
        }

        private static void AddOrQueue(
            Dictionary<CancellationToken, TimerEntry> dict,
            List<TimerEntry> pending,
            ref bool isProcessing,
            TimerEntry entry)
        {
            if (isProcessing)
            {
                pending.Add(entry);
                return;
            }

            AddDirect(dict, entry);
        }

        private static void AddDirect(Dictionary<CancellationToken, TimerEntry> dict, TimerEntry entry)
        {
            var token = entry.cts.Token;
            if (dict.TryGetValue(token, out var existing))
            {
                DisposeEntry(existing);
            }

            dict[token] = entry;
        }

        private static void FlushPending(Dictionary<CancellationToken, TimerEntry> dict, List<TimerEntry> pending)
        {
            if (pending.Count == 0) return;
            for (int i = 0; i < pending.Count; i++)
            {
                AddDirect(dict, pending[i]);
            }

            pending.Clear();
        }

        private static void RemoveEntry(Dictionary<CancellationToken, TimerEntry> dict, CancellationToken token)
        {
            if (!dict.TryGetValue(token, out var entry)) return;
            dict.Remove(token);
            DisposeEntry(entry);
        }

        private static void ClearAndDispose(Dictionary<CancellationToken, TimerEntry> dict)
        {
            foreach (var entry in dict.Values)
            {
                DisposeEntry(entry);
            }

            dict.Clear();
        }

        private static void ClearAndDisposePending(List<TimerEntry> pending)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                DisposeEntry(pending[i]);
            }

            pending.Clear();
        }

        private static void DisposeEntry(TimerEntry entry)
        {
            if (entry == null) return;

            entry.isDone = true;
            entry.callback = null;

            if (entry.cts != null)
            {
                entry.cts.Dispose();
                entry.cts = null;
            }
        }

        private sealed class Driver : MonoBehaviour
        {
            private void Update()
            {
                float dt = Time.deltaTime;
                float unscaledDt = Time.unscaledDeltaTime;

                Process(_delays, _pendingDelays, ref _isProcessingDelays, dt, unscaledDt);
                Process(_repeats, _pendingRepeats, ref _isProcessingRepeats, dt, unscaledDt);
                Process(_frames, _pendingFrames, ref _isProcessingFrames, dt, unscaledDt);
            }

            private static void Process(
                Dictionary<CancellationToken, TimerEntry> dict,
                List<TimerEntry> pending,
                ref bool isProcessing,
                float dt,
                float unscaledDt)
            {
                List<CancellationToken> tokensToRemove = null;

                isProcessing = true;
                try
                {
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
                                entry.elapsed = Mathf.Max(0f, elapsed - entry.interval);
                            }
                        }
                    }
                }
                finally
                {
                    isProcessing = false;
                }

                if (tokensToRemove != null)
                {
                    foreach (var token in tokensToRemove)
                    {
                        RemoveEntry(dict, token);
                    }
                }

                FlushPending(dict, pending);
            }
        }
    }
}
