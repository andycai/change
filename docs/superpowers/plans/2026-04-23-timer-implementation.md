# Fun Timer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 UnityProject/Assets/Fun/Runtime/Timer 实现生产级定时器，单文件，无依赖，支持 Delay/Repeat/EveryFrame/DelayAsync。

**Architecture:** 静态 Timer API，内部 TimerDriver (MonoBehaviour) 驱动 Update，TimerEntry 承载所有状态。字典按 CancellationToken 索引，支持 Dispose 和 CT 取消两种停止方式。

**Tech Stack:** 纯 C# + Unity Engine，无外部依赖。

---

## File Structure

```
UnityProject/Assets/Fun/Runtime/Timer/
└── Timer.cs    # 全部代码
```

> Unity meta 文件由编辑器自动生成，无需手动创建。

---

## Task 1: TimerEntry 内部类

**Files:**
- Create: `UnityProject/Assets/Fun/Runtime/Timer/Timer.cs`

- [ ] **Step 1: 创建 Timer.cs 骨架，定义命名空间和类结构**

```csharp
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
            private static void Init() => _delays.Clear();

            public static void EnsureExists()
            {
                if (_gameObject != null) return;
                _gameObject = new GameObject("[Fun.Timer]");
                _gameObject.hideFlags = HideFlags.HideInHierarchy;
                _gameObject.AddComponent<TimerDriver>();
            }

            private class Driver : MonoBehaviour
            {
                void Update()
                {
                    float dt = Time.deltaTime;
                    float unscaledDt = Time.unscaledDeltaTime;
                    // process all dictionaries...
                }
            }
        }
    }
}
```

- [ ] **Step 2: 补全 Update 处理逻辑（process 方法）**

在 Driver.Update 中调用 Process 方法，处理三种字典：

```csharp
private static void Process(Dictionary<CancellationToken, TimerEntry> dict, float dt, float unscaledDt)
{
    var tokensToRemove = List<CancellationToken>.Empty;
    foreach (var kvp in dict)
    {
        var entry = kvp.Value;
        if (entry.isDone || entry.cts.IsCancellationRequested)
        {
            if (tokensToRemove == null) tokensToRemove = new List<CancellationToken>();
            tokensToRemove.Add(kvp.Key);
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
```

- [ ] **Step 3: 提交**

---

## Task 2: 公开静态 API

**Files:**
- Modify: `UnityProject/Assets/Fun/Runtime/Timer/Timer.cs`

- [ ] **Step 1: 添加 Delay 方法**

```csharp
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
    TimerDriver.AddDelay(entry);
    return entry;
}
```

- [ ] **Step 2: 添加 Repeat 方法**

```csharp
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
    TimerDriver.AddRepeat(entry);
    return entry;
}
```

- [ ] **Step 3: 添加 EveryFrame 方法**

```csharp
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
    TimerDriver.AddFrame(entry);
    return entry;
}
```

- [ ] **Step 4: 添加 AddDelay/AddRepeat/AddFrame 到 TimerDriver**

```csharp
internal static void AddDelay(TimerEntry entry) => _delays[entry.cts.Token] = entry;
internal static void AddRepeat(TimerEntry entry) => _repeats[entry.cts.Token] = entry;
internal static void AddFrame(TimerEntry entry) => _frames[entry.cts.Token] = entry;
```

- [ ] **Step 5: 提交**

---

## Task 3: DelayAsync (async/await 支持)

**Files:**
- Modify: `UnityProject/Assets/Fun/Runtime/Timer/Timer.cs`

- [ ] **Step 1: 添加 Awaitable 静态类（链式 api）**

```csharp
public static class Awaitable
{
    private class Awaiter : INotifyCompletion
    {
        private readonly float _seconds;
        private readonly bool _scaled;
        private CancellationToken _ct;
        private Action _continuation;

        public Awaiter(float seconds, bool scaled, CancellationToken ct)
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

    public static Awaiter Delay(float seconds, CancellationToken ct = default, bool scaled = true)
    {
        return new Awaiter(seconds, scaled, ct);
    }
}
```

- [ ] **Step 2: 添加 Timer.DelayAsync**

```csharp
public static Awaitable.Awaiter DelayAsync(float seconds,
    CancellationToken ct = default, bool scaled = true)
{
    return Awaitable.Delay(seconds, ct, scaled);
}
```

- [ ] **Step 3: 提交**

---

## Task 4: 清理入口点

**Files:**
- Modify: `UnityProject/Assets/Fun/Runtime/Timer/Timer.cs`

- [ ] **Step 1: 在 TimerDriver 的 RuntimeInitializeOnLoadMethod 中清理所有字典并置空 GameObject**

```csharp
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
```

- [ ] **Step 2: 检查 TimerEntry 的 callback 是否在 CT 取消时安全清理**

Dispose 实现确保 isDone=true，下次 Update 扫描时移除。CT 取消同理。

- [ ] **Step 3: 提交**

---

## Task 5: 最终检查

- [ ] 确认文件位于 `UnityProject/Assets/Fun/Runtime/Timer/Timer.cs`
- [ ] 确认无编译错误（C# 语法检查通过）
- [ ] 确认 namespace 为 `Fun.Runtime`
- [ ] 提交最终版本

---

## 依赖关系

```
Task 1 → Task 2 → Task 3 → Task 4 → Task 5
```

---

## 验收标准

1. `Timer.Delay(seconds, callback, ct, scaled)` 可用
2. `Timer.Repeat(interval, callback, ct, scaled)` 可用
3. `Timer.EveryFrame(onFrame, ct, scaled)` 可用
4. `await Timer.DelayAsync(seconds, ct, scaled)` 可用
5. `IDisposable.Dispose()` 可停止定时器
6. `CancellationToken` 取消可停止定时器
7. scaled=false 使用 Time.unscaledDeltaTime
8. 无 MonoBehaviour 暴露在公开 API
9. 单文件实现
