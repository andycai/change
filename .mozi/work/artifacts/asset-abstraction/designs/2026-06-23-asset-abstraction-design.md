# 资源管理抽象层 架构设计

> 日期: 2026-06-23 | 状态: 草稿
> FRD: `.mozi/artifacts/asset-abstraction/discover/2026-06-23-asset-abstraction-frd.md`
> 上游: `.mozi/artifacts/asset-abstraction/research/2026-06-23-asset-abstraction-research.md`

## 上游产出引用

### 来自 FRD

| 引用内容 | 如何使用 |
|---------|----------|
| 决策 #2: 泛型接口 `LoadAsync<T>` 支持所有资源类型 | 切片 1 定义泛型接口签名 |
| 决策 #4: 返回 `IDisposable` 句柄自动管理引用计数 | 切片 1 设计 `AssetLease<T>` 和 `GameObjectLease` |
| 决策 #7: 提供 `LoadAsync<T>` 和 `LoadAndInstantiateAsync` 双接口 | 切片 1 定义两类加载方法 |
| 决策 #8: 支持 `CancellationToken` + `IProgress<float>` | 切片 2 实现进度轮询机制 |
| 决策 #10: 通过 VContainer 依赖注入 `IAssetManager` | 切片 3 实现注册扩展 |
| 验收条件: 集成测试覆盖主要场景 | 切片 4 编写集成测试 |

### 来自 Research

| 引用内容 | 如何使用 |
|---------|----------|
| 现有模式: `YooUiAssetLoader` 适配器模式 | 整体架构采用相同模式 |
| 现有模式: `UiAssetLease` 简单句柄设计 | 切片 1 设计相同风格的句柄 |
| 集成点: `ResourcePackage` 通过 VContainer 注入 | 切片 2 构造函数接收 `ResourcePackage` |
| 集成点: VContainer 注册模式 `builder.Register<T>` | 切片 3 使用相同注册 API |
| 风险: YooAsset Progress 是属性需要轮询 | 切片 2 实现后台轮询任务 |
| 风险: YooAsset 无内置实例化 API | 切片 2 手动调用 `Object.Instantiate` |
| 约束: 命名空间 `Change.Runtime.<模块名>` | 所有文件使用 `Change.Runtime.Asset` |
| 约束: 测试框架 EditModeTests + PlayModeTests | 切片 2/4 分别编写单元测试和集成测试 |

## 架构决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 整体架构风格 | 轻量适配器（方案 A） | 符合 YAGNI 原则，与现有 `YooUiAssetLoader` 模式一致，代码量小（200-300 行），工作量符合 FRD 评估（3-5 天） |
| 句柄设计 | 两种句柄类型：`AssetLease<T>` 和 `GameObjectLease` | 职责分离，`AssetLease<T>` 管理纯资源，`GameObjectLease` 管理实例化对象，避免混淆 |
| 进度回调实现 | 后台任务轮询 `AssetHandle.Progress` | YooAsset 不支持 `IProgress<T>` 接口，需手动轮询并调用 `IProgress.Report` |
| 实例化实现 | 在 `LoadAndInstantiateAsync` 内部调用 `Object.Instantiate` | YooAsset 无内置实例化 API，抽象层封装实例化逻辑和生命周期管理 |
| 异常处理 | 使用 `InvalidOperationException` | 与 `YooUiAssetLoader` 一致，包含详细上下文（路径、原因、内部异常） |
| VContainer 注册 | 扩展方法 `RegisterAssetManager` | 提供统一注册入口，简化使用方代码 |

## 文件地图

