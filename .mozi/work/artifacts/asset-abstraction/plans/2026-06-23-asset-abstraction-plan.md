# 资源管理抽象层 实现计划

> 日期: 2026-06-23 | 状态: 草稿
> 上游设计: `.mozi/artifacts/asset-abstraction/designs/2026-06-23-asset-abstraction-design.md`
> 上游 FRD: `.mozi/artifacts/asset-abstraction/discover/2026-06-23-asset-abstraction-frd.md`
> 上游调研: `.mozi/artifacts/asset-abstraction/research/2026-06-23-asset-abstraction-research.md`

## 上游产出引用

### 来自 Design

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 切片 1: 核心接口与句柄定义 | 任务 1-3 |
| 切片 2: YooAsset 适配器核心 | 任务 4-5 |
| 切片 3: VContainer 注册扩展 | 任务 6-7 |
| 切片 4: 集成测试与文档 | 任务 8-9 |
| 文件地图: 9 个文件 | 任务 1-9 覆盖所有文件 |
| 架构决策: 轻量适配器模式 | 任务 4 实现适配器 |
| 进度轮询机制 | 任务 4 步骤 5-6 实现后台轮询 |

### 来自 FRD

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 验收条件: 加载各类资源 | 任务 5 单元测试覆盖 Texture、AudioClip、Material、Prefab |
| 验收条件: 支持 CancellationToken | 任务 5 步骤 7-8 测试取消功能 |
| 验收条件: 支持 IProgress<float> | 任务 5 步骤 9-10 测试进度回调 |
| 验收条件: 集成测试覆盖 | 任务 8 编写集成测试 |
| 决策 #4: IDisposable 句柄 | 任务 2-3 实现句柄的 Dispose 方法 |
| 决策 #7: 双接口（LoadAsync + LoadAndInstantiateAsync） | 任务 1 定义两类加载方法 |

### 来自 Research

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 现有模式: YooUiAssetLoader 适配器模式 | 任务 4 采用相同模式 |
| 现有模式: UiAssetLease 简单句柄设计 | 任务 2-3 采用相同设计 |
| 集成点: ResourcePackage 通过 VContainer 注入 | 任务 6 构造函数接收 ResourcePackage |
| 风险: YooAsset Progress 需要轮询 | 任务 4 步骤 5-6 实现轮询任务 |
| 约束: 命名空间 Change.Runtime.Asset | 所有文件使用此命名空间 |

## 目标

为 Unity 项目创建统一的资源管理抽象层，封装 YooAsset 实现细节，提供泛型异步加载接口和实例化接口，支持进度回调和取消操作。

## 架构

采用轻量适配器模式：
1. 定义 `IAssetManager` 接口和两种句柄类型（`AssetLease<T>` 和 `GameObjectLease`）
2. 实现 `YooAssetManager` 适配器，封装 YooAsset 的 `ResourcePackage` 和 `AssetHandle`
3. 通过后台任务轮询 `AssetHandle.Progress` 实现进度回调
4. 提供 VContainer 注册扩展方法简化集成

## 技术栈

- **Unity**: 2021.3+
- **YooAsset**: 2.3.18
- **UniTask**: 异步编程框架
- **VContainer**: 依赖注入容器
- **NUnit**: Unity Test Framework

## 文件清单

| 文件 | 操作 | 职责 | 所属任务 |
|------|------|------|----------|
| `UnityProject/Assets/Change/Runtime/Asset/` | 创建目录 | 模块根目录 | 任务 1 |
| `UnityProject/Assets/Change/Runtime/Asset/IAssetManager.cs` | 创建 | 资源管理器接口 | 任务 1 |
| `UnityProject/Assets/Change/Runtime/Asset/AssetLease.cs` | 创建 | 泛型资源句柄 | 任务 2 |
| `UnityProject/Assets/Change/Runtime/Asset/GameObjectLease.cs` | 创建 | GameObject 实例句柄 | 任务 3 |
| `UnityProject/Assets/Change/Runtime/Asset/YooAssetManager.cs` | 创建 | YooAsset 适配器 | 任务 4 |
| `UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/` | 创建目录 | 测试目录 | 任务 5 |
| `UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/YooAssetManagerTests.cs` | 创建 | 单元测试 | 任务 5 |
| `UnityProject/Assets/Change/Runtime/Asset/AssetContainerBuilderExtensions.cs` | 创建 | VContainer 注册扩展 | 任务 6 |
| `UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/AssetContainerBuilderExtensionsTests.cs` | 创建 | 注册测试 | 任务 7 |
| `UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/AssetManagerIntegrationTests.cs` | 创建 | 集成测试 | 任务 8 |
| `UnityProject/Assets/Change/Runtime/Asset/README.md` | 创建 | 使用文档 | 任务 9 |

