# 窗口管理增强 实现计划

> 日期: 2026-06-23 | 状态: 草稿
> 上游设计: .mozi/artifacts/window-manager/designs/2026-06-23-window-manager-design.md
> 上游 FRD: .mozi/artifacts/window-manager/discover/2026-06-23-window-manager-frd.md
> 上游调研: .mozi/artifacts/window-manager/research/2026-06-23-window-manager-research.md

## 上游产出引用

### 来自 Design

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 切片 A: 窗口注册与元数据管理 | 任务 1-4 |
| 切片 B: WindowRequest 扩展与唯一性 | 任务 5-8 |
| 切片 C: 分组互斥与动态层级管理 | 任务 9-15 |
| 切片 D: LRU 缓存与延迟释放 | 任务 16-21 |
| 切片 E: 资源加载统一与并发优化 | 任务 22-25 |
| 文件地图: 10 个文件（3 新增 + 7 修改） | 任务 1-25 覆盖 |
| 架构决策: 集成式 LRU 缓存 | 任务 16-21 |
| 架构决策: 保留 YooUiAssetLoader | 任务 22-23 |
| 架构决策: 主动追踪 _currentActiveGroup | 任务 11-13 |

### 来自 FRD

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 验收条件: 5 层窗口层级 | 任务 14 sortingOrder 动态分配 |
| 验收条件: 组间互斥 + Overlay 特殊处理 | 任务 11-13 分组互斥逻辑 |
| 验收条件: 最多缓存 10 个窗口 | 任务 18 LRU 淘汰逻辑 |
| 验收条件: 30 秒延迟释放 | 任务 19-20 延迟释放定时器 |
| 验收条件: 窗口复用时触发 OnOpen | 任务 21 缓存命中流程 |
| 决策 #22: IWindowRegistry 注册接口 | 任务 1-4 |

### 来自 Research

| 引用内容 | 如何转化为计划步骤 |
|---------|-------------------|
| 现有 _gate 锁机制 | 任务 16-21 复用该锁保护缓存 |
| WindowRequest 是 readonly struct | 任务 5-8 保持 struct，扩展字段 |
| inflight 合并机制成熟 | 任务 21 验证与缓存协同 |
| UiAssetLease 生命周期管理 | 任务 19-20 缓存持有 Lease |

## 目标

增强 Unity UI 窗口管理系统，提供层级管理、窗口分组互斥、LRU 缓存和延迟释放机制，支持复杂多窗口业务场景并优化性能。

## 架构

基于现有 WindowManager 扩展：集成式 LRU 缓存（Dictionary + LinkedList），每窗口独立延迟释放定时器（UniTask.Delay），主动追踪当前活跃组（_currentActiveGroup），动态 sortingOrder 分配（每层计数器）。保留 YooUiAssetLoader 并扩展 UIPackage 加载能力。

## 技术栈

- Unity 2022+ / C# 10
- FairyGUI 5.x
- YooAsset 2.x
- UniTask
- VContainer
- NUnit（测试框架）

## 文件清单

| 文件 | 操作 | 职责 | 所属任务 |
|------|------|------|----------|
| `UnityProject/Assets/Change/Runtime/UI/Abstractions/IWindowRegistry.cs` | 创建 | 窗口注册接口定义 | 任务 1 |
| `UnityProject/Assets/Change/Runtime/UI/Core/WindowMetadata.cs` | 创建 | 窗口元数据结构 | 任务 2 |
| `UnityProject/Assets/Change/Runtime/UI/Core/WindowRegistry.cs` | 创建 | 窗口注册表实现 | 任务 3-4 |
| `UnityProject/Assets/Change/Runtime/UI/Core/WindowRequest.cs` | 修改 | 扩展 Group 和 Context 字段 | 任务 5-8 |
| `UnityProject/Assets/Change/Runtime/UI/FairyGui/WindowLayerSortingOrderManager.cs` | 创建 | sortingOrder 计数器管理 | 任务 9-10 |
| `UnityProject/Assets/Change/Runtime/UI/Core/WindowManager.cs` | 修改 | 添加组互斥、LRU 缓存逻辑 | 任务 11-13, 16-21 |
| `UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowView.cs` | 修改 | 动态 sortingOrder 设置 | 任务 14 |
| `UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowFactory.cs` | 修改 | 查询元数据、加载 UIPackage | 任务 15, 24-25 |
| `UnityProject/Assets/Change/Runtime/UI/Core/CachedWindowEntry.cs` | 创建 | LRU 缓存条目 | 任务 16-17 |
| `UnityProject/Assets/Change/Runtime/UI/Loading/YooUiAssetLoader.cs` | 修改 | 扩展 LoadPackageAsync 方法 | 任务 22-23 |


