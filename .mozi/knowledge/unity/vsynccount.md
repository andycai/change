# Unity vSyncCount 使用指南和适用场景

`QualitySettings.vSyncCount` 用于控制 Unity 的垂直同步。它表示 Unity 渲染一帧前需要等待多少次显示器垂直刷新信号。

## 基本含义

| `vSyncCount` | 含义 | 60Hz 屏幕理论帧率 | 120Hz 屏幕理论帧率 |
|---|---|---:|---:|
| `0` | 不等待垂直同步 | 不限制，取决于性能或 `Application.targetFrameRate` | 不限制 |
| `1` | 每次屏幕刷新渲染一帧 | 60 FPS | 120 FPS |
| `2` | 每 2 次屏幕刷新渲染一帧 | 30 FPS | 60 FPS |
| `3` | 每 3 次屏幕刷新渲染一帧 | 20 FPS | 40 FPS |
| `4` | 每 4 次屏幕刷新渲染一帧 | 15 FPS | 30 FPS |

## 基本用法

开启垂直同步：

```csharp
QualitySettings.vSyncCount = 1;
```

关闭垂直同步，并使用目标帧率控制：

```csharp
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 60;
```

## 与 Application.targetFrameRate 的关系

当 `vSyncCount > 0` 时，桌面平台上通常由垂直同步控制帧节奏，`Application.targetFrameRate` 可能被覆盖或弱化。

例如：

```csharp
QualitySettings.vSyncCount = 1;
Application.targetFrameRate = 30;
```

在 60Hz 显示器上，实际可能仍然是 60 FPS，而不是 30 FPS。

如果要明确限制帧率，通常应该关闭 vSync：

```csharp
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 30;
```

## 平台差异

### PC / Mac / 主机

桌面平台上，`vSyncCount` 通常有效。

推荐用于追求画面稳定和平滑的场景：

```csharp
QualitySettings.vSyncCount = 1;
Application.targetFrameRate = -1;
```

优点：

- 减少画面撕裂。
- 画面更稳定。
- 帧节奏通常更平滑。

缺点：

- 可能增加输入延迟。
- 性能不足时可能出现明显掉帧。
- 实际帧率依赖显示器刷新率。

### iOS / Android

移动平台上，`QualitySettings.vSyncCount` 通常不作为主要帧率控制手段。更推荐关闭 vSync，使用 `Application.targetFrameRate` 控制帧率。

省电模式：

```csharp
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 30;
```

标准模式：

```csharp
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 60;
```

高刷新率模式：

```csharp
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 120;
```

移动设备最终帧率还会受到系统调度、发热、节能策略、屏幕刷新率和后台限制影响。

### WebGL

WebGL 通常由浏览器的 `requestAnimationFrame` 控制节奏。一般不建议强依赖 `vSyncCount`，应更多依赖浏览器刷新节奏和 Unity 的目标帧率设置。

### VR / XR

VR/XR 通常由 XR runtime 控制帧率和同步节奏。不建议随意使用 `vSyncCount` 控制 VR 帧率，应优先使用对应 XR SDK、平台配置或设备推荐设置。

## 常见适用场景

### PC 单机游戏，追求画面平滑

推荐：

```csharp
QualitySettings.vSyncCount = 1;
Application.targetFrameRate = -1;
```

适合 PC 单机、主机、RPG、策略游戏和画面表现优先的游戏。

### 竞技类游戏，追求低延迟

推荐：

```csharp
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 120;
```

适合 FPS、MOBA、动作竞技、音游和格斗游戏。

优点是输入延迟更低，帧率控制更自由；缺点是可能出现画面撕裂、GPU 占用更高、发热和功耗增加。

### 移动游戏，控制功耗和发热

推荐默认：

```csharp
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 60;
```

对于卡牌、回合制、放置、经营类游戏，可以考虑默认 30 FPS：

```csharp
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 30;
```

高性能设备可以开放 90 FPS 或 120 FPS 选项，但不建议默认强开。

### 录屏、展示、Demo

PC 上展示稳定 60 FPS：

```csharp
QualitySettings.vSyncCount = 1;
Application.targetFrameRate = -1;
```

固定 30 FPS 录制：

```csharp
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 30;
```