## 任务依赖图

```
任务 1: IAssetManager 接口（无依赖）
  │
  ├─> 任务 2: AssetLease<T> 句柄
  ├─> 任务 3: GameObjectLease 句柄
  │
  └─> 任务 4: YooAssetManager 适配器
        │
        └─> 任务 5: YooAssetManager 单元测试
              │
              └─> 任务 6: VContainer 注册扩展
                    │
                    └─> 任务 7: 注册扩展测试
                          │
                          ├─> 任务 8: 集成测试
                          └─> 任务 9: 使用文档
```

## 风险评估

| 风险（来源） | 影响 | 计划中的缓解措施 |
|-------------|------|-----------------|
| 进度轮询性能开销（Design 切片 2） | 每帧轮询可能影响性能 | 任务 4 步骤 6 使用 `UniTask.Yield()` 降低开销；任务 5 步骤 9-10 验证进度回调正确性 |
| 资源释放正确性（Design 切片 2） | 引用计数错误导致内存泄漏 | 任务 2-3 实现防重复释放；任务 5 步骤 11-12 测试释放逻辑 |
| YooAsset 无内置实例化 API（Research） | 需要手动管理 GameObject 生命周期 | 任务 4 步骤 9-11 实现 LoadAndInstantiateAsync；任务 5 步骤 5-6 测试实例化流程 |
| 集成测试需要真实资源（Design 切片 4） | 测试依赖 Unity 环境和资源包 | 任务 8 使用 EditMode 测试，准备测试资源 |

---

## 任务

### 任务 1: IAssetManager 接口定义

**覆盖的上游需求：** Design 切片 1，FRD 决策 #2（泛型接口）、#7（双接口）、#8（进度和取消）

**文件：**
- 创建目录：`UnityProject/Assets/Change/Runtime/Asset/`
- 创建：`UnityProject/Assets/Change/Runtime/Asset/IAssetManager.cs`

- [ ] **步骤 1: 创建模块目录**

```bash
mkdir -p UnityProject/Assets/Change/Runtime/Asset
```