## 任务依赖图

```
切片 A（任务 1-4）：窗口注册
  └─┬─> 切片 C（任务 15）：Factory 查询元数据
    │   切片 C（任务 11-13）：组互斥
    │
切片 B（任务 5-8）：WindowRequest 扩展
  ├──> 切片 C（任务 11-13）：组互斥逻辑
  └──> 切片 D（任务 16-21）：LRU 缓存

切片 C（任务 9-15）：分组互斥与层级
  └──> 切片 D（任务 16-21）：缓存集成

切片 D（任务 16-21）：LRU 缓存
  └──> 切片 E（任务 22-25）：资源加载

实施顺序：
1. 任务 1-4（切片 A）和任务 5-8（切片 B）可并行
2. 任务 5-8 优先（高风险 Equals/GetHashCode）
3. 任务 9-15（切片 C，依赖 A+B）
4. 任务 16-21（切片 D，依赖 B+C）
5. 任务 22-25（切片 E，依赖 D）
```

## 风险评估

| 风险（来源） | 影响 | 计划中的缓解措施 |
|-------------|------|-----------------|
| WindowRequest Equals/GetHashCode 实现错误（Design 切片 B） | 缓存失效，窗口重复加载 | 任务 7-8 充分的单元测试覆盖边界情况 |
| WindowRequest 变更破坏现有代码（Design 回归评估） | 50+ 上游符号编译失败 | 任务 6 向后兼容构造函数，任务 8 运行完整测试套件 |
| LRU 缓存淘汰逻辑错误（Design 切片 D） | 内存泄漏或过早释放 | 任务 18 精确测试淘汰边界，任务 20 测试定时器取消 |
| 延迟释放定时器未正确取消（Design 切片 D） | 资源泄漏，Dispose 重复调用 | 任务 20-21 测试缓存命中时取消定时器 |
| 组互斥遍历性能问题（Design 切片 C） | 关闭大量窗口时卡顿 | 任务 13 测试关闭 20 个窗口的性能 |
| sortingOrder 溢出（Design 切片 C） | 层级错乱 | 任务 10 测试计数器超过 900 的重置逻辑 |
| UIPackage 加载失败（Design 切片 E） | 窗口无法创建 | 任务 23 测试加载失败时的异常处理 |
| 缓存与 inflight 协同问题（Design 切片 D/E） | 并发打开导致重复加载 | 任务 25 集成测试验证三者协同 |

---

## 任务

### 任务 1: IWindowRegistry 接口定义

**覆盖的上游需求：** Design 切片 A - 窗口注册接口  
**文件：**
- 创建：`UnityProject/Assets/Change/Runtime/UI/Abstractions/IWindowRegistry.cs`
- 测试：`UnityProject/Assets/Change/Tests/Runtime/UI/WindowRegistryTests.cs`

- [ ] **步骤 1: 创建接口文件**

```csharp
namespace Change.Runtime.UI.Abstractions
{
    public interface IWindowRegistry
    {
        void Register(WindowId id, string packageName, string componentName, string group, WindowLayer layer);
        bool TryGetMetadata(WindowId id, out WindowMetadata metadata);
    }
}
```

- [ ] **步骤 2: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/UI/Abstractions/IWindowRegistry.cs
git commit -m "feat(ui): add IWindowRegistry interface"
```

---

### 任务 2: WindowMetadata 结构体定义

**覆盖的上游需求：** Design 切片 A - 窗口元数据结构  
**文件：**
- 创建：`UnityProject/Assets/Change/Runtime/UI/Core/WindowMetadata.cs`

- [ ] **步骤 1: 创建 WindowMetadata 结构体**

```csharp
namespace Change.Runtime.UI.Core
{
    public readonly struct WindowMetadata
    {
        public string PackageName { get; }
        public string ComponentName { get; }
        public string Group { get; }
        public WindowLayer Layer { get; }

        public WindowMetadata(string packageName, string componentName, string group, WindowLayer layer)
        {
            PackageName = packageName;
            ComponentName = componentName;
            Group = group;
            Layer = layer;
        }
    }
}
```

- [ ] **步骤 2: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/UI/Core/WindowMetadata.cs
git commit -m "feat(ui): add WindowMetadata struct"
```

---

### 任务 3: WindowRegistry 实现与测试