也可以在 60Hz 屏幕上使用 `vSyncCount = 2` 接近 30 FPS，但它依赖显示器刷新率，不如 `Application.targetFrameRate = 30` 明确。

## 推荐配置表

### PC 平台

| 目标 | 推荐设置 |
|---|---|
| 默认画面平滑 | `vSyncCount = 1`, `targetFrameRate = -1` |
| 低延迟竞技 | `vSyncCount = 0`, `targetFrameRate = 120 / 144 / 240` |
| 固定 60 FPS | `vSyncCount = 0`, `targetFrameRate = 60` |
| 固定 30 FPS | `vSyncCount = 0`, `targetFrameRate = 30` |

### 移动平台

| 目标 | 推荐设置 |
|---|---|
| 省电 | `vSyncCount = 0`, `targetFrameRate = 30` |
| 标准 | `vSyncCount = 0`, `targetFrameRate = 60` |
| 高刷 | `vSyncCount = 0`, `targetFrameRate = 90 / 120` |
| 自动调节 | 根据温度、电量、设备性能动态调整 `targetFrameRate` |

## 推荐封装

不要在业务代码中到处直接写 `QualitySettings.vSyncCount` 和 `Application.targetFrameRate`，建议封装统一策略。

```csharp
public static class FrameRateSettings
{
    public static void ApplyDesktopDefault()
    {
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
    }

    public static void ApplyDesktopLowLatency(int targetFps = 120)
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFps;
    }

    public static void ApplyMobileStandard()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }

    public static void ApplyMobilePowerSaving()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 30;
    }

    public static void ApplyMobileHighRefresh(int targetFps = 120)
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFps;
    }
}
```

使用示例：

```csharp
#if UNITY_STANDALONE || UNITY_EDITOR
FrameRateSettings.ApplyDesktopDefault();
#elif UNITY_IOS || UNITY_ANDROID
FrameRateSettings.ApplyMobileStandard();
#endif
```

## 常见坑

### `vSyncCount = 2` 不一定等于 30 FPS

在 60Hz 屏幕上，`vSyncCount = 2` 约等于 30 FPS；但在 120Hz 屏幕上，`vSyncCount = 2` 约等于 60 FPS。

如果要固定 30 FPS，更推荐：

```csharp
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 30;
```

### 开启 vSync 后，targetFrameRate 可能不生效

如果要让 `Application.targetFrameRate` 明确生效，应先关闭 vSync：

```csharp
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 30;
```

### 高刷新率设备上要谨慎

如果设备是 120Hz，且设置 `vSyncCount = 1`，游戏可能尝试跑到 120 FPS。这会增加 CPU/GPU 压力、功耗和发热。移动端高刷建议做成玩家可选项。

### 编辑器帧率不等于真机帧率

Unity Editor 的 Game View、Scene View、Profiler 和编辑器焦点状态都会影响帧率表现。最终判断应以真机、Development Build、Profiler 和平台性能工具为准。

## 实战建议

普通 Unity 手游项目默认策略：

```csharp
#if UNITY_IOS || UNITY_ANDROID
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 60;
#else
QualitySettings.vSyncCount = 1;
Application.targetFrameRate = -1;
#endif
```

卡牌、回合制、放置、经营类游戏可以考虑移动端默认 30 FPS：

```csharp
#if UNITY_IOS || UNITY_ANDROID
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 30;
#endif
```

动作、射击、竞技类游戏可以考虑：

```csharp
#if UNITY_IOS || UNITY_ANDROID
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 60;
#else
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 120;
#endif
```

## 总结

PC/主机上可以使用 `vSyncCount` 保证画面同步；移动端更推荐关闭 `vSyncCount`，使用 `Application.targetFrameRate` 控制帧率。

| 平台 | 推荐 |
|---|---|
| PC 单机 | `vSyncCount = 1` |
| PC 竞技 | `vSyncCount = 0` + 高 `targetFrameRate` |
| Android/iOS | `vSyncCount = 0` + `Application.targetFrameRate` |
| VR/XR | 交给 XR runtime 控制 |
| 固定帧率需求 | `vSyncCount = 0` + `Application.targetFrameRate` |

最常用配置：

```csharp
// 移动端标准 60 FPS
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 60;
```

```csharp
// PC 默认垂直同步
QualitySettings.vSyncCount = 1;
Application.targetFrameRate = -1;
```
