# Fun Timer 设计文档

## 概述

在 `UnityProject/Assets/Fun/Runtime` 实现一套生产级别的定时器系统。
核心原则：**简洁优先，拒绝复杂**。

## 需求

- 延迟回调（Delay）
- 间隔循环（Repeat）
- 帧更新（EveryFrame）
- async/await 支持（DelayAsync）
- CancellationToken 取消
- 可选时间尺度（scaled/unscaled）
- 仅 Play Mode
- 单线程 Update 驱动

## API 设计

```csharp
public static class Timer
{
    // 延迟（秒），scaled=true 用 Time.time，false 用 Time.unscaledTime
    public static IDisposable Delay(float seconds, Action callback,
        CancellationToken ct = default, bool scaled = true);

    // 重复执行（秒）
    public static IDisposable Repeat(float interval, Action callback,
        CancellationToken ct = default, bool scaled = true);

    // 每帧回调，deltaTime = Time.deltaTime 或 Time.unscaledDeltaTime
    public static IDisposable EveryFrame(Action<float deltaTime, CancellationToken> onFrame,
        CancellationToken ct = default, bool scaled = true);

    // async/await 风格
    public static Awaitable DelayAsync(float seconds,
        CancellationToken ct = default, bool scaled = true);
}
```

## 架构

```
Timer.cs (单文件)
├── TimerEntry       // 定时器条目，内部类
├── TimerDriver       // 内部 MonoBehaviour，驱动 Update
└── Timer            // 公开静态 API
```

- `TimerDriver` 在首次访问 Timer API 时懒创建
- GameObject 名为 `[Fun.Timer]`，HideFlags.HideInHierarchy，DontDestroyOnLoad
- 所有定时器在 TimerDriver.Update() 中集中处理

## 数据结构

- `_delays: Dictionary<CancellationToken, TimerEntry>`
- `_repeats: Dictionary<CancellationToken, TimerEntry>`
- `_frames: Dictionary<CancellationToken, TimerEntry>`

TimerEntry 包含：
```csharp
class TimerEntry
{
    public float interval;
    public float elapsed;
    public Action callback;
    public CancellationTokenSource cts; // 组合取消
    public bool scaled;
    public TimerKind kind; // Delay, Repeat, Frame
}
```

## 生命周期

- `IDisposable` 返回 TimerEntry 本身
- `Dispose()` 从对应字典移除，定时器中止
- CancellationToken 取消时自动移除

## 错误处理

- callback 异常捕获后 `Debug.LogWarning`，不向外抛
- 无 callback 或已取消时安全忽略

## 文件结构

```
UnityProject/Assets/Fun/Runtime/
└── Timer/
    ├── Timer.cs         // 主文件
    └── Timer.cs.meta
```

## 实现要点

1. **无额外 allocations**：返回 TimerEntry 作为 IDisposable，不创建包装对象
2. **无 MonoBehaviour 污染**：TimerDriver 隐藏在内部
3. **线程安全**：仅在主线程调用，字典操作不加锁
4. **简单优先**：不支持编辑器 Edit Mode，不支持高精度 Stopwatch
