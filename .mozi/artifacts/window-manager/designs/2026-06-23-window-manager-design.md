# 窗口管理增强 架构设计

> 日期: 2026-06-23 | 状态: 草稿
> FRD: .mozi/artifacts/window-manager/discover/2026-06-23-window-manager-frd.md
> 上游: .mozi/artifacts/window-manager/research/2026-06-23-window-manager-research.md

## 上游产出引用

### 来自 FRD

| 引用内容 | 如何使用 |
|---------|----------|
| 决策 #1: 增强现有 WindowManager | 架构决策 - 集成式 LRU 缓存方案 |
| 决策 #10: 统一使用 IAssetManager | 架构决策 - 保留 YooUiAssetLoader 并扩展 |
| 决策 #22: IWindowRegistry 注册接口 | 切片 A 实现窗口注册系统 |
| 验收条件: 5 层窗口层级 | 切片 C 实现层级管理 |
| 验收条件: 组间互斥 + Overlay 特殊处理 | 切片 C 实现分组互斥逻辑 |
| 验收条件: 最多缓存 10 个窗口 | 切片 D 实现 LRU 缓存 |
| 验收条件: 30 秒延迟释放 | 切片 D 实现延迟释放机制 |
| 未决问题 #1-10 | Research 已全部回答 |

### 来自 Research

| 引用内容 | 如何使用 |
|---------|----------|
| 现有 _gate 锁机制 | 复用单一锁保护缓存和延迟释放 |
| WindowRequest 是 readonly struct | 保持 struct，扩展 Group 和 Context 字段 |
| IAssetManager 不支持 UIPackage | 保留 YooUiAssetLoader，添加 LoadPackageAsync 方法 |
| sortingOrder = Layer * 1000 + 1 | 扩展为动态递增算法 |
| inflight 合并机制成熟 | 保留并与 LRU 缓存协同 |
| UiAssetLease 生命周期管理 | 缓存条目持有 Lease 直到延迟释放 |

## 架构决策

| 决策 | 选择 | 理由 |
|------|------|------|
| LRU 缓存架构 | 集成到 WindowManager（Dictionary + LinkedList + 单一锁） | 简单，复用现有 _gate 锁，O(1) 查询和淘汰，避免双锁协调复杂度 |
| 资源加载策略 | 保留 YooUiAssetLoader，扩展 LoadPackageAsync 方法 | IAssetManager 不支持 UIPackage，扩展 Asset 模块超出本功能范围，务实选择 |
| 分组互斥策略 | 主动追踪当前活跃组（_currentActiveGroup） | O(1) 组切换判断，状态显式，易于测试和调试 |
| 延迟释放定时器 | 每个缓存窗口独立 UniTask.Delay(30s) | 与现有 UniTask 异步模式一致，取消语义清晰（CancellationTokenSource） |
| WindowRequest 结构 | 保持 struct，添加 Group 和 Context 字段 | 避免破坏 50+ 上游符号，struct 作为 Dictionary key 性能更优 |
| Overlay 组标识 | null 或空字符串 | 简单，与 string 类型自然契合，在组互斥逻辑中特殊处理即可 |


## 文件地图