**覆盖的上游需求：** Design 切片 A - 窗口注册表实现  
**依赖：** 任务 1, 2  
**文件：**
- 创建：`UnityProject/Assets/Change/Runtime/UI/Core/WindowRegistry.cs`
- 测试：`UnityProject/Assets/Change/Tests/Runtime/UI/WindowRegistryTests.cs`

- [ ] **步骤 1: 编写失败的测试 - 注册和查询**

```csharp
using NUnit.Framework;
using Change.Runtime.UI.Core;
using Change.Runtime.UI.Abstractions;

namespace Change.Tests.Runtime.UI
{
    public class WindowRegistryTests
    {
        [Test]
        public void Register_AndTryGetMetadata_ReturnsTrue()
        {
            var registry = new WindowRegistry();
            var id = new WindowId("TestWindow");
            
            registry.Register(id, "TestPackage", "TestComponent", "TestGroup", WindowLayer.Normal);
            
            bool found = registry.TryGetMetadata(id, out var metadata);
            
            Assert.IsTrue(found);
            Assert.AreEqual("TestPackage", metadata.PackageName);
            Assert.AreEqual("TestComponent", metadata.ComponentName);
            Assert.AreEqual("TestGroup", metadata.Group);
            Assert.AreEqual(WindowLayer.Normal, metadata.Layer);
        }

        [Test]
        public void TryGetMetadata_NotRegistered_ReturnsFalse()
        {
            var registry = new WindowRegistry();
            var id = new WindowId("NonExistent");
            
            bool found = registry.TryGetMetadata(id, out var metadata);
            
            Assert.IsFalse(found);
        }

        [Test]
        public void Register_DuplicateId_ThrowsException()
        {
            var registry = new WindowRegistry();
            var id = new WindowId("TestWindow");
            
            registry.Register(id, "Pkg1", "Comp1", "Group1", WindowLayer.Normal);
            
            Assert.Throws<InvalidOperationException>(() => 
                registry.Register(id, "Pkg2", "Comp2", "Group2", WindowLayer.Popup));
        }

        [Test]
        public void Register_NullGroup_StoresNull()
        {
            var registry = new WindowRegistry();
            var id = new WindowId("OverlayWindow");
            
            registry.Register(id, "Pkg", "Comp", null, WindowLayer.System);
            
            bool found = registry.TryGetMetadata(id, out var metadata);
            
            Assert.IsTrue(found);
            Assert.IsNull(metadata.Group);
        }
    }
}
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
cd UnityProject
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -runTests -batchmode -projectPath . \
  -testPlatform EditMode \
  -testFilter "Change.Tests.Runtime.UI.WindowRegistryTests" \
  -logFile - 2>&1 | grep -E "(FAIL|PASS|WindowRegistryTests)"
```

预期：所有测试 FAIL，报错 "WindowRegistry type not found"

- [ ] **步骤 3: 实现 WindowRegistry**

```csharp
using System;
using System.Collections.Generic;
using Change.Runtime.UI.Abstractions;

namespace Change.Runtime.UI.Core
{
    public sealed class WindowRegistry : IWindowRegistry
    {
        private readonly Dictionary<WindowId, WindowMetadata> _metadata = new();

        public void Register(WindowId id, string packageName, string componentName, string group, WindowLayer layer)
        {
            if (_metadata.ContainsKey(id))
            {
                throw new InvalidOperationException($"Window '{id}' is already registered.");
            }

            _metadata[id] = new WindowMetadata(packageName, componentName, group, layer);
        }

        public bool TryGetMetadata(WindowId id, out WindowMetadata metadata)
        {
            return _metadata.TryGetValue(id, out metadata);
        }
    }
}
```

- [ ] **步骤 4: 运行测试验证通过**

```bash
cd UnityProject
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -runTests -batchmode -projectPath . \
  -testPlatform EditMode \
  -testFilter "Change.Tests.Runtime.UI.WindowRegistryTests" \
  -logFile - 2>&1 | grep -E "(PASS|FAIL)"
```

预期：所有测试 PASS

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/UI/Core/WindowRegistry.cs \
        UnityProject/Assets/Change/Tests/Runtime/UI/WindowRegistryTests.cs
git commit -m "feat(ui): implement WindowRegistry with tests"
```

---

### 任务 4: VContainer 注册示例

**覆盖的上游需求：** FRD 验收条件 - 业务层可批量注册窗口  
**依赖：** 任务 3  
**文件：**
- 创建：`UnityProject/Assets/Change/Samples/UI/WindowRegistrationExample.cs`

- [ ] **步骤 1: 创建业务层注册示例**

```csharp
using VContainer;
using VContainer.Unity;
using Change.Runtime.UI.Core;
using Change.Runtime.UI.Abstractions;