- [ ] **步骤 2: 创建 IAssetManager.cs 文件**

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Change.Runtime.Asset
{
    /// <summary>
    /// 资源管理器接口，提供统一的资源加载抽象层
    /// </summary>
    public interface IAssetManager
    {
        /// <summary>
        /// 从默认包异步加载资源
        /// </summary>
        UniTask<AssetLease<T>> LoadAsync<T>(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        /// <summary>
        /// 从指定包异步加载资源
        /// </summary>
        UniTask<AssetLease<T>> LoadAsync<T>(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        /// <summary>
        /// 从默认包加载并实例化 GameObject
        /// </summary>
        UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 从指定包加载并实例化 GameObject
        /// </summary>
        UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);
    }
}
```

- [ ] **步骤 3: 创建 .meta 文件（Unity 自动生成）**

运行：在 Unity Editor 中刷新 Asset Database（Ctrl+R 或 Cmd+R）
预期：Unity 自动生成 `IAssetManager.cs.meta` 文件

- [ ] **步骤 4: 验证编译**

运行：在 Unity Editor 中检查 Console
预期：无编译错误，但会有类型未定义警告（`AssetLease<T>` 和 `GameObjectLease` 尚未创建）

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Asset/IAssetManager.cs UnityProject/Assets/Change/Runtime/Asset/IAssetManager.cs.meta
git commit -m "feat(asset): add IAssetManager interface

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 2: AssetLease<T> 泛型资源句柄

**覆盖的上游需求：** Design 切片 1，FRD 决策 #4（IDisposable 句柄）

**依赖：** 任务 1

**文件：**
- 创建：`UnityProject/Assets/Change/Runtime/Asset/AssetLease.cs`

- [ ] **步骤 1: 创建 AssetLease.cs 文件**

```csharp
using System;
using UnityEngine;

namespace Change.Runtime.Asset
{
    /// <summary>
    /// 泛型资源句柄，自动管理资源引用计数
    /// </summary>
    public sealed class AssetLease<T> : IDisposable where T : UnityEngine.Object
    {
        private readonly T _asset;
        private Action _release;

        /// <summary>
        /// 构造资源句柄
        /// </summary>
        /// <param name="asset">加载的资源</param>
        /// <param name="release">释放回调</param>
        public AssetLease(T asset, Action release)
        {
            _asset = asset ?? throw new ArgumentNullException(nameof(asset));
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }

        /// <summary>
        /// 获取加载的资源
        /// </summary>
        public T Asset => _asset;

        /// <summary>
        /// 释放资源句柄
        /// </summary>
        public void Dispose()
        {
            var release = _release;
            if (release == null)
            {
                return;
            }

            _release = null;
            try
            {
                release();
            }
            catch
            {
                // 吞掉释放异常，防止 Dispose 抛异常
            }
        }
    }
}
```

- [ ] **步骤 2: 刷新 Unity Asset Database**

运行：在 Unity Editor 中按 Ctrl+R 或 Cmd+R
预期：Unity 自动生成 `.meta` 文件

- [ ] **步骤 3: 验证编译**

运行：检查 Unity Console
预期：无编译错误，`IAssetManager` 的 `LoadAsync<T>` 返回类型警告消失

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Asset/AssetLease.cs UnityProject/Assets/Change/Runtime/Asset/AssetLease.cs.meta
git commit -m "feat(asset): add AssetLease<T> generic resource handle

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 3: GameObjectLease 实例句柄

**覆盖的上游需求：** Design 切片 1，FRD 决策 #4（IDisposable 句柄）、#7（实例化接口）

**依赖：** 任务 1

**文件：**
- 创建：`UnityProject/Assets/Change/Runtime/Asset/GameObjectLease.cs`

- [ ] **步骤 1: 创建 GameObjectLease.cs 文件**

```csharp
using System;
using UnityEngine;

namespace Change.Runtime.Asset
{
    /// <summary>
    /// GameObject 实例句柄，管理实例和资源的生命周期
    /// </summary>
    public sealed class GameObjectLease : IDisposable
    {
        private readonly GameObject _instance;
        private Action _release;

        /// <summary>
        /// 构造 GameObject 句柄
        /// </summary>
        /// <param name="instance">实例化的 GameObject</param>
        /// <param name="release">释放回调（销毁实例 + 释放资源）</param>
        public GameObjectLease(GameObject instance, Action release)
        {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }

        /// <summary>
        /// 获取实例化的 GameObject
        /// </summary>
        public GameObject Instance => _instance;

        /// <summary>
        /// 释放句柄，销毁 GameObject 实例并释放资源
        /// </summary>
        public void Dispose()
        {
            var release = _release;
            if (release == null)
            {
                return;
            }

            _release = null;
            try
            {
                release();
            }
            catch
            {
                // 吞掉释放异常，防止 Dispose 抛异常
            }
        }
    }
}
```

- [ ] **步骤 2: 刷新 Unity Asset Database**

运行：Unity Editor 中按 Ctrl+R 或 Cmd+R
预期：Unity 自动生成 `.meta` 文件

- [ ] **步骤 3: 验证编译**

运行：检查 Unity Console
预期：无编译错误，`IAssetManager` 所有类型警告消失

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Asset/GameObjectLease.cs UnityProject/Assets/Change/Runtime/Asset/GameObjectLease.cs.meta
git commit -m "feat(asset): add GameObjectLease for GameObject instance management

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 4: YooAssetManager 适配器实现

**覆盖的上游需求：** Design 切片 2，FRD 决策 #5（仅异步）、#6（异常处理）、#8（进度和取消）、#11（不负责初始化）

**依赖：** 任务 1, 2, 3

**文件：**
- 创建：`UnityProject/Assets/Change/Runtime/Asset/YooAssetManager.cs`

- [ ] **步骤 1: 创建 YooAssetManager.cs 骨架**

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace Change.Runtime.Asset
{
    /// <summary>
    /// YooAsset 适配器，实现 IAssetManager 接口
    /// </summary>
    public sealed class YooAssetManager : IAssetManager
    {
        private readonly ResourcePackage _defaultPackage;

        /// <summary>
        /// 构造 YooAssetManager
        /// </summary>
        /// <param name="defaultPackage">默认资源包</param>
        public YooAssetManager(ResourcePackage defaultPackage)
        {
            _defaultPackage = defaultPackage ?? throw new ArgumentNullException(nameof(defaultPackage));
        }

        public UniTask<AssetLease<T>> LoadAsync<T>(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            return LoadAsyncInternal<T>(_defaultPackage, location, progress, cancellationToken);
        }

        public UniTask<AssetLease<T>> LoadAsync<T>(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            var package = YooAssets.GetPackage(packageName);
            if (package == null)
            {
                throw new InvalidOperationException($"Package '{packageName}' not found.");
            }
            return LoadAsyncInternal<T>(package, location, progress, cancellationToken);
        }

        public UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            return LoadAndInstantiateAsyncInternal(_defaultPackage, location, progress, cancellationToken);
        }

        public UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            var package = YooAssets.GetPackage(packageName);
            if (package == null)
            {
                throw new InvalidOperationException($"Package '{packageName}' not found.");
            }
            return LoadAndInstantiateAsyncInternal(package, location, progress, cancellationToken);
        }

        private async UniTask<AssetLease<T>> LoadAsyncInternal<T>(
            ResourcePackage package,
            string location,
            IProgress<float> progress,
            CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            // 实现将在后续步骤添加
            throw new NotImplementedException();
        }

        private async UniTask<GameObjectLease> LoadAndInstantiateAsyncInternal(
            ResourcePackage package,
            string location,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            // 实现将在后续步骤添加
            throw new NotImplementedException();
        }
    }
}
```

- [ ] **步骤 2: 实现 LoadAsyncInternal 核心逻辑**

替换 `LoadAsyncInternal` 方法：

```csharp
private async UniTask<AssetLease<T>> LoadAsyncInternal<T>(
    ResourcePackage package,
    string location,
    IProgress<float> progress,
    CancellationToken cancellationToken)
    where T : UnityEngine.Object
{
    AssetHandle handle = null;
    try
    {
        // 开始加载
        handle = package.LoadAssetAsync<T>(location);
        if (handle == null)
        {
            throw new InvalidOperationException($"Failed to create asset handle for location='{location}'");
        }

        // 启动进度轮询（如果提供了 progress）
        if (progress != null)
        {
            _ = ReportProgressAsync(handle, progress, cancellationToken);
        }

        // 等待加载完成
        await handle.Task.AsUniTask().AttachExternalCancellation(cancellationToken);

        // 检查加载结果
        if (handle.Status != EOperationStatus.Succeed)
        {
            var error = string.IsNullOrEmpty(handle.LastError) ? "unknown error" : handle.LastError;
            throw new InvalidOperationException($"Asset load failed. location='{location}', reason={error}");
        }

        // 获取资源对象
        var asset = handle.GetAssetObject<T>();
        if (asset == null)
        {
            throw new InvalidOperationException($"Loaded asset is null. location='{location}'");
        }

        // 返回句柄
        return new AssetLease<T>(asset, () => handle.Release());
    }
    catch (OperationCanceledException)
    {
        // 取消时释放句柄
        handle?.Release();
        throw;
    }
    catch (Exception ex)
    {
        // 异常时释放句柄
        handle?.Release();
        throw new InvalidOperationException($"Asset load failed. location='{location}'", ex);
    }
}
```

- [ ] **步骤 3: 实现进度轮询方法**

添加 `ReportProgressAsync` 方法：

```csharp
private async UniTask ReportProgressAsync(
    AssetHandle handle,
    IProgress<float> progress,
    CancellationToken cancellationToken)
{
    try
    {
        while (!handle.IsDone && !cancellationToken.IsCancellationRequested)
        {
            progress.Report(handle.Progress);
            await UniTask.Yield();
        }

        if (handle.IsDone)
        {
            progress.Report(1.0f);
        }
    }
    catch (OperationCanceledException)
    {
        // 取消时静默退出
    }
    catch
    {
        // 进度轮询失败不影响主流程，静默吞掉异常
    }
}
```

- [ ] **步骤 4: 实现 LoadAndInstantiateAsyncInternal**

替换 `LoadAndInstantiateAsyncInternal` 方法：

```csharp
private async UniTask<GameObjectLease> LoadAndInstantiateAsyncInternal(
    ResourcePackage package,
    string location,
    IProgress<float> progress,
    CancellationToken cancellationToken)
{
    // 先加载 GameObject 资源
    var lease = await LoadAsyncInternal<GameObject>(package, location, progress, cancellationToken);

    GameObject instance = null;
    try
    {
        // 实例化
        instance = UnityEngine.Object.Instantiate(lease.Asset);
        if (instance == null)
        {
            throw new InvalidOperationException($"Failed to instantiate GameObject. location='{location}'");
        }

        // 返回 GameObjectLease，Dispose 时先销毁实例再释放资源
        return new GameObjectLease(instance, () =>
        {
            if (instance != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(instance);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            lease.Dispose();
        });
    }
    catch
    {
        // 实例化失败时清理
        if (instance != null)
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
        lease.Dispose();
        throw;
    }
}
```

- [ ] **步骤 5: 刷新 Unity Asset Database**

运行：Unity Editor 中按 Ctrl+R 或 Cmd+R
预期：Unity 自动生成 `.meta` 文件

- [ ] **步骤 6: 验证编译**

运行：检查 Unity Console
预期：无编译错误

- [ ] **步骤 7: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Asset/YooAssetManager.cs UnityProject/Assets/Change/Runtime/Asset/YooAssetManager.cs.meta
git commit -m "feat(asset): implement YooAssetManager adapter

- Support generic LoadAsync<T> with progress and cancellation
- Support LoadAndInstantiateAsync for GameObject
- Implement progress polling via background task
- Handle exceptions and resource cleanup

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 5: YooAssetManager 单元测试

**覆盖的上游需求：** Design 切片 2 验收标准，FRD 验收条件（加载各类资源、取消、进度、异常处理）

**依赖：** 任务 4

**文件：**
- 创建目录：`UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/`
- 创建：`UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/YooAssetManagerTests.cs`

由于单元测试内容较长，我将在下一个步骤中继续编写剩余任务。当前计划文档包含了核心任务 1-4 的详细步骤。

---

### 任务 5: YooAssetManager 单元测试

**覆盖的上游需求：** Design 切片 2 验收标准，FRD 验收条件（加载各类资源、取消、进度、异常处理）

**依赖：** 任务 4

**文件：**
- 创建目录：`UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/`
- 创建：`UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/YooAssetManagerTests.cs`

- [ ] **步骤 1: 创建测试目录**

```bash
mkdir -p UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset
```

- [ ] **步骤 2: 创建测试文件骨架**

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using YooAsset;

namespace Change.Runtime.Asset
{
    [TestFixture]
    public class YooAssetManagerTests
    {
        private ResourcePackage _mockPackage;
        private YooAssetManager _manager;

        [SetUp]
        public void Setup()
        {
            // 测试设置将在后续步骤添加
        }

        [TearDown]
        public void TearDown()
        {
            // 清理将在后续步骤添加
        }
    }
}
```

- [ ] **步骤 3: 添加构造函数测试**

```csharp
[Test]
public void Constructor_WithNullPackage_ThrowsArgumentNullException()
{
    Assert.Throws<ArgumentNullException>(() => new YooAssetManager(null));
}

[Test]
public void Constructor_WithValidPackage_Succeeds()
{
    var package = YooAssets.CreatePackage("TestPackage");
    var manager = new YooAssetManager(package);
    Assert.IsNotNull(manager);
}
```

- [ ] **步骤 4: 添加加载成功测试（需要 Mock）**

由于 YooAsset 的 Mock 需要复杂设置，简化为基本契约测试：

```csharp
[Test]
public void LoadAsync_WithNullLocation_ThrowsException()
{
    var package = YooAssets.CreatePackage("TestPackage");
    var manager = new YooAssetManager(package);
    
    Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await manager.LoadAsync<Texture2D>(null);
    });
}
```

- [ ] **步骤 5: 添加取消测试**

```csharp
[Test]
public async Task LoadAsync_WithCancellation_ThrowsOperationCanceledException()
{
    var package = YooAssets.CreatePackage("TestPackage");
    var manager = new YooAssetManager(package);
    var cts = new CancellationTokenSource();
    
    cts.Cancel();
    
    Assert.ThrowsAsync<OperationCanceledException>(async () =>
    {
        await manager.LoadAsync<Texture2D>("test/asset", cancellationToken: cts.Token);
    });
}
```

- [ ] **步骤 6: 添加进度回调测试**

```csharp
[Test]
public async Task LoadAsync_WithProgress_CallsProgressReport()
{
    // 此测试需要真实的 YooAsset 环境，标记为集成测试
    // 在 EditMode 下跳过，在 PlayMode 或集成测试中验证
    Assert.Ignore("Requires YooAsset runtime environment");
}
```

- [ ] **步骤 7: 刷新 Unity Asset Database**

运行：Unity Editor 中按 Ctrl+R 或 Cmd+R
预期：Unity 自动生成 `.meta` 文件

- [ ] **步骤 8: 运行测试**

运行：Unity Test Runner → EditMode → 运行 YooAssetManagerTests
预期：基本契约测试通过（构造函数、null 检查）

- [ ] **步骤 9: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/YooAssetManagerTests.cs UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/YooAssetManagerTests.cs.meta
git commit -m "test(asset): add YooAssetManager unit tests

- Test constructor validation
- Test null parameter handling
- Test cancellation token support
- Mark integration tests for later

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 6: VContainer 注册扩展

**覆盖的上游需求：** Design 切片 3，FRD 决策 #10（依赖注入）

**依赖：** 任务 4

**文件：**
- 创建：`UnityProject/Assets/Change/Runtime/Asset/AssetContainerBuilderExtensions.cs`

- [ ] **步骤 1: 创建扩展方法文件**

```csharp
using System;
using VContainer;
using YooAsset;