| 文件 | 所属切片 | 职责 |
|------|----------|------|
| `UnityProject/Assets/Change/Runtime/UI/Abstractions/IWindowRegistry.cs` | 切片 A | 窗口注册接口定义 |
| `UnityProject/Assets/Change/Runtime/UI/Core/WindowRegistry.cs` | 切片 A | 窗口注册表默认实现 |
| `UnityProject/Assets/Change/Runtime/UI/Core/WindowMetadata.cs` | 切片 A | 窗口元数据结构（Package/Component/Group/Layer） |
| `UnityProject/Assets/Change/Runtime/UI/Core/WindowRequest.cs` | 切片 B | 扩展 Group 和 Context 字段，更新 Equals/GetHashCode |
| `UnityProject/Assets/Change/Runtime/UI/Core/WindowManager.cs` | 切片 C, D | 添加分组互斥、LRU 缓存、延迟释放逻辑 |
| `UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowView.cs` | 切片 C | 动态 sortingOrder 分配 |
| `UnityProject/Assets/Change/Runtime/UI/FairyGui/WindowLayerSortingOrderManager.cs` | 切片 C | 每层 sortingOrder 计数器管理 |
| `UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowFactory.cs` | 切片 C, E | 从 IWindowRegistry 查询元数据，调用 LoadPackageAsync |
| `UnityProject/Assets/Change/Runtime/UI/Core/CachedWindowEntry.cs` | 切片 D | LRU 缓存条目（View + CancellationTokenSource） |
| `UnityProject/Assets/Change/Runtime/UI/Loading/YooUiAssetLoader.cs` | 切片 E | 扩展 LoadPackageAsync 方法 |

## 切片分解

### 切片 A: 窗口注册与元数据管理

**依赖：** 无  
**风险等级：** 低  
**涉及文件：** `IWindowRegistry.cs`（新增）, `WindowRegistry.cs`（新增）, `WindowMetadata.cs`（新增）

**内容：** 提供窗口元数据注册系统，业务层可以在启动时注册窗口的 Package、Component、Group、Layer 信息，Runtime 层通过 IWindowRegistry 查询。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `IWindowRegistry.Register` | `void Register(WindowId id, string packageName, string componentName, string group, WindowLayer layer)` | 注册窗口元数据，重复注册抛出异常 |
| `IWindowRegistry.TryGetMetadata` | `bool TryGetMetadata(WindowId id, out WindowMetadata metadata)` | 查询窗口元数据，找到返回 true |

**数据契约：**

```csharp
public readonly struct WindowMetadata
{
    public string PackageName { get; }     // FairyGUI 包名
    public string ComponentName { get; }   // FairyGUI 组件名
    public string Group { get; }           // 窗口分组，null 或空字符串表示 Overlay
    public WindowLayer Layer { get; }      // 窗口层级
}
```

**验收标准：**

- [ ] 可以注册窗口元数据并成功查询
- [ ] 重复注册同一 WindowId 抛出 `InvalidOperationException`
- [ ] 查询不存在的 WindowId 返回 false
- [ ] 注册 group 为 null 的窗口，查询返回的 `WindowMetadata.Group` 为 null
- [ ] 业务层可以在 VContainer Scope 中批量注册窗口（示例代码可编译通过）

**回归风险评估：**

- **影响范围：** 无，纯新增接口
- **缓解措施：** IWindowRegistry 通过 VContainer 注册为单例，现有代码不感知

---

### 切片 B: WindowRequest 扩展与唯一性

**依赖：** 无  
**风险等级：** 高（Equals/GetHashCode 实现错误会导致缓存失效）  
**涉及文件：** `WindowRequest.cs`（扩展）

**内容：** 扩展 WindowRequest 结构体，添加 Group 和 Context 字段支持分组互斥和参数化窗口，更新 Equals/GetHashCode 逻辑使 Context 参与唯一性判断。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `WindowRequest` 构造函数 | `WindowRequest(WindowId id, WindowOpenOptions options, string group, object context)` | 创建窗口请求，group 和 context 可为 null |
| `WindowRequest.Equals` | `bool Equals(WindowRequest other)` | AllowMultipleInstances 时比较 Context，否则只比较 Id |
| `WindowRequest.GetHashCode` | `int GetHashCode()` | HashCode.Combine(Id, InstanceId?, Context?) |

**数据契约：**

- **Group 字段：** `string` 类型，null 或空字符串表示 Overlay 组，非空字符串表示普通分组
- **Context 字段：** `object` 类型，null 表示单例窗口，非 null 表示参数化窗口（如物品详情 + itemId）
- **Equals 逻辑：** Group 不参与 Equals；Context 仅在 AllowMultipleInstances 为 true 时参与比较

**验收标准：**