| 文件 | 所属切片 | 职责 |
|------|----------|------|
| `UnityProject/Assets/Change/Runtime/Asset/IAssetManager.cs` | 切片 1 | 资源管理器接口定义 |
| `UnityProject/Assets/Change/Runtime/Asset/AssetLease.cs` | 切片 1 | 泛型资源句柄（封装资源 + 释放回调） |
| `UnityProject/Assets/Change/Runtime/Asset/GameObjectLease.cs` | 切片 1 | GameObject 实例句柄（封装实例 + 释放回调） |
| `UnityProject/Assets/Change/Runtime/Asset/YooAssetManager.cs` | 切片 2 | YooAsset 适配器实现 |
| `UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/YooAssetManagerTests.cs` | 切片 2 | 单元测试（Mock YooAsset） |
| `UnityProject/Assets/Change/Runtime/Asset/AssetContainerBuilderExtensions.cs` | 切片 3 | VContainer 注册扩展方法 |
| `UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/AssetContainerBuilderExtensionsTests.cs` | 切片 3 | 注册测试 |
| `UnityProject/Assets/Change/Runtime/Tests/EditMode/Asset/AssetManagerIntegrationTests.cs` | 切片 4 | 集成测试（真实资源加载） |
| `UnityProject/Assets/Change/Runtime/Asset/README.md` | 切片 4 | 使用文档和示例代码 |

## 切片分解

### 切片 1: 核心接口与句柄定义

**依赖：** 无
**风险等级：** 低
**涉及文件：** `IAssetManager.cs`, `AssetLease.cs`, `GameObjectLease.cs`

**内容：** 定义资源管理器接口和两种句柄类型。接口提供泛型加载方法和实例化方法，句柄实现 `IDisposable` 自动管理 YooAsset 引用计数。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `IAssetManager.LoadAsync<T>` | `UniTask<AssetLease<T>> LoadAsync<T>(string location, IProgress<float> progress, CancellationToken cancellationToken) where T : UnityEngine.Object` | 异步加载资源，返回句柄，支持进度回调和取消 |
| `IAssetManager.LoadAsync<T>` (重载) | `UniTask<AssetLease<T>> LoadAsync<T>(string packageName, string location, IProgress<float> progress, CancellationToken cancellationToken) where T : UnityEngine.Object` | 从指定包加载资源 |
| `IAssetManager.LoadAndInstantiateAsync` | `UniTask<GameObjectLease> LoadAndInstantiateAsync(string location, IProgress<float> progress, CancellationToken cancellationToken)` | 加载并实例化 GameObject |
| `IAssetManager.LoadAndInstantiateAsync` (重载) | `UniTask<GameObjectLease> LoadAndInstantiateAsync(string packageName, string location, IProgress<float> progress, CancellationToken cancellationToken)` | 从指定包加载并实例化 |
| `AssetLease<T>.Asset` | `T Asset { get; }` | 获取加载的资源 |
| `AssetLease<T>.Dispose` | `void Dispose()` | 释放资源句柄 |
| `GameObjectLease.Instance` | `GameObject Instance { get; }` | 获取实例化的 GameObject |
| `GameObjectLease.Dispose` | `void Dispose()` | 销毁 GameObject 并释放资源 |

**数据契约：**

```csharp
namespace Change.Runtime.Asset
{
    public interface IAssetManager
    {
        UniTask<AssetLease<T>> LoadAsync<T>(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        UniTask<AssetLease<T>> LoadAsync<T>(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);

        UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);
    }

    public sealed class AssetLease<T> : IDisposable where T : UnityEngine.Object
    {
        public T Asset { get; }
        public void Dispose();
    }

    public sealed class GameObjectLease : IDisposable
    {
        public GameObject Instance { get; }
        public void Dispose();
    }
}
```

**验收标准：**

- [ ] 接口编译通过
- [ ] 接口签名符合 FRD 要求（泛型、CancellationToken、IProgress）
- [ ] 命名空间为 `Change.Runtime.Asset`
- [ ] 句柄类型实现 `IDisposable`
- [ ] 接口方法支持可选参数（`progress` 和 `cancellationToken` 有默认值）

**回归风险评估：**

- **影响范围：** 无（新增接口，不影响现有代码）
- **缓解措施：** 无需（纯接口定义）

---

### 切片 2: YooAsset 适配器核心

**依赖：** 切片 1
**风险等级：** 高
**涉及文件：** `YooAssetManager.cs`, `YooAssetManagerTests.cs`

**内容：** 实现 `IAssetManager` 接口，封装 YooAsset 的 `ResourcePackage` 和 `AssetHandle`。核心挑战是进度轮询机制和资源释放的正确性。这是最高风险切片，需要优先验证。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `YooAssetManager` 构造函数 | `YooAssetManager(ResourcePackage defaultPackage)` | 接收默认资源包，参数为 null 时抛出 `ArgumentNullException` |
| `LoadAsync<T>` 实现 | 同 `IAssetManager.LoadAsync<T>` | 调用 YooAsset API，启动进度轮询，返回 `AssetLease<T>` |
| `LoadAndInstantiateAsync` 实现 | 同 `IAssetManager.LoadAndInstantiateAsync` | 先加载资源，再实例化，返回 `GameObjectLease` |

