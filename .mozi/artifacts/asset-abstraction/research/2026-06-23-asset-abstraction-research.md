# 资源管理抽象层 代码调研

> 日期: 2026-06-23
> 范围: YooAsset 集成、VContainer 注册模式、测试框架
> 上游: `.mozi/artifacts/asset-abstraction/discover/2026-06-23-asset-abstraction-frd.md`
> 调研深度: 中等

## Summary

项目中 YooAsset 的 `ResourcePackage` 通过 VContainer 依赖注入传递，使用 `RegisterInstance` 注册序列化字段实例。现有 `YooUiAssetLoader` 采用适配器模式封装 YooAsset，返回 `UiAssetLease` 句柄（简单的 IDisposable 包装 + 回调）。项目命名空间遵循 `Change.Runtime.<模块名>` 约定，每个模块独立目录。VContainer 通过 `LifetimeScope` + `IHotfixGameInstaller` 接口实现分层注册。测试框架完善，支持 EditMode 和 PlayMode 测试。YooAsset 提供 `AssetHandle`（继承 `HandleBase`），原生支持 `Progress` 属性、`Task` 属性和 `IDisposable`，但不提供内置的实例化 API。

## 与 FRD 的映射关系

### 回答的未决问题

| FRD 未决问题 | 调研发现 | 结论 |
|-------------|----------|------|
| YooAsset 的 ResourcePackage 如何获取？ | `GameHotfixRootScope` 通过 `[SerializeField]` 持有 `ResourcePackage` 实例，在 `Configure` 方法中用 `RegisterInstance` 注册到 VContainer | 采用序列化字段 + 依赖注入模式，抽象层可通过构造函数注入获取 `ResourcePackage` |
| 资源句柄的具体设计？ | `YooUiAssetLoader` 返回 `UiAssetLease`（封装 GameObject 实例 + 释放回调），YooAsset 原生的 `AssetHandle` 已实现 `IDisposable` | 建议设计两种句柄：`AssetLease<T>`（纯资源）和 `GameObjectLease`（实例化对象），均实现 `IDisposable` |
| 异常类型设计？ | 项目中自定义异常继承 `InvalidOperationException`，`YooUiAssetLoader` 也使用 `InvalidOperationException` | 保持一致，使用 `InvalidOperationException`，无需自定义异常类型 |
| 命名空间和目录结构？ | Runtime 模块遵循 `Change.Runtime.<模块名>` 约定，每个模块独立目录（如 UI、FrameBudget、Gas） | 创建 `Change.Runtime.Asset` 命名空间，在 `UnityProject/Assets/Change/Runtime/Asset/` 目录下实现 |

### 支撑的决策

| FRD 决策 | 调研支撑 | 验证结果 |
|---------|----------|----------|
| 通过 VContainer 依赖注入 `IAssetManager` | `GameHotfixRootScope` 已使用 `builder.Register<IUiAssetLoader>` 注册接口实现 | 可行，延续现有模式 |
| 返回 `IDisposable` 句柄管理生命周期 | `UiAssetLease` 和 `AssetHandle` 均实现 `IDisposable` | 可行，符合现有代码风格 |
| 仅提供异步加载接口 | YooAsset 提供 `LoadAssetAsync<T>`，`YooUiAssetLoader` 也是纯异步 | 可行，YooAsset 原生支持 |
| 支持 `CancellationToken` + `IProgress<float>` | `AssetHandle` 有 `Progress` 属性，`Task` 属性可配合 UniTask，但不直接支持 `IProgress<T>` 接口 | 可行，需要手动轮询 `Progress` 属性并调用 `IProgress.Report` |
| 加载失败抛出异常 | `YooUiAssetLoader` 在失败时抛出 `InvalidOperationException`，包含详细上下文 | 可行，延续现有模式 |
| 提供集成测试 | 项目有 `EditModeTests` 和 `PlayModeTests` 两套测试框架，asmdef 配置完善 | 可行，在 `Change.Runtime.EditModeTests` 中编写测试 |

## Code References