- [ ] 相同 Id + 相同 Context 的 WindowRequest 判定为相等
- [ ] 相同 Id + 不同 Context 的 WindowRequest 判定为不相等
- [ ] Context 为 null 的 WindowRequest 可正确参与 Dictionary 查询
- [ ] Context 为值类型（int, string）时 Equals 和 GetHashCode 正确工作
- [ ] 现有测试中的 WindowRequest 构造无需修改（向后兼容，Group 和 Context 默认 null）
- [ ] 单元测试覆盖 Equals/GetHashCode 的边界情况

**回归风险评估：**

- **影响范围：** 高 - 50 个上游符号，所有使用 WindowRequest 的代码
- **缓解措施：**
  - 提供向后兼容的构造函数重载（不传 Group 和 Context 时默认为 null）
  - 现有 Equals/GetHashCode 逻辑保持不变（只在 AllowMultipleInstances 时才比较 Context）
  - 编译后运行完整测试套件验证无破坏性变更


---

### 切片 C: 分组互斥与动态层级管理

**依赖：** 切片 A（需要查询 Group）、切片 B（WindowRequest.Group 字段）  
**风险等级：** 中（Overlay 组特殊处理、sortingOrder 溢出处理）  
**涉及文件：** `WindowManager.cs`（扩展）, `FairyGuiWindowView.cs`（扩展）, `WindowLayerSortingOrderManager.cs`（新增）, `FairyGuiWindowFactory.cs`（修改）

**内容：** 实现窗口分组互斥逻辑（打开新组时关闭旧组窗口，Overlay 组不参与互斥），动态分配 sortingOrder（同层后打开的窗口自动在上面，支持手动置顶）。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `WindowManager._currentActiveGroup` | `string` 字段 | 追踪当前活跃分组，null 表示无活跃组或当前是 Overlay |
| `WindowManager.CloseWindowsInGroup` | `void CloseWindowsInGroup(string group)` | 关闭指定组的所有窗口（不等待动画） |
| `WindowManager.IsOverlayGroup` | `bool IsOverlayGroup(string group)` | 判断是否 Overlay 组（null 或空字符串） |
| `WindowLayerSortingOrderManager.AllocateSortingOrder` | `int AllocateSortingOrder(WindowLayer layer)` | 为指定层分配新的 sortingOrder |
| `WindowLayerSortingOrderManager.ResetLayerIfNeeded` | `void ResetLayerIfNeeded(WindowLayer layer)` | 计数器超过 900 时重置该层 |
| `FairyGuiWindowView.SetSortingOrder` | `void SetSortingOrder(int sortingOrder)` | 设置窗口的 sortingOrder |

**数据契约：**

**组互斥规则：**
- Overlay 组（null 或空字符串）：不参与互斥，可与任何组共存
- 普通组（非空字符串）：打开时关闭其他普通组的所有窗口，但不关闭 Overlay 组

**sortingOrder 分配规则：**
- 基础值：`Layer * 1000`（Background=0, Normal=1000, Popup=2000, Guide=3000, System=4000）
- 动态值：基础值 + 该层计数器（从 1 开始递增）
- 重置策略：计数器超过 900 时，重新分配该层所有已打开窗口的 sortingOrder，计数器归零

**行为契约：**

**OpenAsync 组互斥流程：**
1. 从 IWindowRegistry 查询窗口元数据，获取 Group 和 Layer
2. 如果 Group 是 Overlay，跳过互斥检查
3. 如果 Group 不是 Overlay 且与 `_currentActiveGroup` 不同：
   - 遍历 `_opened` Dictionary 的所有 WindowRequest key
   - 对每个 key 调用 `_registry.TryGetMetadata` 查询其 Group
   - 如果查询到的 Group == `_currentActiveGroup`，调用 `Close(key)` 关闭该窗口（不等待动画）
   - 更新 `_currentActiveGroup = Group`
4. 从 WindowLayerSortingOrderManager 分配 sortingOrder
5. 继续原有 OpenAsync 逻辑（检查 _opened、_inflight、调用 Factory 创建）