namespace Change.Samples.UI
{
    public class WindowRegistrationExample : IStartable
    {
        private readonly IWindowRegistry _registry;

        public WindowRegistrationExample(IWindowRegistry registry)
        {
            _registry = registry;
        }

        void IStartable.Start()
        {
            // 注册商店窗口（普通组）
            _registry.Register(
                new WindowId("ShopWindow"),
                "UI_Shop",
                "ShopMain",
                "Shop",
                WindowLayer.Normal
            );

            // 注册确认框（Overlay 组）
            _registry.Register(
                new WindowId("ConfirmDialog"),
                "UI_Common",
                "ConfirmDialog",
                null,  // Overlay 组
                WindowLayer.Popup
            );

            // 注册公会窗口（普通组）
            _registry.Register(
                new WindowId("GuildWindow"),
                "UI_Guild",
                "GuildMain",
                "Guild",
                WindowLayer.Normal
            );
        }
    }

    public class UIRegistrationScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IWindowRegistry, WindowRegistry>(Lifetime.Singleton);
            builder.RegisterEntryPoint<WindowRegistrationExample>();
        }
    }
}
```

- [ ] **步骤 2: 编译验证**

```bash
cd UnityProject
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -projectPath . \
  -executeMethod UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation \
  -logFile - 2>&1 | grep -E "(error|warning|Success)"
```

预期：编译成功，无错误

- [ ] **步骤 3: Commit**

```bash
git add UnityProject/Assets/Change/Samples/UI/WindowRegistrationExample.cs
git commit -m "feat(ui): add WindowRegistry VContainer example"
```

---

### 任务 5: WindowRequest 添加 Group 和 Context 字段

**覆盖的上游需求：** Design 切片 B - WindowRequest 扩展  
**文件：**
- 修改：`UnityProject/Assets/Change/Runtime/UI/Core/WindowRequest.cs`

- [ ] **步骤 1: 读取现有 WindowRequest 结构**

```bash
cat UnityProject/Assets/Change/Runtime/UI/Core/WindowRequest.cs
```

- [ ] **步骤 2: 添加 Group 和 Context 字段**

在 WindowRequest 结构体中添加：

```csharp
public readonly struct WindowRequest : IEquatable<WindowRequest>
{
    // 现有字段
    public WindowId Id { get; }
    public WindowOpenOptions Options { get; }
    
    // 新增字段
    public string Group { get; }
    public object Context { get; }

    // 向后兼容的构造函数（不传 Group 和 Context）
    public WindowRequest(WindowId id, WindowOpenOptions options)
        : this(id, options, null, null)
    {
    }

    // 新构造函数
    public WindowRequest(WindowId id, WindowOpenOptions options, string group, object context)
    {
        Id = id;
        Options = options;
        Group = group;
        Context = context;
    }

    // 现有 Equals 和 GetHashCode 方法保持不变（下个任务更新）
    public bool Equals(WindowRequest other) { /* 现有实现 */ }
    public override bool Equals(object obj) { /* 现有实现 */ }
    public override int GetHashCode() { /* 现有实现 */ }
}
```

- [ ] **步骤 3: 编译验证向后兼容性**

```bash
cd UnityProject
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -projectPath . \
  -executeMethod UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation \
  -logFile - 2>&1 | grep -E "(error|warning)"
```

预期：编译成功，现有代码无破坏

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/UI/Core/WindowRequest.cs
git commit -m "feat(ui): add Group and Context fields to WindowRequest"
```

---

### 任务 6: WindowRequest 更新 Equals 逻辑

**覆盖的上游需求：** Design 切片 B - Context 参与唯一性判断  
**依赖：** 任务 5  
**文件：**
- 修改：`UnityProject/Assets/Change/Runtime/UI/Core/WindowRequest.cs`

- [ ] **步骤 1: 更新 Equals 方法**

```csharp
public bool Equals(WindowRequest other)
{
    // Id 必须相同
    if (!Id.Equals(other.Id))
        return false;

    // 如果不允许多实例，只比较 Id
    if (!Options.AllowMultipleInstances)
        return true;

    // 允许多实例时，比较 InstanceId 和 Context
    if (Options.InstanceId != other.Options.InstanceId)
        return false;

    // Context 参与比较
    if (Context == null && other.Context == null)
        return true;
    
    if (Context == null || other.Context == null)
        return false;

    return Context.Equals(other.Context);
}
```

- [ ] **步骤 2: 更新 GetHashCode 方法**

