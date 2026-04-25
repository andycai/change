using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Change.Runtime
{
    internal static class TimerDriver
    {
        private static GameObject _gameObject;
        private static int _mainThreadId;

        private static readonly Dictionary<CancellationToken, TimerEntry> _delays = new();
        private static readonly Dictionary<CancellationToken, TimerEntry> _repeats = new();
        private static readonly Dictionary<CancellationToken, TimerEntry> _frames = new();
        private static readonly List<TimerEntry> _pendingDelays = new();
        private static readonly List<TimerEntry> _pendingRepeats = new();
        private static readonly List<TimerEntry> _pendingFrames = new();

        private static bool _isProcessingDelays;
        private static bool _isProcessingRepeats;
        private static bool _isProcessingFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CaptureMainThread()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;

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
            EnsureMainThread();

            if (_gameObject != null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("[Change.Timer] Timer can only be used in Play Mode.");
            }

            _gameObject = new GameObject("[Change.Timer]");
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

        private static void EnsureMainThread()
        {
            if (_mainThreadId == 0)
            {
                throw new InvalidOperationException("[Change.Timer] Timer main thread has not been initialized yet.");
            }

            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                throw new InvalidOperationException("[Change.Timer] Timer can only be used from the main thread.");
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
            if (pending.Count == 0)
            {
                return;
            }

            for (int i = 0; i < pending.Count; i++)
            {
                AddDirect(dict, pending[i]);
            }

            pending.Clear();
        }

        private static void RemoveEntry(Dictionary<CancellationToken, TimerEntry> dict, CancellationToken token)
        {
            if (!dict.TryGetValue(token, out var entry))
            {
                return;
            }

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
            if (entry == null)
            {
                return;
            }

            entry.isDone = true;
            entry.callback = null;

            if (entry.cts != null)
            {
                entry.cts.Dispose();
                entry.cts = null;
            }
        }

        private static bool InvokeCallback(TimerEntry entry)
        {
            try
            {
                entry.callback?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                entry.isDone = true;
                Debug.LogException(ex);
                return false;
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
                            tokensToRemove ??= new List<CancellationToken>();
                            tokensToRemove.Add(kvp.Key);
                            continue;
                        }

                        if (entry.kind == TimerKind.Frame)
                        {
                            if (!InvokeCallback(entry) || entry.cts.IsCancellationRequested)
                            {
                                tokensToRemove ??= new List<CancellationToken>();
                                tokensToRemove.Add(kvp.Key);
                            }

                            continue;
                        }

                        float elapsed = entry.scaled ? (entry.elapsed + dt) : (entry.elapsed + unscaledDt);
                        entry.elapsed = elapsed;

                        if (elapsed < entry.interval)
                        {
                            continue;
                        }

                        InvokeCallback(entry);

                        if (entry.kind == TimerKind.Delay || entry.isDone || entry.cts.IsCancellationRequested)
                        {
                            entry.isDone = true;
                            tokensToRemove ??= new List<CancellationToken>();
                            tokensToRemove.Add(kvp.Key);
                        }
                        else
                        {
                            entry.elapsed = Mathf.Max(0f, elapsed - entry.interval);
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