**验收标准：**

- [ ] 打开 "Shop" 组窗口后，再打开 "Guild" 组窗口，"Shop" 组所有窗口自动关闭
- [ ] 打开 Overlay 组窗口（如确认框）不会关闭当前活跃组的窗口
- [ ] 在 "Shop" 组打开多个窗口，后打开的窗口 sortingOrder 更大（自动在上面）
- [ ] 调用 BringToFront 后，窗口 sortingOrder 更新为该层最大值 + 1
- [ ] 同层打开 100 个窗口，sortingOrder 正确递增，无溢出
- [ ] FairyGuiWindowFactory 从 IWindowRegistry 查询到正确的 PackageName 和 ComponentName

**回归风险评估：**

- **影响范围：** 中 - WindowManager.OpenAsync 修改影响 41 个上游符号
- **缓解措施：**
  - 组互斥逻辑只在窗口携带非 Overlay Group 时触发
  - sortingOrder 分配向后兼容（原有固定值改为动态值，不影响层级关系）
  - 现有窗口如果未注册到 IWindowRegistry，抛出清晰的异常信息提示开发者注册

---

### 切片 D: LRU 缓存与延迟释放

**依赖：** 切片 B（WindowRequest 作为缓存 key）、切片 C（关闭窗口后加入缓存）  
**风险等级：** 高（缓存淘汰逻辑、延迟释放定时器取消、并发安全）  
**涉及文件：** `WindowManager.cs`（扩展）, `CachedWindowEntry.cs`（新增）

**内容：** 实现全局 LRU 缓存（最多 10 个窗口），窗口关闭后加入缓存并启动 30 秒延迟释放定时器，重新打开时复用缓存实例并触发 OnOpen。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `WindowManager._cache` | `Dictionary<WindowRequest, CachedWindowEntry>` | LRU 缓存存储 |
| `WindowManager._cacheAccessOrder` | `LinkedList<WindowRequest>` | LRU 访问顺序，头部=最近访问 |
| `WindowManager.AddToCache` | `void AddToCache(WindowRequest request, IWindowView view)` | 将窗口加入缓存并启动延迟释放 |
| `WindowManager.EvictLeastRecentlyUsed` | `void EvictLeastRecentlyUsed()` | 淘汰最旧窗口 |
| `WindowManager.ScheduleDelayedRelease` | `UniTaskVoid ScheduleDelayedRelease(WindowRequest, CancellationToken)` | 30 秒后释放窗口资源 |
| `CachedWindowEntry` | `class CachedWindowEntry` | 缓存条目（IWindowView + CancellationTokenSource） |

**数据契约：**

**LRU 缓存结构：**
- `_cache: Dictionary<WindowRequest, CachedWindowEntry>` - O(1) 查询
- `_cacheAccessOrder: LinkedList<WindowRequest>` - O(1) 移动到头部，O(1) 移除尾部
- 链表头部 = 最近访问，尾部 = 最久未访问

**缓存容量：**
- 全局最多 10 个窗口（包含 Overlay 组）
- 超出时淘汰 `_cacheAccessOrder` 尾部窗口

**延迟释放：**
- 窗口加入缓存后，启动 `UniTask.Delay(30000ms)` 定时器
- 定时器到期时调用 `view.Dispose()` 释放资源
- 如果窗口在 30 秒内重新打开，取消定时器并从缓存恢复

**行为契约：**

**AddToCache 流程：**
1. 将 `(request, CachedWindowEntry)` 加入 `_cache`
2. 将 `request` 加入 `_cacheAccessOrder` 头部
3. 调用 `ScheduleDelayedRelease` 启动 30 秒定时器
4. 如果缓存容量超过 10，调用 `EvictLeastRecentlyUsed`