**行为契约：**

**LoadAsync<T> 实现逻辑**：
1. 获取目标 `ResourcePackage`（默认或指定）
2. 调用 `package.LoadAssetAsync<T>(location)`，获取 `AssetHandle`
3. 如果 `progress != null`，启动后台任务轮询 `handle.Progress` 并调用 `progress.Report`
4. `await handle.Task.AsUniTask().AttachExternalCancellation(cancellationToken)`
5. 检查 `handle` 状态，失败时抛出 `InvalidOperationException`（包含 location、reason、LastError）
6. 创建 `AssetLease<T>`，构造函数传入 `handle.GetAssetObject<T>()` 和释放回调 `() => handle.Release()`
7. 返回 `AssetLease<T>`

**LoadAndInstantiateAsync 实现逻辑**：
1. 调用 `LoadAsync<GameObject>(packageName, location, progress, cancellationToken)`
2. 获得 `AssetLease<GameObject> lease`
3. 调用 `Object.Instantiate(lease.Asset)`，捕获异常并封装为 `InvalidOperationException`
4. 创建 `GameObjectLease`，传入实例和释放回调 `() => { if (instance != null) Destroy(instance); lease.Dispose(); }`
5. 返回 `GameObjectLease`

**进度轮询机制**：
```csharp
// 伪代码
if (progress != null)
{
    _ = UniTask.Run(async () =>
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
    }, cancellationToken);
}
```

**验收标准：**

- [ ] 可以加载 Texture2D、AudioClip、Material、Prefab 等各类资源
- [ ] 可以加载并实例化 GameObject
- [ ] 进度回调被正确调用（从 0.0 递增到 1.0）
- [ ] 取消令牌生效，加载中途取消抛出 `OperationCanceledException`
- [ ] 加载失败抛出 `InvalidOperationException`，包含 location 和 reason
- [ ] 句柄 Dispose 后 YooAsset 引用计数正确递减（通过测试验证）
- [ ] 单元测试覆盖率 > 80%

**回归风险评估：**

- **影响范围：** 无（新增类，不影响现有代码）
- **缓解措施：** 通过单元测试 Mock YooAsset API，验证适配器行为正确性

---

### 切片 3: VContainer 注册扩展

**依赖：** 切片 2
**风险等级：** 低
**涉及文件：** `AssetContainerBuilderExtensions.cs`, `AssetContainerBuilderExtensionsTests.cs`

**内容：** 提供 VContainer 注册扩展方法，简化在 `LifetimeScope.Configure` 中的注册代码。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `RegisterAssetManager` | `void RegisterAssetManager(this IContainerBuilder builder, ResourcePackage defaultPackage)` | 注册 `IAssetManager` 为单例，工厂方法创建 `YooAssetManager` |

**行为契约：**

**RegisterAssetManager 实现逻辑**：
1. 检查参数：`builder` 和 `defaultPackage` 不为 null，否则抛出 `ArgumentNullException`
2. 调用 `builder.Register<IAssetManager>(_ => new YooAssetManager(defaultPackage), Lifetime.Singleton)`
3. 无返回值

**验收标准：**

- [ ] 可以通过 `container.Resolve<IAssetManager>()` 获取实例
- [ ] 解析的实例是 `YooAssetManager` 类型
- [ ] 生命周期为单例（多次解析返回同一实例）
- [ ] 测试覆盖注册和解析流程
- [ ] 参数为 null 时抛出 `ArgumentNullException`

**回归风险评估：**

- **影响范围：** 无（新增扩展方法，不影响现有注册逻辑）
- **集成点：** 需要在 `GameHotfixRootScope.Configure` 中调用 `builder.RegisterAssetManager(_uiPackage)`（假设 UI 包作为默认包）

---

### 切片 4: 集成测试与文档

**依赖：** 切片 3
**风险等级：** 中
**涉及文件：** `AssetManagerIntegrationTests.cs`, `README.md`

