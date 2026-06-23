# 窗口管理增强 代码调研

> 日期: 2026-06-23
> 范围: UnityProject/Assets/Change/Runtime/UI/, UnityProject/Assets/Change/Runtime/Asset/
> 上游: .mozi/artifacts/window-manager/discover/2026-06-23-window-manager-frd.md
> 调研深度: 复杂

## Summary

现有窗口管理系统基于 WindowManager + IWindowFactory + IWindowView 架构，已实现并发控制（_gate 锁 + inflight 合并）、复用机制（ReuseIfLoaded）和多实例支持（AllowMultipleInstances + InstanceId）。FairyGuiWindowView 已支持基础层级管理（sortingOrder = Layer * 1000 + 1），但缺少 LRU 缓存、延迟释放、分组互斥功能。IAssetManager 只支持 UnityEngine.Object 类型资源加载，不直接支持 FairyGUI UIPackage。YooUiAssetLoader 通过 IYooAssetLoadHandle 加载预制体并实例化 GameObject，返回 UiAssetLease 管理生命周期。WindowRequest 是 readonly struct，需要扩展支持 Group 字段和更灵活的 Context 参数。

## 与 FRD 的映射关系

### 回答的未决问题

| FRD 未决问题 | 调研发现 | 结论 |
|-------------|----------|------|
| #1: 现有 WindowManager 的并发控制（_gate 锁）是否需要优化？ | 使用单一 object _gate 保护 _opened 和 _inflight 两个 Dictionary，锁粒度较粗但逻辑简单 | 暂时保留现有实现，LRU 缓存和延迟释放可复用同一个锁，后续性能测试后再优化 |
| #2: IWindowRegistry 是否需要支持运行时注销？ | 现有系统无运行时注销场景，FRD 假设业务层在启动时完成注册 | 不需要支持，设计为只读注册表即可 |
| #3: 延迟释放的定时器如何实现？ | 现有代码广泛使用 UniTask（如 WindowManager.OpenAsync） | 使用 UniTask.Delay，与现有异步模式一致 |
| #4: LRU 缓存数据结构选择？ | _opened 和 _inflight 都使用 Dictionary<WindowRequest, T>，查询效率高 | 使用 Dictionary<WindowRequest, CacheEntry> + LinkedList<WindowRequest> 实现 LRU，O(1) 查询和淘汰 |
| #5: Overlay 组的具体标识方式？ | WindowRequest.Options.Layer 已是 enum，可扩展；Group 将是新增的 string 字段 | 使用 null 或空字符串表示 Overlay 组，在组互斥逻辑中特殊处理 |
| #6: WindowRequest 是否需要扩展为 class？ | 当前是 readonly struct，Equals/GetHashCode 参与 Dictionary key | 保持 struct，添加 Group 字段和 Context 字段，Context 使用 object 类型（需要参与 Equals/GetHashCode） |
| #7: IAssetManager 是否已支持加载 FairyGUI 包？ | IAssetManager 只支持 LoadAsync<T> where T : UnityEngine.Object，UIPackage 不是 UnityEngine.Object | 不支持，需要扩展 IAssetManager 或在 FairyGuiWindowFactory 中手动调用 UIPackage.AddPackage |
| #8: FairyGuiWindowFactory 如何改造？ | 当前依赖 IUiAssetLoader.LoadPrefabAsync 返回 UiAssetLease（GameObject 实例） | 可保留 YooUiAssetLoader 用于加载 GameObject，但需额外处理 FairyGUI UIPackage 的加载和卸载生命周期 |
| #9: sortingOrder 的分配算法？ | 当前 FairyGuiWindowView.BringToFront 固定为 Layer * 1000 + 1，无动态递增 | 维护每层的 sortingOrder 计数器，打开窗口时递增，BringToFront 时重新分配更高值；层间隔 1000 足够（单层最多 999 个窗口） |
| #10: 窗口元数据（Group、Layer）应该存储在哪里？ | Layer 已存储在 WindowRequest.Options.Layer 和 IWindowView.Layer | Group 存储在 WindowRequest 中（新增字段），IWindowView 不需要感知 Group（由 WindowManager 管理） |