**EvictLeastRecentlyUsed 流程：**
1. 从 `_cacheAccessOrder` 尾部移除最旧的 WindowRequest
2. 从 `_cache` 移除对应 CachedWindowEntry
3. CachedWindowEntry 的 ReleaseCts 仍在运行，30 秒后会触发 Dispose

**ScheduleDelayedRelease 流程：**
```csharp
private async UniTaskVoid ScheduleDelayedRelease(WindowRequest request, CancellationToken ct)
{
    try
    {
        await UniTask.Delay(30000, cancellationToken: ct);
        
        lock (_gate)
        {
            if (_cache.TryGetValue(request, out var entry))
            {
                _cache.Remove(request);
                _cacheAccessOrder.Remove(request);
                entry.View.Dispose();  // 释放资源（GameObject + UiAssetLease）
            }
        }
    }
    catch (OperationCanceledException)
    {
        // 窗口在 30 秒内重新打开，定时器被取消，正常流程
    }
}
```

**OpenAsync 缓存命中流程：**
1. `lock (_gate)` 进入临界区
2. 检查 `_cache.TryGetValue(request, out var entry)`
3. 如果命中：
   - `entry.ReleaseCts.Cancel()` 取消延迟释放
   - 从 `_cache` 移除该条目：`_cache.Remove(request)`
   - 从 `entry.View` 提取 IWindowView，创建 `OpenedWindowEntry` 并加入 `_opened`
   - 从 `_cacheAccessOrder` 移除该 request：`_cacheAccessOrder.Remove(request)`
   - 退出临界区，调用 `_presenterHost.OnOpened(request, entry.View)` 触发 Presenter.OnOpen
   - 返回 `entry.View`

**验收标准：**

- [ ] 窗口关闭后立即重新打开，复用缓存实例（无资源加载）
- [ ] 窗口关闭 30 秒后自动释放资源（Dispose 被调用）
- [ ] 缓存中窗口重新打开时，Presenter.OnOpen 被触发
- [ ] 同时缓存 10 个窗口，第 11 个窗口关闭时淘汰最旧的窗口
- [ ] 淘汰的窗口仍然有 30 秒延迟释放（不是立即释放）
- [ ] 延迟释放期间重新打开窗口，定时器被正确取消（无资源泄漏）
- [ ] Overlay 组窗口参与 LRU 缓存（与普通窗口相同逻辑）
- [ ] 并发打开同一窗口，缓存命中时不会重复触发 OnOpen

**回归风险评估：**

- **影响范围：** 高 - WindowManager.OpenAsync 和 Close 修改
- **缓解措施：**
  - LRU 缓存对调用方完全透明（API 签名不变）
  - 缓存命中 vs 未命中的行为一致（都触发 OnOpen）
  - 添加集成测试验证缓存与 inflight 合并机制的协同
  - 监控内存占用（10 个窗口约 500 MB，在可接受范围内）


---

### 切片 E: 资源加载统一与并发优化

**依赖：** 切片 D（缓存机制已就位）  
**风险等级：** 低（保留现有 inflight 合并逻辑）  
**涉及文件：** `YooUiAssetLoader.cs`（扩展）, `FairyGuiWindowFactory.cs`（修改）

**内容：** 扩展 YooUiAssetLoader 支持 FairyGUI UIPackage 加载，FairyGuiWindowFactory 在创建窗口前加载对应的 UIPackage，验证 LRU 缓存与 inflight 合并机制的协同工作。

**接口契约：**

| 接口 | 签名 | 行为 |
|------|------|------|
| `YooUiAssetLoader.LoadPackageAsync` | `UniTask<string> LoadPackageAsync(string packageName, CancellationToken)` | 加载 FairyGUI UIPackage，返回包名 |
| `YooUiAssetLoader.UnloadPackage` | `void UnloadPackage(string packageName)` | 卸载 FairyGUI UIPackage |
| `FairyGuiWindowFactory._loadedPackages` | `HashSet<string>` 字段 | 跟踪已加载的包，避免重复加载 |

**数据契约：**