```csharp
public override int GetHashCode()
{
    if (!Options.AllowMultipleInstances)
    {
        return Id.GetHashCode();
    }

    int hash = Id.GetHashCode();
    hash = (hash * 397) ^ Options.InstanceId.GetHashCode();
    
    if (Context != null)
    {
        hash = (hash * 397) ^ Context.GetHashCode();
    }

    return hash;
}
```

- [ ] **步骤 3: 编译验证**

```bash
cd UnityProject
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -quit -batchmode -projectPath . \
  -executeMethod UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation \
  -logFile - 2>&1 | grep -E "(error|warning)"
```

预期：编译成功

- [ ] **步骤 4: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/UI/Core/WindowRequest.cs
git commit -m "feat(ui): update WindowRequest Equals/GetHashCode for Context"
```

---

### 任务 7: WindowRequest Equals/GetHashCode 单元测试

**覆盖的上游需求：** Design 切片 B 验收标准 - Equals/GetHashCode 边界测试  
**依赖：** 任务 6  
**文件：**
- 测试：`UnityProject/Assets/Change/Tests/Runtime/UI/WindowRequestTests.cs`

- [ ] **步骤 1: 编写 Equals/GetHashCode 测试**

```csharp
using NUnit.Framework;
using Change.Runtime.UI.Core;

namespace Change.Tests.Runtime.UI
{
    public class WindowRequestTests
    {
        [Test]
        public void Equals_SameIdAndContext_ReturnsTrue()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions { AllowMultipleInstances = true, InstanceId = 1 };
            var context = new { ItemId = 123 };

            var req1 = new WindowRequest(id, options, "Group", context);
            var req2 = new WindowRequest(id, options, "Group", context);

            Assert.IsTrue(req1.Equals(req2));
            Assert.AreEqual(req1.GetHashCode(), req2.GetHashCode());
        }

        [Test]
        public void Equals_SameIdDifferentContext_ReturnsFalse()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions { AllowMultipleInstances = true, InstanceId = 1 };

            var req1 = new WindowRequest(id, options, "Group", new { ItemId = 123 });
            var req2 = new WindowRequest(id, options, "Group", new { ItemId = 456 });

            Assert.IsFalse(req1.Equals(req2));
        }

        [Test]
        public void Equals_ContextNull_HandlesCorrectly()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions { AllowMultipleInstances = true, InstanceId = 1 };

            var req1 = new WindowRequest(id, options, "Group", null);
            var req2 = new WindowRequest(id, options, "Group", null);

            Assert.IsTrue(req1.Equals(req2));
            Assert.AreEqual(req1.GetHashCode(), req2.GetHashCode());
        }

        [Test]
        public void Equals_OneContextNullOneNot_ReturnsFalse()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions { AllowMultipleInstances = true, InstanceId = 1 };

            var req1 = new WindowRequest(id, options, "Group", null);
            var req2 = new WindowRequest(id, options, "Group", new { ItemId = 123 });

            Assert.IsFalse(req1.Equals(req2));
        }

        [Test]
        public void Equals_DisallowMultipleInstances_IgnoresContext()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions { AllowMultipleInstances = false };

            var req1 = new WindowRequest(id, options, "Group", new { ItemId = 123 });
            var req2 = new WindowRequest(id, options, "Group", new { ItemId = 456 });

            Assert.IsTrue(req1.Equals(req2));
        }

        [Test]
        public void Equals_GroupDoesNotParticipate()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions { AllowMultipleInstances = true, InstanceId = 1 };

            var req1 = new WindowRequest(id, options, "Group1", 123);
            var req2 = new WindowRequest(id, options, "Group2", 123);

            Assert.IsTrue(req1.Equals(req2));
        }

        [Test]
        public void GetHashCode_ValueTypeContext_WorksCorrectly()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions { AllowMultipleInstances = true, InstanceId = 1 };

            var req1 = new WindowRequest(id, options, "Group", 123);
            var req2 = new WindowRequest(id, options, "Group", 123);

            Assert.AreEqual(req1.GetHashCode(), req2.GetHashCode());
        }

        [Test]
        public void GetHashCode_StringContext_WorksCorrectly()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions { AllowMultipleInstances = true, InstanceId = 1 };

            var req1 = new WindowRequest(id, options, "Group", "context-string");
            var req2 = new WindowRequest(id, options, "Group", "context-string");

            Assert.AreEqual(req1.GetHashCode(), req2.GetHashCode());
        }
    }
}
```

- [ ] **步骤 2: 运行测试验证通过**

```bash
cd UnityProject
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -runTests -batchmode -projectPath . \
  -testPlatform EditMode \
  -testFilter "Change.Tests.Runtime.UI.WindowRequestTests" \
  -logFile - 2>&1 | grep -E "(PASS|FAIL)"