**内容：** 编写集成测试验证完整功能，编写使用文档帮助团队快速上手。集成测试需要真实 Unity 环境和测试资源。

**接口契约：**

无（测试和文档）

**行为契约：**

**集成测试场景**：
1. **加载 Texture2D**：验证加载成功，`Asset` 不为 null，Dispose 后可再次加载
2. **加载 AudioClip**：验证加载成功，`Asset` 不为 null
3. **加载并实例化 Prefab**：验证实例创建成功，`Instance` 不为 null，Dispose 后实例被销毁
4. **取消加载操作**：中途取消，验证抛出 `OperationCanceledException`
5. **进度回调**：记录进度值，验证从 0.0 递增到 1.0
6. **加载不存在的资源**：验证抛出 `InvalidOperationException`，消息包含路径
7. **资源句柄 Dispose**：验证 Dispose 后可正常工作（不抛异常）

**文档内容**：
- 快速开始：5 行代码演示加载纹理
- 接口 API 参考：每个方法的参数和返回值说明
- VContainer 注册：在 `Configure` 中调用 `RegisterAssetManager` 的示例
- 错误处理：如何捕获和处理 `InvalidOperationException`
- 常见问题（FAQ）：进度回调为何需要轮询、如何更换默认包、如何加载 RawFile 等

**验收标准：**

- [ ] 所有集成测试通过（EditMode 或 PlayMode）
- [ ] 文档包含完整的使用示例（加载纹理、音频、实例化 Prefab）
- [ ] 文档包含 VContainer 注册示例
- [ ] 团队成员可以根据文档在 5 分钟内完成首次调用

**回归风险评估：**

- **影响范围：** 无（测试和文档不影响运行时代码）
- **注意事项：** 集成测试需要准备测试资源（Texture、AudioClip、Prefab），可能需要配置 YooAsset 的测试资源包

---

## 切片依赖图

```
切片 1：核心接口与句柄定义（无依赖）
  │
  └─> 切片 2：YooAsset 适配器核心（高风险，优先验证）
        │
        └─> 切片 3：VContainer 注册扩展
              │
              └─> 切片 4：集成测试与文档（完整用户旅程）
```

## 关键接口

### 切片 1 对外暴露（公开 API）

```csharp
namespace Change.Runtime.Asset
{
    public interface IAssetManager
    {
        UniTask<AssetLease<T>> LoadAsync<T>(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        UniTask<AssetLease<T>> LoadAsync<T>(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);

        UniTask<GameObjectLease> LoadAndInstantiateAsync(
            string packageName,
            string location,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);
    }

    public sealed class AssetLease<T> : IDisposable where T : UnityEngine.Object
    {
        public T Asset { get; }
        public void Dispose();
    }

    public sealed class GameObjectLease : IDisposable
    {
        public GameObject Instance { get; }
        public void Dispose();
    }
}
```

### 切片 2 实现（内部）

```csharp
namespace Change.Runtime.Asset
{
    public sealed class YooAssetManager : IAssetManager
    {
        public YooAssetManager(ResourcePackage defaultPackage);
        
        // IAssetManager 接口实现
    }
}
```

### 切片 3 对外暴露（注册扩展）

```csharp
namespace Change.Runtime.Asset
{
    public static class AssetContainerBuilderExtensions
    {
        public static void RegisterAssetManager(
            this IContainerBuilder builder,
            ResourcePackage defaultPackage);
    }
}
```

## 回归风险评估

| 影响范围 | 风险等级 | 缓解措施 |
|---------|---------|----------|
| 现有 YooAsset 使用点 | 无 | 新增抽象层不影响现有代码（如 `YooUiAssetLoader`），可并存 |
| VContainer 注册逻辑 | 低 | 新增扩展方法，不修改现有 `GameHotfixRootScope.Configure` 中的其他注册 |
| 资源加载性能 | 低 | 进度轮询每帧一次，开销可忽略；句柄封装无额外内存分配 |
| 测试资源准备 | 中 | 集成测试需要准备测试资源包，可能需要配置 YooAsset 打包流程 |

**整体评估**：低风险。新增功能，不修改现有代码，与现有系统隔离良好。