### 支撑的决策

| FRD 决策 | 调研支撑 | 验证结果 |
|---------|----------|----------|
| #1: 增强现有 WindowManager | WindowManager 架构清晰，已有 _opened、_inflight、_gate 等基础设施 | 可行，扩展点明确 |
| #10: 移除 YooUiAssetLoader，统一使用 IAssetManager | IAssetManager 不支持 FairyGUI UIPackage，YooUiAssetLoader 专为 UI 资源设计 | 需调整：保留 YooUiAssetLoader 用于 GameObject 加载，或扩展 IAssetManager |
| #20: WindowId + Context 作为唯一性 | 当前 WindowRequest.Equals 已支持 AllowMultipleInstances + InstanceId 多实例逻辑 | 可行，Context 可映射到 InstanceId 或扩展为更灵活的 object |
| #21: 保留 inflight 合并机制 | _inflight Dictionary + InflightEntry.WaiterCount 实现了合并，逻辑成熟 | 可行，LRU 缓存逻辑不影响 inflight 合并 |


## 依赖拓扑图

```
窗口管理增强
├── P0: WindowManager (UnityProject/Assets/Change/Runtime/UI/Core/WindowManager.cs)
│   ├── OpenAsync() — 异步打开窗口，支持复用和并发合并
│   ├── Close() — 关闭窗口，调用 Presenter.OnClose 和 Host.OnClosing
│   ├── TryGet() — 查询已打开窗口
│   ├── _opened: Dictionary<WindowRequest, OpenedWindowEntry> — 已打开窗口缓存
│   ├── _inflight: Dictionary<WindowRequest, InflightEntry> — 并发加载去重
│   └── _gate: object — 并发控制锁
├── P0: IWindowFactory (UnityProject/Assets/Change/Runtime/UI/Abstractions/IWindowFactory.cs)
│   └── CreateAsync() — 创建窗口视图，由 WindowManager 调用
├── P0: IAssetManager (UnityProject/Assets/Change/Runtime/Asset/IAssetManager.cs)
│   ├── LoadAsync<T>() — 从默认包或指定包加载资源
│   └── LoadAndInstantiateAsync() — 加载并实例化 GameObject
├── P1: WindowRequest (UnityProject/Assets/Change/Runtime/UI/Core/WindowRequest.cs)
│   ├── Id: WindowId — 窗口标识符
│   ├── Options: WindowOpenOptions — 打开选项（Layer、ReuseIfLoaded、AllowMultipleInstances、InstanceId）
│   └── Equals/GetHashCode — 参与 Dictionary key，基于 Id + MultipleInstances + InstanceId
├── P1: IWindowView (UnityProject/Assets/Change/Runtime/UI/Abstractions/IWindowView.cs)
│   ├── Id: WindowId
│   ├── Layer: WindowLayer
│   ├── State: WindowState
│   ├── BringToFront() — 置顶窗口
│   ├── SetVisible() — 显示/隐藏
│   └── Dispose() — 释放资源
├── P1: FairyGuiWindowView (UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowView.cs)
│   ├── BringToFront() — 设置 sortingOrder = Layer * 1000 + 1
│   ├── SetVisible() — 控制 GComponent.visible
│   ├── Dispose() — 释放 UiAssetLease
│   └── _root: GComponent — FairyGUI 根组件
├── P2: YooUiAssetLoader (UnityProject/Assets/Change/Runtime/UI/Loading/YooUiAssetLoader.cs)
│   ├── LoadPrefabAsync() — 加载并实例化 GameObject
│   └── 返回 UiAssetLease — GameObject 生命周期管理
└── P2: YooAssetManager (UnityProject/Assets/Change/Runtime/Asset/YooAssetManager.cs)
    └── LoadAsync<T>() — IAssetManager 的 YooAsset 实现
```

## 复用轮子清单