```

预期：所有测试 PASS

- [ ] **步骤 3: Commit**

```bash
git add UnityProject/Assets/Change/Tests/Runtime/UI/WindowRequestTests.cs
git commit -m "test(ui): add WindowRequest Equals/GetHashCode tests"
```

---

### 任务 8: 运行完整测试套件验证无破坏性变更

**覆盖的上游需求：** Design 切片 B 回归风险缓解  
**依赖：** 任务 7  
**文件：** 无（验证任务）

- [ ] **步骤 1: 运行所有现有单元测试**

```bash
cd UnityProject
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -runTests -batchmode -projectPath . \
  -testPlatform EditMode \
  -logFile - 2>&1 | tee test-results.log
```

预期：所有测试 PASS，无新增失败

- [ ] **步骤 2: 检查测试结果**

```bash
grep -E "(Test Run Summary|tests passed)" test-results.log
```

预期：输出类似 "123 tests passed, 0 failed"

- [ ] **步骤 3: 如果有失败，分析并修复**

如果有测试失败：
1. 检查失败测试是否因 WindowRequest 变更导致
2. 更新测试代码使用新的构造函数
3. 重新运行测试直到全部通过

- [ ] **步骤 4: 记录验证结果**

```bash
echo "WindowRequest 扩展验证完成，所有测试通过" >> .mozi/artifacts/window-manager/verification.log
git add .mozi/artifacts/window-manager/verification.log
git commit -m "docs(ui): verify WindowRequest changes pass all tests"
```

---

### 任务 9: WindowLayerSortingOrderManager 实现

**覆盖的上游需求：** Design 切片 C - sortingOrder 计数器管理  
**文件：**
- 创建：`UnityProject/Assets/Change/Runtime/UI/FairyGui/WindowLayerSortingOrderManager.cs`
- 测试：`UnityProject/Assets/Change/Tests/Runtime/UI/WindowLayerSortingOrderManagerTests.cs`

- [ ] **步骤 1: 编写失败的测试**

```csharp
using NUnit.Framework;
using Change.Runtime.UI.FairyGui;

namespace Change.Tests.Runtime.UI
{
    public class WindowLayerSortingOrderManagerTests
    {
        [Test]
        public void AllocateSortingOrder_FirstCall_ReturnsBaseValuePlusOne()
        {
            var manager = new WindowLayerSortingOrderManager();
            
            int order = manager.AllocateSortingOrder(WindowLayer.Normal);
            
            Assert.AreEqual(1001, order); // Normal base = 1000, first allocation = 1001
        }

        [Test]
        public void AllocateSortingOrder_MultipleCalls_Increments()
        {
            var manager = new WindowLayerSortingOrderManager();
            
            int order1 = manager.AllocateSortingOrder(WindowLayer.Normal);
            int order2 = manager.AllocateSortingOrder(WindowLayer.Normal);
            int order3 = manager.AllocateSortingOrder(WindowLayer.Normal);
            
            Assert.AreEqual(1001, order1);
            Assert.AreEqual(1002, order2);
            Assert.AreEqual(1003, order3);
        }

        [Test]
        public void AllocateSortingOrder_DifferentLayers_IndependentCounters()
        {
            var manager = new WindowLayerSortingOrderManager();
            
            int normal1 = manager.AllocateSortingOrder(WindowLayer.Normal);
            int popup1 = manager.AllocateSortingOrder(WindowLayer.Popup);
            int normal2 = manager.AllocateSortingOrder(WindowLayer.Normal);
            
            Assert.AreEqual(1001, normal1);
            Assert.AreEqual(2001, popup1); // Popup base = 2000
            Assert.AreEqual(1002, normal2);
        }
    }
}
```

- [ ] **步骤 2: 运行测试验证失败**

```bash
cd UnityProject
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -runTests -batchmode -projectPath . \
  -testPlatform EditMode \
  -testFilter "Change.Tests.Runtime.UI.WindowLayerSortingOrderManagerTests" \
  -logFile - 2>&1 | grep -E "(FAIL|PASS)"
```

预期：FAIL，"WindowLayerSortingOrderManager type not found"

- [ ] **步骤 3: 实现 WindowLayerSortingOrderManager**

```csharp
using System.Collections.Generic;

namespace Change.Runtime.UI.FairyGui
{
    public sealed class WindowLayerSortingOrderManager
    {
        private readonly Dictionary<WindowLayer, int> _layerCounters = new();