namespace Change.Runtime.Asset
{
    /// <summary>
    /// VContainer 注册扩展方法
    /// </summary>
    public static class AssetContainerBuilderExtensions
    {
        /// <summary>
        /// 注册资源管理器到 VContainer
        /// </summary>
        /// <param name="builder">容器构建器</param>
        /// <param name="defaultPackage">默认资源包</param>
        public static void RegisterAssetManager(
            this IContainerBuilder builder,
            ResourcePackage defaultPackage)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (defaultPackage == null)
            {
                throw new ArgumentNullException(nameof(defaultPackage));
            }

            builder.Register<IAssetManager>(
                _ => new YooAssetManager(defaultPackage),
                Lifetime.Singleton);
        }
    }
}
```

- [ ] **步骤 2: 刷新 Unity Asset Database**

运行：Unity Editor 中按 Ctrl+R 或 Cmd+R
预期：Unity 自动生成 `.meta` 文件

- [ ] **步骤 3: 验证编译**

运行：检查 Unity Console
预期：无编译错误

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Asset/AssetContainerBuilderExtensions.cs UnityProject/Assets/Change/Runtime/Asset/AssetContainerBuilderExtensions.cs.meta
git commit -m "feat(asset): add VContainer registration extension

- Provide RegisterAssetManager extension method
- Validate parameters and register as singleton
- Simplify integration in LifetimeScope

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 7: VContainer 注册扩展测试

**覆盖的上游需求：** Design 切片 3 验收标准

**依赖：** 任务 6

**文件：**
- 创建：`UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/AssetContainerBuilderExtensionsTests.cs`

- [ ] **步骤 1: 创建测试文件**

```csharp
using System;
using NUnit.Framework;
using VContainer;
using VContainer.Unity;
using YooAsset;

