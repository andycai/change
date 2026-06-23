# Change.Runtime.Asset - 资源管理抽象层

统一的资源管理抽象层，封装 YooAsset 实现细节，提供类型安全的资源加载接口。

## 快速开始

### 1. 注册到 VContainer

在 `LifetimeScope.Configure` 方法中注册：

```csharp
using Change.Runtime.Asset;
using VContainer;
using YooAsset;

public class GameRootScope : LifetimeScope
{
    [SerializeField] private ResourcePackage _defaultPackage;

    protected override void Configure(IContainerBuilder builder)
    {
        // 注册资源管理器（Singleton 生命周期）
        builder.RegisterAssetManager(_defaultPackage);

        // 其他注册...
    }
}
```

`RegisterAssetManager` 是 `IContainerBuilder` 的扩展方法，将 `IAssetManager` 注册为 Singleton，底层使用 `YooAssetManager` 实现。

### 2. 注入并使用

```csharp
using Change.Runtime.Asset;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class Example
{
    private readonly IAssetManager _assetManager;

    public Example(IAssetManager assetManager)
    {
        _assetManager = assetManager;
    }

    public async UniTask LoadTextureAsync()
    {
        // 加载纹理
        using (var lease = await _assetManager.LoadAsync<Texture2D>("ui/icon"))
        {
            var texture = lease.Asset;
            // 使用纹理...
        } // Dispose 时自动释放资源
    }

    public async UniTask LoadAndInstantiateAsync()
    {
        // 加载并实例化 GameObject
        using (var lease = await _assetManager.LoadAndInstantiateAsync("prefabs/player"))
        {
            var instance = lease.Instance;
            // 使用实例...
        } // Dispose 时自动销毁实例并释放资源
    }
}
```

## API 参考

### IAssetManager

资源管理器接口，提供统一的异步资源加载能力。

#### LoadAsync<T>(string location, IProgress<float> progress, CancellationToken cancellationToken)

从默认包异步加载资源。

| 参数 | 类型 | 说明 |
|------|------|------|
| `location` | `string` | 资源路径（YooAsset 地址） |
| `progress` | `IProgress<float>` | 可选的进度回调（0.0 - 1.0） |
| `cancellationToken` | `CancellationToken` | 可选的取消令牌 |

- **返回**: `UniTask<AssetLease<T>>` 资源的租约句柄
- **类型约束**: `T : UnityEngine.Object`
- **异常**: 加载失败时抛出 `InvalidOperationException`，取消时抛出 `OperationCanceledException`

#### LoadAsync<T>(string packageName, string location, IProgress<float> progress, CancellationToken cancellationToken)

从指定包加载资源。

| 参数 | 类型 | 说明 |
|------|------|------|
| `packageName` | `string` | YooAsset 资源包名称 |
| `location` | `string` | 资源路径（YooAsset 地址） |
| `progress` | `IProgress<float>` | 可选的进度回调（0.0 - 1.0） |
| `cancellationToken` | `CancellationToken` | 可选的取消令牌 |

- **返回**: `UniTask<AssetLease<T>>`
- **异常**: 包名无效时抛出 `ArgumentException`，包未初始化时抛出 `InvalidOperationException`

#### LoadAndInstantiateAsync(string location, IProgress<float> progress, CancellationToken cancellationToken)

从默认包加载并实例化 GameObject。

| 参数 | 类型 | 说明 |
|------|------|------|
| `location` | `string` | 预制体资源路径（YooAsset 地址） |
| `progress` | `IProgress<float>` | 可选的进度回调（0.0 - 1.0） |
| `cancellationToken` | `CancellationToken` | 可选的取消令牌 |

- **返回**: `UniTask<GameObjectLease>` 实例句柄
- **说明**: 先加载预制体资源，再调用 `Object.Instantiate` 实例化

#### LoadAndInstantiateAsync(string packageName, string location, IProgress<float> progress, CancellationToken cancellationToken)

从指定包加载并实例化 GameObject。

| 参数 | 类型 | 说明 |
|------|------|------|
| `packageName` | `string` | YooAsset 资源包名称 |
| `location` | `string` | 预制体资源路径（YooAsset 地址） |
| `progress` | `IProgress<float>` | 可选的进度回调（0.0 - 1.0） |
| `cancellationToken` | `CancellationToken` | 可选的取消令牌 |

### AssetLease\<T\>

泛型资源句柄，实现 `IDisposable`，封装 YooAsset 引用计数管理。

| 成员 | 类型 | 说明 |
|------|------|------|
| `Asset` | `T` | 获取已加载的资源实例（只读） |
| `Dispose()` | `void` | 释放资源引用计数，可安全重复调用 |

- **类型约束**: `T : UnityEngine.Object`
- **生命周期**: 构造函数接收资源实例和释放回调，`Dispose()` 时调用回调
- **建议**: 使用 `using` 语句确保及时释放

### GameObjectLease

GameObject 实例句柄，实现 `IDisposable`，管理实例和资源的完整生命周期。

| 成员 | 类型 | 说明 |
|------|------|------|
| `Instance` | `GameObject` | 获取实例化的 GameObject（只读） |
| `Dispose()` | `void` | 销毁 GameObject 实例并释放底层资源，可安全重复调用 |

- **生命周期**: `Dispose()` 时自动调用 `Object.Destroy`（运行时）或 `Object.DestroyImmediate`（编辑器模式），然后释放底层资源句柄
- **建议**: 使用 `using` 语句确保及时释放

### AssetContainerBuilderExtensions