### 核心文件

| 文件 | 职责 | 与本次需求的关系 |
|------|------|------------------|
| `Assets/Change/Runtime/UI/Loading/YooUiAssetLoader.cs` | UI 资源加载器，封装 YooAsset | 参考适配器模式和错误处理模式 |
| `Assets/Change/Runtime/UI/Loading/UiAssetLease.cs` | UI 资源句柄，实现 IDisposable | 参考句柄设计（简单封装 + 回调） |
| `Assets/Change/Runtime/UI/Abstractions/IUiAssetLoader.cs` | UI 资源加载接口 | 参考接口设计风格 |
| `Assets/GameScript/Composition/GameHotfixRootScope.cs` | VContainer 根作用域配置 | 参考 VContainer 注册模式和 ResourcePackage 获取方式 |
| `Assets/Change/Runtime/Composition/IHotfixGameInstaller.cs` | 安装器接口 | 参考分层注册模式 |
| `Library/PackageCache/com.tuyoogame.yooasset@2.3.18/Runtime/ResourcePackage/ResourcePackage.cs` | YooAsset 核心类 | 调研 LoadAssetAsync API 签名 |
| `Library/PackageCache/com.tuyoogame.yooasset@2.3.18/Runtime/ResourceManager/Handle/HandleBase.cs` | YooAsset 句柄基类 | 调研 Progress、Task、IDisposable 实现 |
| `Library/PackageCache/com.tuyoogame.yooasset@2.3.18/Runtime/ResourceManager/Handle/AssetHandle.cs` | YooAsset 资源句柄 | 调研具体句柄类型 |

### 关键函数/类

| 符号 | 位置 | 作用 |
|------|------|------|
| `ResourcePackage.LoadAssetAsync<T>(string, uint)` | `ResourcePackage.cs` | YooAsset 的泛型异步加载 API |
| `YooUiAssetLoader.FromResourcePackage(ResourcePackage)` | `YooUiAssetLoader.cs:64` | 工厂方法，从 ResourcePackage 创建加载器 |
| `UiAssetLease(GameObject, Action)` | `UiAssetLease.cs:10` | 句柄构造函数，封装实例和释放回调 |
| `HandleBase.Release()` | `HandleBase.cs:21` | YooAsset 句柄的释放方法 |
| `HandleBase.Progress` | `HandleBase.cs:49` | 加载进度属性（0-1） |
| `AssetHandle.Task` | `YooUiAssetLoader.cs:48` | 用于 await 的 Task 属性 |

## Integration Points

### 内部接口

- **VContainer 注册**：通过 `IContainerBuilder.Register<IAssetManager>(factory, Lifetime.Singleton)` 注册抽象层
- **ResourcePackage 获取**：构造函数注入 `ResourcePackage` 实例（由 `GameHotfixRootScope` 提供）
- **命名空间**：`Change.Runtime.Asset`（与现有模块命名一致）
- **测试集成**：在 `Change.Runtime.EditModeTests` 中编写测试，引用 `Change.Runtime` 和 `YooAsset`

### 外部依赖

- **YooAsset 2.3.18**：资源管理底层库
  - `ResourcePackage`：包管理器
  - `AssetHandle`：资源句柄
  - `LoadAssetAsync<T>`：异步加载 API
- **UniTask**：异步任务框架，YooAsset 的 `AssetHandle.Task` 返回标准 `System.Threading.Tasks.Task`，可通过 `.AsUniTask()` 转换
- **VContainer**：依赖注入容器
  - `IContainerBuilder`：构建时注册
  - `LifetimeScope`：作用域管理
- **Unity Test Framework**：测试框架（EditMode 和 PlayMode）

### 调用链

```
用户代码
  │
  └─> IAssetManager.LoadAsync<T>(path, progress, cancellationToken)
        │
        ├─> ResourcePackage.LoadAssetAsync<T>(path)  [YooAsset]
        │     │
        │     └─> 返回 AssetHandle
        │
        ├─> 轮询 AssetHandle.Progress → IProgress<float>.Report
        │
        ├─> await AssetHandle.Task.AsUniTask()
        │
        └─> 返回 AssetLease<T>(asset, () => handle.Release())
              │
              └─> 用户 Dispose() → 调用释放回调 → handle.Release()
```