| # | 系统/模块 | 复用方式 | 关键接口 | 备注 |
|---|----------|----------|----------|------|
| 1 | WindowManager 并发控制 | 直接扩展 | `_gate: object`, `_opened: Dictionary`, `_inflight: Dictionary` | 复用现有锁机制保护 LRU 缓存和延迟释放队列 |
| 2 | WindowRequest 多实例支持 | 参考模式 | `Options.AllowMultipleInstances`, `Options.InstanceId`, `Equals()` | Context 参数可映射为 InstanceId 或扩展为 object 参与 Equals |
| 3 | FairyGuiWindowView 层级管理 | 扩展实现 | `BringToFront()`, `_root.sortingOrder` | 已有 Layer * 1000 基础值，扩展为动态递增 |
| 4 | IWindowPresenterHost 生命周期钩子 | 直接复用 | `OnOpened()`, `OnClosing()` | Presenter 在窗口打开/关闭时被通知，LRU 缓存复用时也会触发 OnOpen |
| 5 | UniTask 异步模式 | 直接复用 | `UniTask<T>`, `UniTaskVoid`, `UniTask.Delay()` | 延迟释放定时器使用 UniTask.Delay |
| 6 | UiAssetLease 资源生命周期 | 参考模式 | `Dispose()`, `Action _release` | LRU 缓存中的窗口保留 UiAssetLease，延迟释放时调用 Dispose |

## 改动与新增点

| # | 操作类型 | 文件/模块 | 说明 |
|---|---------|----------|------|
| 1 | 扩展 | `WindowRequest.cs` | 添加 `string Group` 字段和 `object Context` 字段，更新 Equals/GetHashCode |
| 2 | 扩展 | `WindowManager.cs` | 添加 LRU 缓存（Dictionary + LinkedList）、延迟释放队列、组互斥逻辑 |
| 3 | 扩展 | `FairyGuiWindowView.cs` | 动态分配 sortingOrder（维护每层计数器） |
| 4 | 新增 | `IWindowRegistry.cs` | 窗口元数据注册接口（WindowId → Package/Component/Group/Layer） |
| 5 | 新增 | `WindowRegistry.cs` | IWindowRegistry 的默认实现（Dictionary 存储） |
| 6 | 扩展或新增 | `IAssetManager.cs` 或 `FairyGuiWindowFactory.cs` | 支持 FairyGUI UIPackage 加载（需进一步设计） |
| 7 | 可选移除 | `YooUiAssetLoader.cs` | 如果 IAssetManager 扩展支持 GameObject 加载，可移除；否则保留 |
| 8 | 扩展 | `WindowOpenOptions.cs` | 当前已有 Layer、ReuseIfLoaded、AllowMultipleInstances、InstanceId，无需修改 |


## CodeGraph 分析记录

| 步骤 | 查询内容 | 关键发现 |
|------|----------|----------|
| 入口发现 | `codegraph_context(task="增强 Unity UI 窗口管理系统...")` | 发现 8 个核心符号：IAssetManager, WindowRequest, WindowManager, IWindowFactory, YooAssetManager, FairyGuiWindowView, YooUiAssetLoader, IWindowView |
| API表面(WindowManager) | `codegraph_callees(symbol="WindowManager")` | 公开方法：OpenAsync, TryGet, Close；依赖 IWindowFactory, IWindowPresenterHost |
| API表面(IAssetManager) | `codegraph_callees(symbol="IAssetManager")` | 泛型方法 LoadAsync<T>, LoadAndInstantiateAsync；约束 T : UnityEngine.Object |
| API表面(FairyGuiWindowView) | `codegraph_callees(symbol="FairyGuiWindowView")` | 实现 IWindowView，依赖 GComponent, UiAssetLease, WindowId, WindowLayer |
| 关键路径1 | `codegraph_trace(from="WindowManager.OpenAsync", to="FairyGuiWindowFactory.CreateAsync")` | 通过 IWindowFactory 接口动态调度，OpenAsync → OpenAsyncInternal → _factory.CreateAsync |
| 关键路径2 | `codegraph_trace(from="YooUiAssetLoader.LoadPrefabAsync", to="UiAssetLease")` | LoadPrefabAsync 直接 return new UiAssetLease(instance, releaseCallback)，生命周期管理清晰 |
| 影响面(WindowManager) | `codegraph_impact(symbol="WindowManager", depth=2)` | 41 个上游符号受影响，主要是测试（WindowManagerCoreTests, WindowManagerPresenterHostTests）和集成点（GameFlowDemoDriver, GameHotfixRootScope） |
| 影响面(WindowRequest) | `codegraph_impact(symbol="WindowRequest", depth=2)` | 50 个上游符号，包括 WindowManager、IWindowFactory、IWindowPresenterHost、所有测试 |
| 影响面(FairyGuiWindowView) | `codegraph_impact(symbol="FairyGuiWindowView", depth=2)` | 22 个上游符号，主要是 FairyGuiWindowFactory 和 IWindowView 接口 |
| 源码确认 | `codegraph_explore(query="WindowManager IWindowFactory IWindowView...")` | 确认了 109 个符号的源码，验证了接口签名、并发控制实现（_gate 锁）、多实例逻辑（Equals 基于 Id + MultipleInstances + InstanceId） |