VContainer 注册扩展方法。

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterAssetManager` | `static RegistrationBuilder RegisterAssetManager(this IContainerBuilder builder, ResourcePackage defaultPackage)` | 以 Singleton 生命周期注册 `IAssetManager` |

- **参数校验**: `builder` 或 `defaultPackage` 为 `null` 时抛出 `ArgumentNullException`

## 高级用法

### 进度回调

```csharp
var progress = new Progress<float>(p => Debug.Log($"Loading: {p * 100}%"));
using (var lease = await _assetManager.LoadAsync<Texture2D>("ui/icon", progress))
{
    // 使用资源...
}
```

进度通过后台轮询 `AssetHandle.Progress` 实现，从 0.0 递增到 1.0。

### 取消加载

```csharp
var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(5));

try
{
    using (var lease = await _assetManager.LoadAsync<Texture2D>(
        "ui/icon", cancellationToken: cts.Token))
    {
        // 使用资源...
    }
}
catch (OperationCanceledException)
{
    Debug.Log("Load cancelled");
}
```

### 从指定包加载

```csharp
using (var lease = await _assetManager.LoadAsync<AudioClip>(
    "DLCPackage", "audio/music"))
{
    // 使用资源...
}
```

适用于多包场景（如 DLC、扩展包等）。包必须已通过 YooAsset 初始化。

### 加载并实例化（带参数）

```csharp
using (var lease = await _assetManager.LoadAndInstantiateAsync(
    "DLCPackage", "prefabs/enemy"))
{
    var enemy = lease.Instance;
    enemy.transform.position = spawnPoint;
    // 使用实例...
}
```

## 错误处理

### 加载失败

资源路径无效、资源不存在或加载过程异常时，抛出 `InvalidOperationException`，包含详细的错误上下文（路径、资源类型、YooAsset LastError）：

```csharp
try
{
    using (var lease = await _assetManager.LoadAsync<Texture2D>("invalid/path"))
    {
        // 使用资源...
    }
}
catch (InvalidOperationException ex)
{
    Debug.LogError($"Asset load failed: {ex.Message}");
}
```

### 取消加载

用户主动取消时抛出 `OperationCanceledException`：

```csharp
try
{
    using (var lease = await _assetManager.LoadAsync<Texture2D>(
        "ui/icon", cancellationToken: cancellationToken))
    {
        // 使用资源...
    }
}
catch (OperationCanceledException)
{
    // 正常的取消流程，不需要错误处理
}
```

### 包不存在

使用 `LoadAsync<T>(packageName, ...)` 时，如果包未初始化，抛出 `InvalidOperationException`：

```csharp
try
{
    using (var lease = await _assetManager.LoadAsync<Texture2D>(
        "NonExistentPackage", "ui/icon"))
    {
        // ...
    }
}
catch (InvalidOperationException ex)
{
    Debug.LogError($"Package error: {ex.Message}");
}
```

## 常见问题

### Q: 如何更换默认包？

在 VContainer 注册时传入不同的 `ResourcePackage` 实例：

```csharp
builder.RegisterAssetManager(myOtherPackage);
```

### Q: 为什么进度回调需要轮询？

YooAsset 的 `AssetHandle.Progress` 是属性而非事件。抽象层通过后台任务 (`UniTaskVoid`) 轮询该属性并调用 `IProgress<T>.Report()`，对调用方透明。

### Q: 如何加载 RawFile 或其他非 UnityEngine.Object 类型？

当前版本仅支持 `UnityEngine.Object` 子类型（`Texture2D`、`AudioClip`、`GameObject`、`Material` 等）。RawFile 支持可在后续版本通过扩展方法添加。

### Q: 句柄必须 Dispose 吗？

是的。不 Dispose 会导致 YooAsset 引用计数无法递减，资源无法卸载（内存泄漏）。建议始终使用 `using` 语句或 `finally` 块确保调用。

### Q: Dispose 可以调用多次吗？

可以。`AssetLease<T>` 和 `GameObjectLease` 的 `Dispose()` 方法只执行一次释放逻辑，后续调用为无操作（no-op）。

### Q: 加载过程中 Scene 切换会怎样？

如果 Scene 切换导致资源引用丢失，建议在切换前调用 `Dispose()` 释放句柄，或使用 `CancellationToken` 取消进行中的加载。

## 与现有 UI 模块的关系

| 模块 | 定位 | 关系 |
|------|------|------|
| `IAssetManager` | 通用资源加载接口 | 本模块核心接口 |
| `YooUiAssetLoader` | UI 专用加载器 | 绑定 `WindowId`，UI 模块内部使用 |
| `UiAssetLease` | UI 资源句柄 | 专用于 UI 资源生命周期管理 |

- `IAssetManager` 和 `YooUiAssetLoader` 可以并存，各自服务于不同的使用场景
- 长期可考虑将 UI 模块的资源加载逐步迁移到 `IAssetManager`，以减少重复代码

## 架构

```
调用方 (GameScript)
    │
    ▼
IAssetManager (接口，Change.Runtime.Asset)
    │
    ▼
YooAssetManager (适配器实现)
    │
    ▼
YooAsset ResourcePackage / AssetHandle
```

- **抽象层不负责 YooAsset 初始化**，初始化逻辑由上层（如 `GameCompositionHost`）负责
- **所有公共方法为异步**，基于 UniTask，支持进度报告和取消操作
- **资源生命周期通过 RAII 句柄管理**，`using` 语句自动释放

## 许可证

内部项目，版权归公司所有。