**UIPackage 生命周期：**
- 包加载：首次创建该包中的窗口时加载
- 包卸载：由 Asset 模块的引用计数管理，WindowManager 不主动卸载
- 包复用：`_loadedPackages` 跟踪已加载的包，避免重复调用 `AddPackage`

**与 IAssetManager 的关系：**
- 保留 YooUiAssetLoader，不移除
- IAssetManager 暂不扩展（避免影响 Asset 模块）
- 添加注释说明未来可迁移到 IAssetManager（当 Asset 模块支持 UIPackage 时）

**行为契约：**

**LoadPackageAsync 流程：**
1. 通过 YooAsset 的 ResourcePackage.LoadAssetAsync 加载 UIPackage 资源
2. 调用 `UIPackage.AddPackage(bytes)` 或 `UIPackage.AddPackage(assetBundle)`
3. 返回包名，存储到 `_loadedPackages`

**FairyGuiWindowFactory.CreateAsync 流程：**
1. `_registry.TryGetMetadata(request.Id, out var metadata)`，获取 PackageName 和 ComponentName
2. 如果 `!_loadedPackages.Contains(metadata.PackageName)`：
   - 调用 `await _loader.LoadPackageAsync(metadata.PackageName, cancellationToken)`
   - `_loadedPackages.Add(metadata.PackageName)`
3. 调用 `await _loader.LoadPrefabAsync(address, cancellationToken)` 加载 GameObject
4. 从 GameObject 获取 GComponent，创建 FairyGuiWindowView
5. 返回视图

**并发优化验证：**
- 现有 `_inflight` 机制与 LRU 缓存协同工作
- 如果窗口在缓存中，直接返回，不进入 inflight
- 如果窗口不在缓存且正在加载（inflight），合并请求
- 确保一个窗口在同一时刻只有一个加载任务

**验收标准：**

- [ ] 首次打开窗口时，FairyGUI UIPackage 被正确加载
- [ ] 同一包中的多个窗口共享 UIPackage（只加载一次）
- [ ] 并发打开同一窗口时，UIPackage 和 GameObject 都只加载一次
- [ ] 缓存命中时不会重复加载 UIPackage 或 GameObject
- [ ] YooUiAssetLoader 的错误处理与现有代码一致（加载失败抛出异常）
- [ ] 所有窗口资源加载通过 YooUiAssetLoader 完成（FRD 验收条件 #7 满足）

**回归风险评估：**

- **影响范围：** 低 - YooUiAssetLoader 新增方法，FairyGuiWindowFactory 修改实现
- **缓解措施：**
  - 保留 YooUiAssetLoader，不破坏现有接口
  - LoadPackageAsync 是新增方法，不影响现有调用方
  - FairyGuiWindowFactory 的修改集中在 CreateAsync 内部，对 WindowManager 透明
  - 添加集成测试验证包加载逻辑

---

## 切片依赖图

```
切片 A (窗口注册) - 低风险
  └─┬─> 切片 C (分组互斥+层级)
    │
切片 B (WindowRequest 扩展) - 高风险
  ├──> 切片 C (分组互斥+层级) - 中风险
  └──> 切片 D (LRU缓存+延迟释放) - 高风险
         └──> 切片 E (资源加载) - 低风险

实施顺序：
1. 切片 A 和 B 并行（无相互依赖）
2. 切片 B 优先验证（高风险 Equals/GetHashCode）
3. 切片 C（依赖 A+B）
4. 切片 D（依赖 B+C）
5. 切片 E（依赖 D）
```

## 关键接口

### 切片 A 暴露给切片 C

```csharp
// 窗口注册接口
public interface IWindowRegistry
{
    void Register(WindowId id, string packageName, string componentName, string group, WindowLayer layer);
    bool TryGetMetadata(WindowId id, out WindowMetadata metadata);
}

public readonly struct WindowMetadata
{
    public string PackageName { get; }
    public string ComponentName { get; }
    public string Group { get; }
    public WindowLayer Layer { get; }
}
```