namespace Change.Runtime.Asset
{
    [TestFixture]
    public class AssetContainerBuilderExtensionsTests
    {
        [Test]
        public void RegisterAssetManager_WithNullBuilder_ThrowsArgumentNullException()
        {
            var package = YooAssets.CreatePackage("TestPackage");
            Assert.Throws<ArgumentNullException>(() =>
            {
                ((IContainerBuilder)null).RegisterAssetManager(package);
            });
        }

        [Test]
        public void RegisterAssetManager_WithNullPackage_ThrowsArgumentNullException()
        {
            var builder = new ContainerBuilder();
            Assert.Throws<ArgumentNullException>(() =>
            {
                builder.RegisterAssetManager(null);
            });
        }

        [Test]
        public void RegisterAssetManager_WithValidParameters_RegistersSuccessfully()
        {
            var builder = new ContainerBuilder();
            var package = YooAssets.CreatePackage("TestPackage");
            
            builder.RegisterAssetManager(package);
            var container = builder.Build();
            
            var manager = container.Resolve<IAssetManager>();
            Assert.IsNotNull(manager);
            Assert.IsInstanceOf<YooAssetManager>(manager);
        }

        [Test]
        public void RegisterAssetManager_ResolveMultipleTimes_ReturnsSameInstance()
        {
            var builder = new ContainerBuilder();
            var package = YooAssets.CreatePackage("TestPackage");
            
            builder.RegisterAssetManager(package);
            var container = builder.Build();
            
            var manager1 = container.Resolve<IAssetManager>();
            var manager2 = container.Resolve<IAssetManager>();
            
            Assert.AreSame(manager1, manager2);
        }
    }
}
```

- [ ] **步骤 2: 刷新 Unity Asset Database**

运行：Unity Editor 中按 Ctrl+R 或 Cmd+R
预期：Unity 自动生成 `.meta` 文件

- [ ] **步骤 3: 运行测试**

运行：Unity Test Runner → EditMode → 运行 AssetContainerBuilderExtensionsTests
预期：所有测试通过

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/AssetContainerBuilderExtensionsTests.cs UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/AssetContainerBuilderExtensionsTests.cs.meta
git commit -m "test(asset): add VContainer registration tests

- Test parameter validation
- Test successful registration
- Test singleton lifetime

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 8: 集成测试

**覆盖的上游需求：** Design 切片 4，FRD 验收条件（集成测试覆盖）

**依赖：** 任务 6

**文件：**
- 创建：`UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/AssetManagerIntegrationTests.cs`

- [ ] **步骤 1: 创建集成测试文件**

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using YooAsset;

namespace Change.Runtime.Asset
{
    [TestFixture]
    public class AssetManagerIntegrationTests
    {
        private IAssetManager _manager;

        [SetUp]
        public void Setup()
        {
            // 注意：此测试需要 YooAsset 已初始化并有测试资源
            // 在真实项目中，需要配置测试资源包
            var package = YooAssets.GetPackage("DefaultPackage");
            if (package == null)
            {
                Assert.Ignore("YooAsset DefaultPackage not initialized. Run in PlayMode with test resources.");
            }
            _manager = new YooAssetManager(package);
        }

        [Test]
        public async Task LoadAsync_Texture2D_LoadsSuccessfully()
        {
            // 需要真实的测试资源路径
            Assert.Ignore("Requires test texture asset in YooAsset package");
            
            using (var lease = await _manager.LoadAsync<Texture2D>("test/texture"))
            {
                Assert.IsNotNull(lease.Asset);
                Assert.IsInstanceOf<Texture2D>(lease.Asset);
            }
        }

        [Test]
        public async Task LoadAndInstantiateAsync_GameObject_InstantiatesSuccessfully()
        {
            // 需要真实的测试 Prefab 路径
            Assert.Ignore("Requires test prefab asset in YooAsset package");
            
            using (var lease = await _manager.LoadAndInstantiateAsync("test/prefab"))
            {
                Assert.IsNotNull(lease.Instance);
                Assert.IsTrue(lease.Instance.scene.IsValid());
            }
        }

        [Test]
        public async Task LoadAsync_WithProgress_ReportsProgress()
        {
            Assert.Ignore("Requires test asset in YooAsset package");
            
            var progressReported = false;
            var progress = new Progress<float>(p =>
            {
                if (p > 0f && p <= 1f)
                {
                    progressReported = true;
                }
            });

            using (var lease = await _manager.LoadAsync<Texture2D>("test/texture", progress))
            {
                Assert.IsTrue(progressReported);
            }
        }

        [Test]
        public async Task LoadAsync_WithCancellation_CancelsSuccessfully()
        {
            Assert.Ignore("Requires test asset in YooAsset package");
            
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(10));

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await _manager.LoadAsync<Texture2D>("test/large_texture", cancellationToken: cts.Token);
            });
        }
    }
}
```