## Code References

### 核心文件

| 文件 | 职责 | 与本次需求的关系 |
|------|------|------------------|
| `UnityProject/Assets/Change/Runtime/UI/Core/WindowManager.cs` | 窗口生命周期管理、并发控制、复用机制 | 需要扩展：添加 LRU 缓存、延迟释放、组互斥逻辑 |
| `UnityProject/Assets/Change/Runtime/UI/Core/WindowRequest.cs` | 窗口请求的唯一性标识 | 需要扩展：添加 Group 和 Context 字段 |
| `UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowView.cs` | FairyGUI 窗口视图实现 | 需要扩展：动态 sortingOrder 分配算法 |
| `UnityProject/Assets/Change/Runtime/UI/Abstractions/IWindowFactory.cs` | 窗口创建工厂接口 | 间接影响：创建窗口时需要从注册表查询元数据 |
| `UnityProject/Assets/Change/Runtime/Asset/IAssetManager.cs` | 统一资源加载接口 | 需要评估：是否扩展支持 FairyGUI UIPackage |
| `UnityProject/Assets/Change/Runtime/UI/Loading/YooUiAssetLoader.cs` | UI 资源加载器 | 需要评估：是否保留或迁移到 IAssetManager |
| `UnityProject/Assets/Change/Framework/UI/Abstractions/WindowOpenOptions.cs` | 窗口打开选项 | 无需修改：已有 Layer、ReuseIfLoaded、AllowMultipleInstances、InstanceId |

### 关键函数/类

| 符号 | 位置 | 作用 |
|------|------|------|
| `WindowManager.OpenAsync()` | `WindowManager.cs:59` | 窗口打开入口，处理复用和并发 |
| `WindowManager.OpenAsyncInternal()` | `WindowManager.cs:65` | 内部实现：检查 _opened、_inflight，调用 _factory.CreateAsync |
| `WindowManager.CreateAndCacheAsync()` | `WindowManager.cs:158` | 异步创建窗口并缓存到 _opened |
| `WindowManager.Close()` | `WindowManager.cs:128` | 关闭窗口，调用 OnClose 和 OnClosing，从 _opened 移除 |
| `WindowRequest.Equals()` | `WindowRequest.cs:19` | 唯一性判断：Id + AllowMultipleInstances + InstanceId |
| `FairyGuiWindowView.BringToFront()` | `FairyGuiWindowView.cs:32` | 设置 sortingOrder = Layer * 1000 + 1 |
| `FairyGuiWindowFactory.CreateAsync()` | `FairyGuiWindowFactory.cs:19` | 创建 FairyGuiWindowView，调用 IUiAssetLoader.LoadPrefabAsync |
| `YooUiAssetLoader.LoadPrefabAsync()` | `YooUiAssetLoader.cs:70` | 加载 GameObject 预制体，返回 UiAssetLease |
| `IAssetManager.LoadAsync<T>()` | `IAssetManager.cs:21` | 从默认包或指定包加载资源，约束 T : UnityEngine.Object |