### 切片 B 暴露给切片 C/D

```csharp
// WindowRequest 扩展
public readonly struct WindowRequest : IEquatable<WindowRequest>
{
    public WindowId Id { get; }
    public WindowOpenOptions Options { get; }
    public string Group { get; }      // 新增
    public object Context { get; }    // 新增
    
    public WindowRequest(WindowId id, WindowOpenOptions options, string group, object context);
    public bool Equals(WindowRequest other);
    public override int GetHashCode();
}
```

### 切片 C 暴露给切片 D

```csharp
// WindowManager 组互斥与层级管理
public sealed class WindowManager
{
    private string _currentActiveGroup;
    
    private void CloseWindowsInGroup(string group);
    private bool IsOverlayGroup(string group);
}

// sortingOrder 管理
public sealed class WindowLayerSortingOrderManager
{
    public int AllocateSortingOrder(WindowLayer layer);
    public void ResetLayerIfNeeded(WindowLayer layer);
}
```

### 切片 D 暴露给切片 E

```csharp
// LRU 缓存
public sealed class WindowManager
{
    private readonly Dictionary<WindowRequest, CachedWindowEntry> _cache;
    private readonly LinkedList<WindowRequest> _cacheAccessOrder;
    
    private void AddToCache(WindowRequest request, IWindowView view);
    private void EvictLeastRecentlyUsed();
    private UniTaskVoid ScheduleDelayedRelease(WindowRequest request, CancellationToken ct);
}

internal sealed class CachedWindowEntry
{
    public IWindowView View { get; }
    public CancellationTokenSource ReleaseCts { get; }
}
```

### 切片 E 对外接口

```csharp
// YooUiAssetLoader 扩展
public sealed class YooUiAssetLoader : IUiAssetLoader
{
    public async UniTask<string> LoadPackageAsync(string packageName, CancellationToken cancellationToken);
    public void UnloadPackage(string packageName);
}
```

## 回归风险评估

| 影响范围 | 风险等级 | 缓解措施 |
|---------|---------|----------|
| WindowRequest 结构变更 | 高 | 向后兼容构造函数，运行完整测试套件 |
| WindowManager.OpenAsync 逻辑变更 | 高 | 缓存逻辑透明，增加集成测试覆盖缓存+inflight 协同 |
| FairyGuiWindowView sortingOrder 变更 | 中 | 动态值向后兼容，层级关系保持不变 |
| IWindowRegistry 新增依赖 | 低 | 通过 VContainer 注入，现有代码不感知 |
| YooUiAssetLoader 扩展 | 低 | 新增方法，不影响现有调用方 |

**整体缓解策略：**
1. 切片 B 完成后立即运行所有现有单元测试，验证 WindowRequest 变更无破坏性
2. 切片 D 完成后运行性能测试，监控 10 个窗口缓存的内存占用（目标 < 500 MB）
3. 切片 E 完成后运行完整集成测试，验证缓存、inflight、UIPackage 加载三者协同
4. 所有切片完成后，手动测试组切换、窗口复用、延迟释放等关键场景
5. 在业务层添加示例代码，验证 IWindowRegistry 注册流程的易用性


## 切片间契约总结

| 切片对 | 接口契约 | 数据流向 | 职责边界 |
|--------|----------|----------|----------|
| A → C | IWindowRegistry.TryGetMetadata → WindowManager | WindowId → WindowMetadata (Group, Layer) | A 提供元数据查询，C 消费元数据进行组互斥判断 |
| B → C | WindowRequest.Group 字段 → WindowManager | WindowRequest 携带 Group 信息用于互斥判断 | B 扩展数据结构，C 使用 Group 字段实现互斥逻辑 |
| B → D | WindowRequest 作为缓存 key → _cache Dictionary | WindowRequest 的 Equals/GetHashCode 支持缓存查询 | B 保证唯一性判断正确，D 依赖此判断实现缓存 |
| C → D | WindowManager.Close 将窗口加入缓存 | IWindowView 实例 → _cache → 延迟释放队列 | C 负责关闭窗口，D 负责缓存和延迟释放 |
| D → E | 缓存命中时跳过资源加载 | _cache 命中 → 直接返回 IWindowView (不调用 Factory) | D 提供缓存复用机制，E 验证资源加载不重复触发 |
| A → E | IWindowRegistry 提供 PackageName → Factory | WindowId → PackageName → UIPackage 加载 | A 提供包名映射，E 根据包名加载 UIPackage |