        public int AllocateSortingOrder(WindowLayer layer)
        {
            int baseValue = GetBaseValue(layer);
            
            if (!_layerCounters.TryGetValue(layer, out int counter))
            {
                counter = 0;
            }

            counter++;
            _layerCounters[layer] = counter;

            return baseValue + counter;
        }

        public void ResetLayerIfNeeded(WindowLayer layer)
        {
            if (_layerCounters.TryGetValue(layer, out int counter) && counter > 900)
            {
                _layerCounters[layer] = 0;
            }
        }

        private int GetBaseValue(WindowLayer layer)
        {
            return layer switch
            {
                WindowLayer.Background => 0,
                WindowLayer.Normal => 1000,
                WindowLayer.Popup => 2000,
                WindowLayer.Guide => 3000,
                WindowLayer.System => 4000,
                _ => 0
            };
        }
    }
}
```

- [ ] **步骤 4: 运行测试验证通过**

```bash
cd UnityProject
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -runTests -batchmode -projectPath . \
  -testPlatform EditMode \
  -testFilter "Change.Tests.Runtime.UI.WindowLayerSortingOrderManagerTests" \
  -logFile - 2>&1 | grep -E "(PASS|FAIL)"
```

预期：所有测试 PASS

- [ ] **步骤 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/UI/FairyGui/WindowLayerSortingOrderManager.cs \
        UnityProject/Assets/Change/Tests/Runtime/UI/WindowLayerSortingOrderManagerTests.cs
git commit -m "feat(ui): implement WindowLayerSortingOrderManager"
```

---

### 任务 10: WindowLayerSortingOrderManager 重置逻辑测试

**覆盖的上游需求：** Design 切片 C 验收标准 - 计数器超过 900 重置  
**依赖：** 任务 9  
**文件：**
- 测试：`UnityProject/Assets/Change/Tests/Runtime/UI/WindowLayerSortingOrderManagerTests.cs`

- [ ] **步骤 1: 添加重置逻辑测试**

```csharp
[Test]
public void ResetLayerIfNeeded_CounterOver900_ResetsToZero()
{
    var manager = new WindowLayerSortingOrderManager();
    
    // 分配 901 次，触发计数器 > 900
    for (int i = 0; i < 901; i++)
    {
        manager.AllocateSortingOrder(WindowLayer.Normal);
    }
    
    manager.ResetLayerIfNeeded(WindowLayer.Normal);
    
    // 下次分配应该从 1001 开始（重置后）
    int nextOrder = manager.AllocateSortingOrder(WindowLayer.Normal);
    Assert.AreEqual(1001, nextOrder);
}

[Test]
public void ResetLayerIfNeeded_CounterUnder900_DoesNotReset()
{
    var manager = new WindowLayerSortingOrderManager();
    
    // 分配 5 次
    for (int i = 0; i < 5; i++)
    {
        manager.AllocateSortingOrder(WindowLayer.Normal);
    }
    
    manager.ResetLayerIfNeeded(WindowLayer.Normal);
    
    // 下次分配应该从 1006 开始（未重置）
    int nextOrder = manager.AllocateSortingOrder(WindowLayer.Normal);
    Assert.AreEqual(1006, nextOrder);
}
```

- [ ] **步骤 2: 运行测试验证通过**

```bash
cd UnityProject
/Applications/Unity/Hub/Editor/2022.3.0f1/Unity.app/Contents/MacOS/Unity \
  -runTests -batchmode -projectPath . \
  -testPlatform EditMode \
  -testFilter "Change.Tests.Runtime.UI.WindowLayerSortingOrderManagerTests.ResetLayerIfNeeded" \
  -logFile - 2>&1 | grep -E "(PASS|FAIL)"
```

预期：所有测试 PASS

- [ ] **步骤 3: Commit**

```bash
git add UnityProject/Assets/Change/Tests/Runtime/UI/WindowLayerSortingOrderManagerTests.cs
git commit -m "test(ui): add sortingOrder reset logic tests"
```

---

### 任务 11-13: WindowManager 分组互斥逻辑（切片 C）

**任务 11:** 添加 _currentActiveGroup 字段和 IsOverlayGroup 方法  
**任务 12:** 实现 CloseWindowsInGroup 方法（遍历 _opened，查询每个 WindowRequest 的 Group，关闭匹配的窗口）  
**任务 13:** 在 OpenAsync 中集成组互斥检查（步骤：查询元数据→检查 Overlay→比较 _currentActiveGroup→调用 CloseWindowsInGroup→更新 _currentActiveGroup）

---