实例化流程：
```
IAssetManager.LoadAndInstantiateAsync(path, progress, cancellationToken)
  │
  ├─> LoadAsync<GameObject>(path, progress, cancellationToken)
  │     │
  │     └─> 返回 AssetLease<GameObject>
  │
  ├─> Object.Instantiate(lease.Asset)
  │
  └─> 返回 GameObjectLease(instance, () => { Destroy(instance); lease.Dispose(); })
```

## Architecture Insights

### 现有模式

1. **适配器模式隔离第三方库**：
   - `YooUiAssetLoader` 通过 `IYooAssetPackage` 和 `IYooAssetLoadHandle` 接口隔离 YooAsset
   - 适配器类 `YooAssetPackageAdapter` 和 `YooAssetLoadHandleAdapter` 封装 YooAsset 类型
   - 公开的 `YooUiAssetLoader` 依赖内部接口，而非直接依赖 YooAsset

2. **句柄 + 回调的生命周期管理**：
   - `UiAssetLease` 封装 GameObject 实例和释放回调
   - 通过闭包捕获 YooAsset 的 `AssetHandle`，在 `Dispose()` 时调用 `handle.Release()`
   - 防止重复释放：`_release` 字段置 null

3. **VContainer 分层注册**：
   - 根作用域（`LifetimeScope`）配置引擎层依赖
   - 热更新作用域（`GameHotfixRootScope`）继承根作用域，注册业务层依赖
   - 通过 `IHotfixGameInstaller` 接口分离注册逻辑

4. **异常处理模式**：
   - 继承 `InvalidOperationException`，传递详细上下文（如 `windowId`、`location`、`reason`）
   - 多层异常聚合：`AggregateException` 包装主异常和释放异常

### 先例参考

- **UI 资源加载**：`YooUiAssetLoader` 已经实现了完整的适配器模式封装（2026-04-27 左右创建）
- **ContentStreaming 模块**：`IAssetDownloadAdapter` 和 `YooAssetDownloadAdapter` 也使用了适配器模式，但仅是占位实现
- **VContainer 注册**：`GameHotfixRootScope.Configure` 方法展示了标准的注册模式

### 风险与建议

| 风险 | 影响 | 建议 |
|------|------|------|
| YooAsset 不提供内置实例化 API | 需要手动实现 `LoadAndInstantiateAsync`，管理 GameObject 生命周期 | 在抽象层内部调用 `Object.Instantiate`，返回 `GameObjectLease` 时关联资源句柄和实例的双重释放逻辑 |
| YooAsset 的 `Progress` 是属性而非 `IProgress<T>` | 无法直接传递给 YooAsset，需要轮询 | 在异步加载期间启动后台任务轮询 `AssetHandle.Progress`，手动调用 `IProgress<float>.Report` |
| `ResourcePackage` 通过序列化字段注入 | 依赖 Unity 编辑器配置，运行时无法动态创建 | 接受这个限制，构造函数注入时要求已初始化的 `ResourcePackage` 实例 |
| 现有 `YooUiAssetLoader` 和新抽象层功能重叠 | 可能造成团队混淆，不知道用哪个 | 在抽象层文档中说明：`YooUiAssetLoader` 针对 UI 优化（如 WindowId 绑定），通用资源加载用 `IAssetManager`；长期可考虑将 `YooUiAssetLoader` 迁移到新抽象层 |
| 测试需要真实 Unity 环境 | EditMode 测试无法完整验证资源加载（需要 AssetBundle） | 优先编写 EditMode 测试验证接口契约和错误处理逻辑，补充 PlayMode 测试验证真实资源加载（需要准备测试资源包） |

## 调研问题与答案