## Integration Points

### 内部接口

**WindowManager 扩展点：**
- `_opened: Dictionary<WindowRequest, OpenedWindowEntry>` — 需要扩展为 LRU 缓存结构
- `_gate: object` — 复用现有锁保护缓存和延迟释放队列
- `IWindowPresenterHost.OnOpened/OnClosing` — 窗口生命周期钩子，缓存复用时也会触发

**WindowRequest 扩展点：**
- 添加 `string Group` 字段 — 窗口分组标识
- 添加 `object Context` 字段 — 参数化窗口支持
- 更新 `Equals()` 和 `GetHashCode()` — Context 参与唯一性判断

**IWindowRegistry 新接口：**
```csharp
public interface IWindowRegistry
{
    void Register(WindowId id, string package, string component, string group, WindowLayer layer);
    bool TryGetMetadata(WindowId id, out WindowMetadata metadata);
}
```

**FairyGuiWindowView 扩展点：**
- `BringToFront()` — 改为动态分配 sortingOrder
- 需要维护静态或单例的层级计数器

### 外部依赖

**Unity + FairyGUI + YooAsset：**
- `FairyGUI.GComponent` — UI 根组件，通过 `sortingOrder` 控制层级
- `YooAsset.ResourcePackage` — 资源包管理
- `Cysharp.Threading.Tasks.UniTask` — 异步编程

**VContainer：**
- `IContainerBuilder.RegisterInstance()` — 注册 IWindowRegistry 单例

### 调用链

```
业务层打开窗口
  │
  ├─► WindowManager.OpenAsync(WindowRequest)
  │     │
  │     ├─► 检查 LRU 缓存（新增）
  │     │     └─► 命中：直接返回，触发 Presenter.OnOpen
  │     │
  │     ├─► 检查组互斥（新增）
  │     │     └─► 关闭旧组所有窗口
  │     │
  │     ├─► 检查 _opened（现有）
  │     │     └─► ReuseIfLoaded: 复用已打开窗口
  │     │
  │     ├─► 检查 _inflight（现有）
  │     │     └─► 合并并发请求
  │     │
  │     └─► IWindowFactory.CreateAsync()
  │           │
  │           ├─► IWindowRegistry.TryGetMetadata（新增）
  │           │     └─► 查询 Package/Component 映射
  │           │
  │           └─► IUiAssetLoader.LoadPrefabAsync()
  │                 └─► YooAsset.LoadGameObjectAsync()
  │
  └─► WindowManager.Close(WindowRequest)
        │
        ├─► 从 _opened 移除
        ├─► 触发 Presenter.OnClose, Host.OnClosing
        ├─► 加入 LRU 缓存（新增）
        │     └─► 检查容量，淘汰最旧窗口
        │           └─► 启动 30 秒延迟释放定时器（新增）
        │
        └─► 保留 IWindowView 实例（新增）
```

## Architecture Insights

### 现有模式

**1. 并发控制模式：**
- 使用单一 `object _gate` 锁保护所有共享状态（`_opened`, `_inflight`）
- 锁粒度较粗，但逻辑简单，避免死锁
- LRU 缓存和延迟释放可以复用同一个锁

**2. 资源生命周期模式：**
- `UiAssetLease` 实现 `IDisposable`，封装释放回调
- `FairyGuiWindowView` 持有 `UiAssetLease`，Dispose 时级联释放
- 符合 RAII 模式，可直接应用于 LRU 缓存中的窗口

**3. 异步加载模式：**
- 全面使用 `UniTask<T>` 替代 `Task<T>`
- `UniTaskVoid` 用于 fire-and-forget 场景（如 `CreateAndCacheAsync`）
- 延迟释放定时器应使用 `UniTask.Delay()`