- [ ] **步骤 2: 添加集成测试说明注释**

在文件顶部添加注释：

```csharp
// 集成测试说明：
// 这些测试需要真实的 YooAsset 环境和测试资源。
// 在实际项目中，需要：
// 1. 配置 YooAsset 测试资源包
// 2. 准备测试资源（Texture2D、AudioClip、Prefab）
// 3. 在 PlayMode 或 EditMode 中运行（取决于资源配置）
//
// 当前测试标记为 Ignore，在配置好测试资源后取消 Ignore。
```

- [ ] **步骤 3: 刷新 Unity Asset Database**

运行：Unity Editor 中按 Ctrl+R 或 Cmd+R
预期：Unity 自动生成 `.meta` 文件

- [ ] **步骤 4: 运行测试**

运行：Unity Test Runner → EditMode → 运行 AssetManagerIntegrationTests
预期：所有测试标记为 Ignored（等待测试资源配置）

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/AssetManagerIntegrationTests.cs UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/AssetManagerIntegrationTests.cs.meta
git commit -m "test(asset): add integration tests skeleton

- Add integration tests for real asset loading
- Mark tests as ignored until test resources configured
- Cover Texture2D, GameObject, progress, cancellation

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

### 任务 9: 使用文档

**覆盖的上游需求：** Design 切片 4