### 现状调研

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| Q1.1 | YooAsset 的 ResourcePackage 在项目中如何获取和管理？ | grep 搜索 + 文件阅读 | `GameHotfixRootScope` 通过 `[SerializeField] private ResourcePackage _uiPackage;` 持有实例，在 `Configure` 方法中用 `builder.RegisterInstance(_uiPackage)` 注册到 VContainer |
| Q1.2 | 现有的命名空间和目录结构遵循什么约定？ | 目录扫描 + asmdef 检查 | Runtime 目录下按模块组织（FrameBudget、UI、Gas、Net 等），命名空间 `Change.Runtime.<模块名>`，每个模块独立目录 |
| Q1.3 | 项目中是否已有资源管理相关的抽象层或接口？ | grep 搜索 | 有 `IUiAssetLoader`（UI 专用）和 `IAssetDownloadAdapter`（下载适配器），但无通用资源加载抽象 |

### 模式调研

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| Q2.1 | `YooUiAssetLoader` 的资源句柄设计模式是什么？ | 文件阅读 | 返回 `UiAssetLease`，封装 GameObject 实例和 `Action` 回调，`Dispose()` 时销毁实例并调用 `AssetHandle.Release()` |
| Q2.2 | 项目中 VContainer 的注册模式是什么？ | grep 搜索 + 文件阅读 | 通过 `LifetimeScope` 的 `Configure(IContainerBuilder)` 方法注册，支持 `RegisterInstance`（实例）、`Register<TInterface, TImpl>`（接口-实现）、`Register<T>(factory, Lifetime)`（工厂） |
| Q2.3 | 项目中其他模块的异常处理模式是什么？ | 文件阅读 | 自定义异常继承 `InvalidOperationException`，构造函数传递上下文信息（如 `CodecOperationException` 包含 cmdId、requestId），`YooUiAssetLoader` 也使用 `InvalidOperationException` |

### 依赖调研

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| Q3.1 | YooAsset 的主要 API 和类型有哪些？ | PackageCache 文档浏览 | `ResourcePackage`（包管理器）、`AssetHandle`（资源句柄，继承 `HandleBase`）、`LoadAssetAsync<T>(string location, uint priority)`（异步加载 API） |
| Q3.2 | UniTask 与 YooAsset 的集成模式是什么？ | 文件阅读 | `AssetHandle` 有 `Task` 属性（`System.Threading.Tasks.Task`），可通过 `.AsUniTask()` 转换为 `UniTask`，示例：`await handle.Task.AsUniTask()` |
| Q3.3 | VContainer 的注册时机和生命周期管理是怎样的？ | 文件阅读 | `LifetimeScope` 在 `Awake` 时构建容器，支持 `Lifetime.Singleton`（单例）、`Lifetime.Transient`（瞬态），子作用域可通过 `CreateChild` 创建 |

### 风险调研

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| Q4.1 | 现有代码中 YooAsset 的直接使用点有哪些？ | grep 搜索 | 主要在 `GameHotfixRootScope`（注册 ResourcePackage）、`YooUiAssetLoader`（适配器）、`YooAssetDownloadAdapter`（占位实现），示例代码在 `Assets/Samples/` 下 |
| Q4.2 | 资源加载失败的已知场景和处理方式是什么？ | YooUiAssetLoader 错误处理分析 | 失败场景：创建句柄失败、异步加载任务失败、YooAsset 报告失败、Prefab 为 null、实例化失败；处理：捕获异常并封装为 `InvalidOperationException`，包含 windowId、location、reason、inner exception |

### 约束调研

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| Q5.1 | 项目中是否有性能测试或基准测试框架？ | 文件搜索 | 未发现专门的性能测试框架，有 `FrameBudget` 模块用于帧预算管理，可作为性能监控工具 |
| Q5.2 | Unity 测试框架的配置和使用情况如何？ | TestResults 目录分析 + asmdef 检查 | 有 `EditModeTests` 和 `PlayModeTests` 两套测试框架，asmdef 配置了 `UnityEngine.TestRunner` 和 `UnityEditor.TestRunner`，测试结果存放在 `UnityProject/TestResults/` |