**4. 多实例支持模式：**
- `WindowRequest.Equals()` 基于 `Id + AllowMultipleInstances + InstanceId`
- 参数化窗口通过不同 `InstanceId` 区分
- Context 参数可以扩展这个模式，映射为 InstanceId 或直接参与 Equals

### 先例参考

**类似功能实现：**
- 无直接先例 — LRU 缓存和延迟释放是新功能
- 可以参考的模式：
  - `_opened` Dictionary 的使用方式 — LRU 缓存也用 Dictionary 加速查询
  - `InflightEntry.WaiterCount` 引用计数 — 延迟释放可以用类似的引用计数避免过早释放

**代码风格：**
- 使用 readonly struct 作为 Dictionary key（如 `WindowRequest`）
- 私有嵌套类封装内部状态（如 `InflightEntry`）
- 接口抽象 + 实现分离（如 `IWindowFactory` → `FairyGuiWindowFactory`）

### 风险与建议

| 风险 | 影响 | 建议 |
|------|------|------|
| WindowRequest 从 struct 改为 class 会破坏所有调用方 | 编译错误，需要大量修改测试和业务代码 | 保持 struct，Context 使用 object 类型并参与 Equals/GetHashCode |
| IAssetManager 不支持 FairyGUI UIPackage 加载 | 无法完全移除 YooUiAssetLoader | 阶段性方案：保留 YooUiAssetLoader 用于 GameObject 加载；长期方案：扩展 IAssetManager 支持非 UnityEngine.Object 资源 |
| LRU 缓存中的窗口占用内存较大 | 10 个窗口约 500 MB（假设单窗口 50 MB） | 通过性能测试验证实际内存占用，必要时调整缓存容量或实现部分释放（只保留资源文件，释放 GameObject） |
| 组互斥逻辑在高频切换时可能导致卡顿 | 关闭多个窗口时可能有短暂卡顿 | 并行关闭窗口而非串行，但考虑到 FRD 决策 #15 改为"立即关闭"，串行关闭即可 |
| sortingOrder 动态递增可能溢出 | int.MaxValue 约 21 亿，实际不会溢出 | 每层独立计数器，重置策略：超过 900 时重新分配该层所有窗口的 sortingOrder |
| Overlay 组窗口可能挤占主窗口缓存位 | 高频 Toast 提示可能导致主窗口被淘汰 | 监控缓存命中率，必要时为 Overlay 组设置独立缓存池或更短的延迟释放时间 |
| Context 参数使用 object 类型，需要正确实现 Equals | 如果 Context 是自定义类型且未重写 Equals，会导致缓存失效 | 在文档中明确说明 Context 类型要求，或提供辅助类（如 `ContextWrapper<T>` 自动包装值类型） |


## 调研问题与答案

### 现状调研

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| 1 | 现有窗口管理的核心架构是什么？ | codegraph_context, codegraph_explore | WindowManager + IWindowFactory + IWindowView 三层架构，Manager 负责生命周期和并发控制，Factory 负责创建，View 负责显示 |
| 2 | 并发控制如何实现？ | 阅读 WindowManager.cs 源码 | 单一 object _gate 锁 + _opened Dictionary + _inflight Dictionary，InflightEntry.WaiterCount 实现并发请求合并 |
| 3 | 窗口复用机制如何工作？ | 阅读 OpenAsyncInternal 源码 | WindowRequest.Options.ReuseIfLoaded 控制，true 时检查 _opened 并复用，false 时 Dispose 旧窗口并创建新的 |
| 4 | 窗口层级管理现状？ | 阅读 FairyGuiWindowView.BringToFront | 固定公式 sortingOrder = Layer * 1000 + 1，无动态递增，后打开的窗口无法自动在上面 |
| 5 | IAssetManager 的能力边界？ | codegraph_explore IAssetManager | 只支持 LoadAsync<T> where T : UnityEngine.Object，不支持 FairyGUI UIPackage |
| 6 | YooUiAssetLoader 的职责？ | codegraph_trace, 阅读源码 | 加载 GameObject 预制体并实例化，返回 UiAssetLease 管理生命周期，包含错误处理和取消支持 |