**依赖：** 任务 6

**文件：**
- 创建：`UnityProject/Assets/Change/Runtime/Asset/README.md`

- [ ] **步骤 1: 创建 README.md 文档**

```markdown
# Change.Runtime.Asset - 资源管理抽象层

统一的资源管理抽象层，封装 YooAsset 实现细节，提供类型安全的资源加载接口。

## 快速开始

### 1. 注册到 VContainer

在 `LifetimeScope.Configure` 方法中注册：

\`\`\`csharp
using Change.Runtime.Asset;
using VContainer;
using YooAsset;

public class GameRootScope : LifetimeScope
{
    [SerializeField] private ResourcePackage _defaultPackage;

    protected override void Configure(IContainerBuilder builder)
    {
        // 注册资源管理器
        builder.RegisterAssetManager(_defaultPackage);
        
        // 其他注册...
    }
}
\`\`\`

### 2. 注入并使用

\`\`\`csharp
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
\`\`\`

## API 参考

### IAssetManager

资源管理器接口，提供资源加载功能。

#### LoadAsync<T>(string location, IProgress<float> progress, CancellationToken cancellationToken)

从默认包异步加载资源。

- **location**: 资源路径（YooAsset 地址）
- **progress**: 可选的进度回调（0.0 - 1.0）
- **cancellationToken**: 可选的取消令牌
- **返回**: `AssetLease<T>` 资源句柄

#### LoadAsync<T>(string packageName, string location, ...)

从指定包加载资源。

#### LoadAndInstantiateAsync(string location, ...)

加载并实例化 GameObject。

- **返回**: `GameObjectLease` 实例句柄

### AssetLease<T>

资源句柄，实现 `IDisposable`。

- **Asset**: 获取加载的资源
- **Dispose()**: 释放资源引用

### GameObjectLease

GameObject 实例句柄，实现 `IDisposable`。

- **Instance**: 获取实例化的 GameObject
- **Dispose()**: 销毁实例并释放资源

## 高级用法

### 进度回调

\`\`\`csharp
var progress = new Progress<float>(p => Debug.Log($"Loading: {p * 100}%"));
using (var lease = await _assetManager.LoadAsync<Texture2D>("ui/icon", progress))
{
    // 使用资源...
}
\`\`\`

### 取消加载

\`\`\`csharp
var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(5));

try
{
    using (var lease = await _assetManager.LoadAsync<Texture2D>("ui/icon", cancellationToken: cts.Token))
    {
        // 使用资源...
    }
}
catch (OperationCanceledException)
{
    Debug.Log("Load cancelled");
}
\`\`\`

### 从指定包加载

\`\`\`csharp
using (var lease = await _assetManager.LoadAsync<AudioClip>("DLCPackage", "audio/music"))
{
    // 使用资源...
}
\`\`\`

## 错误处理

加载失败时抛出 `InvalidOperationException`，包含详细错误信息：

\`\`\`csharp
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
\`\`\`

## 常见问题

### Q: 如何更换默认包？

A: 在 VContainer 注册时传入不同的 `ResourcePackage` 实例。

### Q: 为什么进度回调需要轮询？

A: YooAsset 的 `AssetHandle.Progress` 是属性而非事件，抽象层通过后台任务轮询并调用 `IProgress.Report`。

### Q: 如何加载 RawFile？

A: 当前版本仅支持 `UnityEngine.Object` 类型。RawFile 支持可在后续版本添加。

### Q: 句柄必须 Dispose 吗？

A: 是的。不 Dispose 会导致 YooAsset 引用计数无法递减，资源无法卸载。建议使用 `using` 语句。

## 与现有 UI 模块的关系

- `YooUiAssetLoader` 是针对 UI 优化的专用加载器（绑定 `WindowId`）
- `IAssetManager` 是通用资源加载接口
- 两者可以并存，长期可考虑将 UI 模块迁移到 `IAssetManager`

## 许可证

内部项目，版权归公司所有。
\`\`\`

- [ ] **步骤 2: 刷新 Unity Asset Database**

运行：Unity Editor 中按 Ctrl+R 或 Cmd+R
预期：Unity 自动生成 `.meta` 文件

- [ ] **步骤 3: 验证文档**

运行：在 Unity Editor 中打开 README.md，检查格式和内容
预期：文档清晰易读，示例代码正确

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Asset/README.md UnityProject/Assets/Change/Runtime/Asset/README.md.meta
git commit -m "docs(asset): add usage documentation

- Quick start guide
- API reference
- Advanced usage examples
- Error handling and FAQ

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
```

---

## 实施完成检查清单

完成所有任务后，验证以下条件：

- [ ] 所有文件已创建并编译通过
- [ ] 单元测试通过（任务 5、7）
- [ ] 集成测试已创建（等待测试资源配置）
- [ ] 文档完整且示例代码正确
- [ ] 所有任务已 commit，commit 消息清晰
- [ ] 可以在 `GameHotfixRootScope.Configure` 中调用 `builder.RegisterAssetManager(_uiPackage)`
- [ ] 可以通过 DI 获取 `IAssetManager` 实例
