using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

namespace Fun.Runtime
{
    public static class Timer
    {
        private enum TimerKind { Delay, Repeat, Frame }

        private class TimerEntry : IDisposable
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

        private static class TimerDriver
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
            }

            public static void EnsureExists()
            {
                if (_gameObject != null) return;
                _gameObject = new GameObject("[Fun.Timer]");
                _gameObject.hideFlags = HideFlags.HideInHierarchy;
                _gameObject.AddComponent<Driver>();
            }

            private class Driver : MonoBehaviour
            {
                void Update()
                {
                    float dt = Time.deltaTime;
                    float unscaledDt = Time.unscaledDeltaTime;

                    Process(_delays, dt, unscaledDt);
                    Process(_repeats, dt, unscaledDt);
                    Process(_frames, dt, unscaledDt);
                }
            }

            private static void Process(Dictionary<CancellationToken, TimerEntry> dict, float dt, float unscaledDt)
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

                    // Frame entries (interval == 0) are invoked every Update
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
                            entry.elapsed = 0f; // reset for repeat
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