### 模式调研

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| 1 | 如何实现资源生命周期管理？ | 阅读 UiAssetLease, AssetLease 源码 | IDisposable + Action 释放回调模式，Dispose 时调用回调释放底层资源 |
| 2 | 多实例窗口如何区分？ | 阅读 WindowRequest.Equals 源码 | AllowMultipleInstances + InstanceId 机制，Equals 和 GetHashCode 参与 Dictionary key |
| 3 | 异步加载模式？ | codegraph_callees, 查看 UniTask 使用 | 全面使用 UniTask<T>，UniTaskVoid 用于 fire-and-forget，await 支持取消令牌 |
| 4 | 窗口生命周期钩子？ | 阅读 IWindowPresenterHost 源码 | OnOpened 在窗口创建后调用，OnClosing 在关闭前调用，支持创建 per-window scope |

### 依赖调研

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| 1 | WindowManager 依赖哪些接口？ | codegraph_callees WindowManager | IWindowFactory, IWindowPresenterHost, WindowRequest, IWindowView |
| 2 | FairyGuiWindowFactory 的依赖？ | 阅读构造函数和 CreateAsync | IUiAssetLoader, IWindowLocationResolver，用于加载资源和解析窗口位置 |
| 3 | FairyGuiWindowView 依赖的外部类型？ | codegraph_callees FairyGuiWindowView | FairyGUI.GComponent, UiAssetLease, WindowId, WindowLayer |
| 4 | 现有代码是否依赖 YooUiAssetLoader？ | codegraph_impact YooUiAssetLoader | FairyGuiWindowFactory 通过 IUiAssetLoader 接口依赖，可以替换实现 |

### 风险调研

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| 1 | 修改 WindowRequest 会影响多少地方？ | codegraph_impact WindowRequest | 50 个上游符号，包括 WindowManager、所有测试、IWindowFactory 实现、业务集成点 |
| 2 | 修改 WindowManager 会影响多少地方？ | codegraph_impact WindowManager | 41 个上游符号，主要是测试（WindowManagerCoreTests 9 个测试方法）和业务集成（GameFlowDemoDriver, GameHotfixRootScope） |
| 3 | 修改 FairyGuiWindowView 会影响多少地方？ | codegraph_impact FairyGuiWindowView | 22 个上游符号，主要是 FairyGuiWindowFactory 和 IWindowView 接口，影响面较小 |
| 4 | _gate 锁的粒度是否会成为性能瓶颈？ | 分析 WindowManager 锁使用 | 锁保护的临界区很小（Dictionary 读写、状态判断），持有时间短，LRU 缓存和延迟释放的操作也很轻量，暂时不会成为瓶颈 |

### 约束调研

| # | 问题 | 调研方法 | 答案 |
|---|------|----------|------|
| 1 | WindowRequest 必须是 struct 吗？ | 分析 Dictionary key 使用 | 是，作为 Dictionary key 使用，改为 class 会破坏性能（引用比较 vs 值比较）和语义（可变 vs 不可变） |
| 2 | IAssetManager 能否扩展支持非 UnityEngine.Object？ | 查看接口定义和约束 | 当前接口约束 T : UnityEngine.Object，扩展需要添加新方法（如 LoadRawAsync）或使用非泛型方法 |
| 3 | FairyGUI sortingOrder 的取值范围？ | 查看 GComponent 文档和现有代码 | int 类型，范围 -2,147,483,648 到 2,147,483,647，Layer * 1000 为基础值，每层最多 999 个窗口 |
| 4 | UniTask.Delay 的精度？ | UniTask 文档 | 基于 Unity 帧更新，精度约 16ms（60 FPS），30 秒延迟释放的误差可接受 |
| 5 | VContainer 是否支持运行时注册？ | VContainer 架构知识 | 不支持，IContainerBuilder 只在容器构建阶段有效，IWindowRegistry 必须在启动时注册完成 |