## 技术债务与未来优化

| 项目 | 当前方案 | 理想方案 | 时机 |
|------|---------|----------|------|
| YooUiAssetLoader 保留 | 扩展 LoadPackageAsync 方法 | 统一到 IAssetManager，移除 YooUiAssetLoader | 当 Asset 模块支持 UIPackage 加载时 |
| 单一 _gate 锁 | 保护所有共享状态（_opened, _inflight, _cache） | 细粒度锁（读写锁或分段锁） | 性能测试发现锁竞争瓶颈时 |
| Context 类型安全 | object 类型，依赖调用方正确实现 Equals | 泛型约束或辅助类（ContextWrapper<T>） | 如果发现大量 Context Equals 错误时 |
| Overlay 组标识 | null 或空字符串 | 枚举类型或专用常量类 | 如果需要多种特殊组类型时 |
| sortingOrder 重置 | 超过 900 时重置该层所有窗口 | 按需重置（只有冲突时才重置） | 如果发现重置开销过大时 |

## 附录：设计决策记录

**决策 A1: 为什么选择集成式 LRU 缓存而非独立组件？**

理由：
1. WindowManager 已有 _gate 锁保护 _opened 和 _inflight，复用此锁无额外开销
2. 缓存操作（查询、淘汰）与 OpenAsync/Close 流程紧密耦合，分离反而增加协调复杂度
3. 代码调研显示现有锁粒度已足够轻量，临界区很小，不会成为瓶颈
4. 单一职责原则在此场景下的收益不如简单性

权衡：测试隔离性下降，但集成测试可覆盖真实场景。

**决策 A2: 为什么保留 YooUiAssetLoader 而不统一到 IAssetManager？**

理由：
1. IAssetManager 当前只支持 `LoadAsync<T> where T : UnityEngine.Object`
2. FairyGUI UIPackage 不是 UnityEngine.Object，需要特殊处理（AddPackage）
3. 扩展 IAssetManager 会影响 Asset 模块，超出本功能范围
4. YooUiAssetLoader 已经过验证，扩展一个方法风险最低

权衡：与 FRD 决策 #10 的理想状态有差距，但务实选择。添加注释标记为未来重构点。

**决策 A3: 为什么 WindowRequest 保持 struct 而非改为 class？**

理由：
1. struct 作为 Dictionary key 性能更优（值比较，避免引用比较和 GC）
2. 改为 class 会破坏所有现有调用方（50+ 符号），测试成本高
3. 添加 Group 和 Context 字段不影响 struct 语义（仍然是不可变值类型）
4. Context 使用 object 类型虽然有装箱开销，但打开窗口是低频操作，可接受

权衡：Context 类型安全性下降，但文档明确要求调用方正确实现 Equals。

**决策 A4: 为什么选择主动追踪 _currentActiveGroup 而非查询式互斥？**

理由：
1. O(1) 判断新窗口是否需要触发组切换，查询式需要 O(n) 遍历 _opened
2. 状态显式，易于调试（可直接查看当前活跃组）
3. 组切换是低频操作（FRD 假设 < 1/sec），状态维护开销可忽略
4. 测试友好（可直接断言 _currentActiveGroup 的值）

权衡：增加一个状态字段，但换来清晰的语义和性能优势。

---

**设计文档完成日期：** 2026-06-23  
**预估实现工作量：** 8-11 天（与 FRD 一致）  
**下游技能：** mz-plan（将设计转化为分步实现计划）