### 任务 14: FairyGuiWindowView 动态 sortingOrder（切片 C）

修改 `SetSortingOrder` 方法和 `BringToFront` 方法，从 WindowLayerSortingOrderManager 获取新的 sortingOrder 并设置到 `_root.sortingOrder`

---

### 任务 15: FairyGuiWindowFactory 集成 IWindowRegistry（切片 C）

在 CreateAsync 开始时调用 `_registry.TryGetMetadata(request.Id, out var metadata)`，获取 PackageName 和 ComponentName 用于后续资源加载

---

### 任务 16-17: CachedWindowEntry 实现（切片 D）

**任务 16:** 创建 CachedWindowEntry 类（包含 IWindowView View 和 CancellationTokenSource ReleaseCts）  
**任务 17:** 添加 Dispose 方法（取消并释放 CancellationTokenSource）

---

### 任务 18: WindowManager LRU 缓存基础结构（切片 D）

添加字段：
- `Dictionary<WindowRequest, CachedWindowEntry> _cache`
- `LinkedList<WindowRequest> _cacheAccessOrder`
- `const int MaxCacheSize = 10`

实现方法：
- `AddToCache(WindowRequest, IWindowView)` - 加入缓存并添加到链表头部
- `EvictLeastRecentlyUsed()` - 从链表尾部移除最旧窗口

---

### 任务 19-20: 延迟释放定时器（切片 D）

**任务 19:** 实现 `ScheduleDelayedRelease(WindowRequest, CancellationToken)` - UniTask.Delay(30000)，到期后从 _cache 移除并调用 View.Dispose()  
**任务 20:** 测试定时器取消逻辑（缓存命中时 ReleaseCts.Cancel() 阻止释放）

---

### 任务 21: WindowManager 缓存命中流程（切片 D）

在 OpenAsync 开始时检查 `_cache.TryGetValue(request, out var entry)`：
- 如果命中：取消定时器，从 _cache 移除，创建 OpenedWindowEntry 加入 _opened，触发 OnOpened，返回 View
- 如果未命中：继续现有逻辑（组互斥→inflight→Factory）

在 Close 方法中调用 `AddToCache` 将窗口加入缓存

---

### 任务 22-23: YooUiAssetLoader 扩展 UIPackage 加载（切片 E）

**任务 22:** 添加 `LoadPackageAsync(string packageName, CancellationToken)` 方法 - 通过 YooAsset 加载 UIPackage 资源并调用 `UIPackage.AddPackage`  
**任务 23:** 添加 `UnloadPackage(string packageName)` 方法 - 调用 `UIPackage.RemovePackage`

测试：加载成功、加载失败异常处理、重复加载不报错

---

### 任务 24: FairyGuiWindowFactory 集成 UIPackage 加载（切片 E）

添加 `HashSet<string> _loadedPackages` 字段

在 CreateAsync 中：
1. 获取 PackageName（任务 15 已实现）
2. 如果 `!_loadedPackages.Contains(packageName)`：调用 `_loader.LoadPackageAsync(packageName)`，添加到 _loadedPackages
3. 继续现有的 GameObject 加载逻辑

---

### 任务 25: 集成测试 - 缓存 + inflight + UIPackage 协同（切片 E）

编写集成测试验证：
1. 并发打开同一窗口，只触发一次资源加载（inflight 合并）
2. 缓存命中时不触发资源加载
3. UIPackage 在同一包的多个窗口间共享
4. 缓存淘汰和延迟释放的完整流程

运行所有测试套件，确认无回归

---

## 完成标准

所有 25 个任务完成后：

- [ ] 所有单元测试和集成测试通过
- [ ] 编译无警告和错误
- [ ] 手动测试关键场景：
  - 组切换关闭旧组窗口
  - Overlay 窗口不触发互斥
  - 窗口关闭后 30 秒内重新打开复用实例
  - 缓存超过 10 个窗口时淘汰最旧的
  - 后打开的窗口自动在同层级上面
- [ ] 业务层注册示例代码可运行
- [ ] 创建最终验证报告

---

## 预估工作量

- 切片 A（任务 1-4）：1 天
- 切片 B（任务 5-8）：1.5 天（高风险，需充分测试）
- 切片 C（任务 9-15）：2.5 天
- 切片 D（任务 16-21）：2.5 天（高风险，缓存逻辑复杂）
- 切片 E（任务 22-25）：1.5 天
- **总计：9 天**（与 FRD 预估 8-11 天一致）

---

**计划编写完成日期：** 2026-06-23  
**下游技能：** mz-implement-subagent（推荐）或 mz-implement

