# Change UI Application Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an MVP application-shell framework on top of CQRS, including app-layer contracts, runtime window management, FairyGUI + YooAsset + UniTask adapters, and one GameScript sample flow.

**Architecture:** Keep strict layering: contracts in `Change.Framework`, engine/middleware adapters in `Change.Runtime`, business implementations in `GameScript`. Use explicit presenter/use-case calls and explicit view updates (no runtime reflection binding). `WindowManager` handles lifecycle/cache/layering, while `YooUiAssetLoader` centralizes async load/release semantics.

**Tech Stack:** Unity 2022.3, C#, Change.Framework (CQRS), FairyGUI, YooAsset 2.3.18, UniTask 2.5.10, Unity Test Framework (EditMode + PlayMode where needed).

---

## Scope Check

This spec is one subsystem (UI application shell) with three layers, but it remains a single coherent implementation plan because all tasks converge on one end-to-end flow (`View -> Presenter -> UseCase/Facade -> CQRS`).

## File Structure (Create/Modify Map)

### Framework (contracts only)

- Create: `UnityProject/Assets/Change/Framework/Application/Abstractions/IUseCase.cs`
- Create: `UnityProject/Assets/Change/Framework/Application/Abstractions/IAppFacade.cs`
- Create: `UnityProject/Assets/Change/Framework/Application/Abstractions/IPresenter.cs`
- Create: `UnityProject/Assets/Change/Framework/UI/Abstractions/WindowLayer.cs`
- Create: `UnityProject/Assets/Change/Framework/UI/Abstractions/WindowState.cs`
- Create: `UnityProject/Assets/Change/Framework/UI/Abstractions/WindowId.cs`
- Create: `UnityProject/Assets/Change/Framework/UI/Abstractions/WindowOpenOptions.cs`

### Runtime (adapters and manager)

- Modify: `UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef` (add package assembly refs)
- Create: `UnityProject/Assets/Change/Runtime/UI/Abstractions/IWindowView.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Abstractions/IWindowFactory.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Abstractions/IUiAssetLoader.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Core/WindowRequest.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Core/WindowManager.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Loading/UiAssetLease.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Loading/YooUiAssetLoader.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowView.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowFactory.cs`

### GameScript (business sample)

- Create: `UnityProject/Assets/GameScript/UI/Inventory/InventoryWindowViewModel.cs`
- Create: `UnityProject/Assets/GameScript/UI/Inventory/InventoryWindowContracts.cs`
- Create: `UnityProject/Assets/GameScript/UI/Inventory/OpenInventoryUseCase.cs`
- Create: `UnityProject/Assets/GameScript/UI/Inventory/InventoryWindowPresenter.cs`

### Tests

- Create: `UnityProject/Assets/Change/Framework/Tests/EditMode/Application/ApplicationContractsTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/WindowManagerCoreTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/YooUiAssetLoaderTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/NoReflectionBindingGuardTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/GameScriptSampleContractTests.cs`

### Docs

- Create: `UnityProject/Assets/Change/Runtime/UI/README.md`

---

### Task 1: Add framework-level app and window contracts

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Application/Abstractions/IUseCase.cs`
- Create: `UnityProject/Assets/Change/Framework/Application/Abstractions/IAppFacade.cs`
- Create: `UnityProject/Assets/Change/Framework/Application/Abstractions/IPresenter.cs`
- Create: `UnityProject/Assets/Change/Framework/UI/Abstractions/WindowLayer.cs`
- Create: `UnityProject/Assets/Change/Framework/UI/Abstractions/WindowState.cs`
- Create: `UnityProject/Assets/Change/Framework/UI/Abstractions/WindowId.cs`
- Create: `UnityProject/Assets/Change/Framework/UI/Abstractions/WindowOpenOptions.cs`
- Test: `UnityProject/Assets/Change/Framework/Tests/EditMode/Application/ApplicationContractsTests.cs`

- [ ] **Step 1: Write the failing contract tests**

```csharp
using System;
using Change.Framework.Application;
using Change.Framework.UI;
using NUnit.Framework;

namespace Change.Framework.Tests.Application
{
    public class ApplicationContractsTests
    {
        private readonly struct DummyRequest
        {
            public DummyRequest(int value) { Value = value; }
            public int Value { get; }
        }

        private sealed class DummyUseCase : IUseCase<DummyRequest, int>
        {
            public int Execute(in DummyRequest request) => request.Value + 1;
        }

        [Test]
        public void WindowId_RejectsNullOrWhiteSpace()
        {
            Assert.Throws<ArgumentException>(() => _ = new WindowId(null));
            Assert.Throws<ArgumentException>(() => _ = new WindowId(string.Empty));
            Assert.Throws<ArgumentException>(() => _ = new WindowId("   "));
        }

        [Test]
        public void WindowId_Equality_UsesValueSemantics()
        {
            var a = new WindowId("Inventory");
            var b = new WindowId("Inventory");

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void WindowOpenOptions_DefaultsToNormalLayerAndReuse()
        {
            var options = WindowOpenOptions.Default;

            Assert.AreEqual(WindowLayer.Normal, options.Layer);
            Assert.IsTrue(options.ReuseIfLoaded);
            Assert.IsFalse(options.AllowMultipleInstances);
            Assert.AreEqual(0, options.InstanceId);
        }

        [Test]
        public void UseCase_ExecutesWithInParameter()
        {
            var useCase = new DummyUseCase();
            var request = new DummyRequest(41);

            var result = useCase.Execute(in request);

            Assert.AreEqual(42, result);
        }
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.Application.ApplicationContractsTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-app-contracts-${TS}.xml" \
  -logFile -
```

Expected: FAIL (contracts not found).

- [ ] **Step 3: Implement minimal contracts**

`UnityProject/Assets/Change/Framework/Application/Abstractions/IUseCase.cs`

```csharp
namespace Change.Framework.Application
{
    public interface IUseCase<TRequest, TResult>
        where TRequest : struct
    {
        TResult Execute(in TRequest request);
    }
}
```

`UnityProject/Assets/Change/Framework/Application/Abstractions/IAppFacade.cs`

```csharp
namespace Change.Framework.Application
{
    public interface IAppFacade
    {
    }
}
```

`UnityProject/Assets/Change/Framework/Application/Abstractions/IPresenter.cs`

```csharp
namespace Change.Framework.Application
{
    public interface IPresenter
    {
        void OnOpen();
        void OnClose();
    }
}
```

`UnityProject/Assets/Change/Framework/UI/Abstractions/WindowLayer.cs`

```csharp
namespace Change.Framework.UI
{
    public enum WindowLayer : byte
    {
        Bottom = 0,
        Normal = 1,
        Popup = 2,
        Top = 3,
    }
}
```

`UnityProject/Assets/Change/Framework/UI/Abstractions/WindowState.cs`

```csharp
namespace Change.Framework.UI
{
    public enum WindowState : byte
    {
        Closed = 0,
        Opening = 1,
        Open = 2,
        Hidden = 3,
        Closing = 4,
    }
}
```

`UnityProject/Assets/Change/Framework/UI/Abstractions/WindowId.cs`

```csharp
using System;

namespace Change.Framework.UI
{
    public readonly struct WindowId : IEquatable<WindowId>
    {
        public WindowId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("WindowId cannot be null or whitespace.", nameof(value));
            }

            Value = value;
        }

        public string Value { get; }

        public bool Equals(WindowId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WindowId other && Equals(other);
        public override int GetHashCode() => Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        public override string ToString() => Value ?? string.Empty;

        public static bool operator ==(WindowId left, WindowId right) => left.Equals(right);
        public static bool operator !=(WindowId left, WindowId right) => !left.Equals(right);
    }
}
```

`UnityProject/Assets/Change/Framework/UI/Abstractions/WindowOpenOptions.cs`

```csharp
namespace Change.Framework.UI
{
    public readonly struct WindowOpenOptions
    {
        public WindowOpenOptions(
            WindowLayer layer,
            bool reuseIfLoaded = true,
            bool allowMultipleInstances = false,
            int instanceId = 0)
        {
            Layer = layer;
            ReuseIfLoaded = reuseIfLoaded;
            AllowMultipleInstances = allowMultipleInstances;
            InstanceId = instanceId;
        }

        public static WindowOpenOptions Default => new(WindowLayer.Normal);

        public WindowLayer Layer { get; }
        public bool ReuseIfLoaded { get; }
        public bool AllowMultipleInstances { get; }
        public int InstanceId { get; }
    }
}
```

- [ ] **Step 4: Run tests and confirm pass**

Run the same command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Application \
        UnityProject/Assets/Change/Framework/UI \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Application/ApplicationContractsTests.cs
git commit -m "feat(framework): add app and window contracts for UI shell"
```

---

### Task 2: Build WindowManager core with cache/layering/dedup semantics

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/UI/Abstractions/IWindowView.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Abstractions/IWindowFactory.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Core/WindowRequest.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Core/WindowManager.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/WindowManagerCoreTests.cs`

- [ ] **Step 1: Write failing WindowManager tests**

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Change.Framework.UI;
using NUnit.Framework;

namespace Change.Runtime.UI.Tests
{
    public class WindowManagerCoreTests
    {
        [Test]
        public async UniTask OpenAsync_ReusesExistingWindow_WhenReuseEnabled()
        {
            var factory = new FakeWindowFactory();
            var manager = new WindowManager(factory);
            var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);

            var first = await manager.OpenAsync(in request, CancellationToken.None);
            var second = await manager.OpenAsync(in request, CancellationToken.None);

            Assert.AreSame(first, second);
            Assert.AreEqual(1, factory.CreateCount);
        }

        [Test]
        public async UniTask OpenAsync_CreatesDistinctInstances_WhenMultipleInstancesEnabled()
        {
            var factory = new FakeWindowFactory();
            var manager = new WindowManager(factory);
            var optionsA = new WindowOpenOptions(WindowLayer.Normal, reuseIfLoaded: false, allowMultipleInstances: true, instanceId: 1);
            var optionsB = new WindowOpenOptions(WindowLayer.Normal, reuseIfLoaded: false, allowMultipleInstances: true, instanceId: 2);
            var requestA = new WindowRequest(new WindowId("Inventory"), optionsA);
            var requestB = new WindowRequest(new WindowId("Inventory"), optionsB);

            var first = await manager.OpenAsync(in requestA, CancellationToken.None);
            var second = await manager.OpenAsync(in requestB, CancellationToken.None);

            Assert.AreNotSame(first, second);
            Assert.AreEqual(2, factory.CreateCount);
        }

        [Test]
        public async UniTask Close_RemovesCachedWindow()
        {
            var factory = new FakeWindowFactory();
            var manager = new WindowManager(factory);
            var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);

            await manager.OpenAsync(in request, CancellationToken.None);
            var closed = manager.Close(in request);

            Assert.IsTrue(closed);
            Assert.IsFalse(manager.TryGet(in request, out _));
        }
    }
}
```

- [ ] **Step 2: Run tests to confirm failure**

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.UI.Tests.WindowManagerCoreTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-window-manager-${TS}.xml" \
  -logFile -
```

Expected: FAIL (`WindowManager` and related contracts missing).

- [ ] **Step 3: Implement WindowManager and runtime abstractions**

`UnityProject/Assets/Change/Runtime/UI/Abstractions/IWindowView.cs`

```csharp
using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public interface IWindowView
    {
        WindowId Id { get; }
        WindowState State { get; }
        void BringToFront();
        void SetVisible(bool visible);
        void Dispose();
    }
}
```

`UnityProject/Assets/Change/Runtime/UI/Abstractions/IWindowFactory.cs`

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.UI
{
    public interface IWindowFactory
    {
        UniTask<IWindowView> CreateAsync(in WindowRequest request, CancellationToken cancellationToken);
    }
}
```

`UnityProject/Assets/Change/Runtime/UI/Core/WindowRequest.cs`

```csharp
using System;
using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public readonly struct WindowRequest : IEquatable<WindowRequest>
    {
        public WindowRequest(WindowId id, WindowOpenOptions options)
        {
            Id = id;
            Options = options;
        }

        public WindowId Id { get; }
        public WindowOpenOptions Options { get; }

        public bool Equals(WindowRequest other)
        {
            return Id.Equals(other.Id)
                   && Options.AllowMultipleInstances == other.Options.AllowMultipleInstances
                   && Options.InstanceId == other.Options.InstanceId;
        }

        public override bool Equals(object obj) => obj is WindowRequest other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Id.GetHashCode();
                hash = (hash * 397) ^ (Options.AllowMultipleInstances ? 1 : 0);
                hash = (hash * 397) ^ Options.InstanceId;
                return hash;
            }
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/UI/Core/WindowManager.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.UI
{
    public sealed class WindowManager
    {
        private readonly IWindowFactory _factory;
        private readonly Dictionary<WindowRequest, IWindowView> _opened = new();
        private readonly Dictionary<WindowRequest, UniTask<IWindowView>> _inflight = new();

        public WindowManager(IWindowFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public int ActiveCount => _opened.Count;

        public bool TryGet(in WindowRequest request, out IWindowView view) => _opened.TryGetValue(request, out view);

        public async UniTask<IWindowView> OpenAsync(in WindowRequest request, CancellationToken cancellationToken)
        {
            if (request.Options.ReuseIfLoaded && _opened.TryGetValue(request, out var existing))
            {
                existing.BringToFront();
                existing.SetVisible(true);
                return existing;
            }

            if (_inflight.TryGetValue(request, out var pending))
            {
                return await pending;
            }

            var createTask = _factory.CreateAsync(in request, cancellationToken);
            _inflight[request] = createTask;

            try
            {
                var created = await createTask;
                _opened[request] = created;
                created.SetVisible(true);
                return created;
            }
            finally
            {
                _inflight.Remove(request);
            }
        }

        public bool Close(in WindowRequest request)
        {
            if (!_opened.TryGetValue(request, out var window))
            {
                return false;
            }

            _opened.Remove(request);
            window.Dispose();
            return true;
        }
    }
}
```

In the same test file, append test doubles:

```csharp
using Change.Framework.UI;

namespace Change.Runtime.UI.Tests
{
    internal sealed class FakeWindowFactory : IWindowFactory
    {
        public int CreateCount { get; private set; }

        public UniTask<IWindowView> CreateAsync(in WindowRequest request, CancellationToken cancellationToken)
        {
            CreateCount++;
            return UniTask.FromResult<IWindowView>(new FakeWindowView(request.Id));
        }
    }

    internal sealed class FakeWindowView : IWindowView
    {
        public FakeWindowView(WindowId id)
        {
            Id = id;
            State = WindowState.Closed;
        }

        public WindowId Id { get; }
        public WindowState State { get; private set; }
        public void BringToFront() { }
        public void SetVisible(bool visible) => State = visible ? WindowState.Open : WindowState.Hidden;
        public void Dispose() => State = WindowState.Closed;
    }
}
```

- [ ] **Step 4: Run tests and confirm pass**

Run Step 2 command again.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/UI/Abstractions \
        UnityProject/Assets/Change/Runtime/UI/Core \
        UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/WindowManagerCoreTests.cs
git commit -m "feat(runtime): add window manager core with cache and dedup"
```

---

### Task 3: Add YooAsset + UniTask loader and FairyGUI factory adapter

**Files:**
- Modify: `UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef`
- Create: `UnityProject/Assets/Change/Runtime/UI/Abstractions/IUiAssetLoader.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Loading/UiAssetLease.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Loading/YooUiAssetLoader.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowView.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowFactory.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/YooUiAssetLoaderTests.cs`

- [ ] **Step 1: Write failing loader tests**

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Change.Framework.UI;
using NUnit.Framework;
using UnityEngine;

namespace Change.Runtime.UI.Tests
{
    public class YooUiAssetLoaderTests
    {
        [Test]
        public async UniTask LoadPrefabAsync_Succeeds_ReturnsLease()
        {
            var package = new FakeYooPackage(success: true);
            var loader = new YooUiAssetLoader(package);

            var lease = await loader.LoadPrefabAsync(new WindowId("Inventory"), "ui/inventory.prefab", CancellationToken.None);

            Assert.NotNull(lease.Instance);
            lease.Dispose();
        }

        [Test]
        public void LoadPrefabAsync_FailedHandle_ThrowsInvalidOperationException()
        {
            var package = new FakeYooPackage(success: false);
            var loader = new YooUiAssetLoader(package);

            Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
            {
                await loader.LoadPrefabAsync(new WindowId("Inventory"), "ui/missing.prefab", CancellationToken.None);
            });
        }
    }
}
```

- [ ] **Step 2: Run tests to confirm failure**

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.UI.Tests.YooUiAssetLoaderTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-ui-loader-${TS}.xml" \
  -logFile -
```

Expected: FAIL (`YooUiAssetLoader` missing).

- [ ] **Step 3: Implement loader + FairyGUI adapter + asmdef refs**

`UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef` (add references)

```json
{
    "name": "Change.Runtime",
    "rootNamespace": "Change.Runtime",
    "references": [
        "Change.Framework",
        "UniTask",
        "YooAsset",
        "FairyGUI"
    ],
    "optionalUnityReferences": [],
    "includePlatforms": [],
    "excludePlatforms": []
}
```

`UnityProject/Assets/Change/Runtime/UI/Abstractions/IUiAssetLoader.cs`

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public interface IUiAssetLoader
    {
        UniTask<UiAssetLease> LoadPrefabAsync(WindowId windowId, string location, CancellationToken cancellationToken);
    }
}
```

`UnityProject/Assets/Change/Runtime/UI/Loading/UiAssetLease.cs`

```csharp
using System;
using UnityEngine;

namespace Change.Runtime.UI
{
    public readonly struct UiAssetLease : IDisposable
    {
        private readonly Action _release;

        public UiAssetLease(GameObject instance, Action release)
        {
            Instance = instance;
            _release = release;
        }

        public GameObject Instance { get; }

        public void Dispose()
        {
            _release?.Invoke();
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/UI/Loading/YooUiAssetLoader.cs`

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Change.Framework.UI;
using UnityEngine;
using YooAsset;

namespace Change.Runtime.UI
{
    public interface IYooAssetPackage
    {
        IYooAssetLoadHandle LoadGameObjectAsync(string location);
    }

    public interface IYooAssetLoadHandle
    {
        System.Threading.Tasks.Task Task { get; }
        bool Succeeded { get; }
        string LastError { get; }
        GameObject AssetObject { get; }
        void Release();
    }

    public sealed class YooAssetPackageAdapter : IYooAssetPackage
    {
        private readonly ResourcePackage _package;

        public YooAssetPackageAdapter(ResourcePackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public IYooAssetLoadHandle LoadGameObjectAsync(string location)
        {
            var handle = _package.LoadAssetAsync<GameObject>(location);
            return new YooAssetLoadHandleAdapter(handle);
        }
    }

    public sealed class YooAssetLoadHandleAdapter : IYooAssetLoadHandle
    {
        private readonly AssetHandle _handle;

        public YooAssetLoadHandleAdapter(AssetHandle handle)
        {
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
        }

        public System.Threading.Tasks.Task Task => _handle.Task;
        public bool Succeeded => _handle.Status == EOperationStatus.Succeed;
        public string LastError => _handle.LastError;
        public GameObject AssetObject => _handle.GetAssetObject<GameObject>();
        public void Release() => _handle.Release();
    }

    public sealed class YooUiAssetLoader : IUiAssetLoader
    {
        private readonly IYooAssetPackage _package;

        public YooUiAssetLoader(IYooAssetPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public async UniTask<UiAssetLease> LoadPrefabAsync(WindowId windowId, string location, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new ArgumentException("Asset location is required.", nameof(location));
            }

            var handle = _package.LoadGameObjectAsync(location);
            await handle.Task.AsUniTask(cancellationToken: cancellationToken);

            if (!handle.Succeeded)
            {
                throw new InvalidOperationException($"Failed to load window asset. id={windowId}, location={location}, error={handle.LastError}");
            }

            var prefab = handle.AssetObject;
            if (prefab == null)
            {
                handle.Release();
                throw new InvalidOperationException($"Loaded asset is null GameObject. id={windowId}, location={location}");
            }

            var instance = UnityEngine.Object.Instantiate(prefab);
            return new UiAssetLease(instance, () =>
            {
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }
                handle.Release();
            });
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowView.cs`

```csharp
using System;
using Change.Framework.UI;
using FairyGUI;

namespace Change.Runtime.UI
{
    public sealed class FairyGuiWindowView : IWindowView
    {
        private readonly GComponent _root;

        public FairyGuiWindowView(WindowId id, GComponent root)
        {
            Id = id;
            _root = root ?? throw new ArgumentNullException(nameof(root));
            State = WindowState.Closed;
        }

        public WindowId Id { get; }
        public WindowState State { get; private set; }

        public void BringToFront()
        {
            _root.SortingOrder = int.MaxValue;
        }

        public void SetVisible(bool visible)
        {
            _root.visible = visible;
            State = visible ? WindowState.Open : WindowState.Hidden;
        }

        public void Dispose()
        {
            State = WindowState.Closed;
            _root.Dispose();
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowFactory.cs`

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FairyGUI;

namespace Change.Runtime.UI
{
    public sealed class FairyGuiWindowFactory : IWindowFactory
    {
        private readonly IUiAssetLoader _loader;
        private readonly IWindowLocationResolver _resolver;

        public FairyGuiWindowFactory(IUiAssetLoader loader, IWindowLocationResolver resolver)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public async UniTask<IWindowView> CreateAsync(in WindowRequest request, CancellationToken cancellationToken)
        {
            var location = _resolver.ResolvePrefabLocation(request.Id);
            var lease = await _loader.LoadPrefabAsync(request.Id, location, cancellationToken);
            var panel = lease.Instance.GetComponent<UIPanel>();
            if (panel == null || panel.ui == null)
            {
                lease.Dispose();
                throw new InvalidOperationException($"UIPanel/GComponent missing on window prefab: {location}");
            }

            return new FairyGuiWindowView(request.Id, panel.ui);
        }
    }

    public interface IWindowLocationResolver
    {
        string ResolvePrefabLocation(Change.Framework.UI.WindowId id);
    }
}
```

Update test doubles in `YooUiAssetLoaderTests.cs`:

```csharp
using YooAsset;
using UnityEngine;

namespace Change.Runtime.UI.Tests
{
    internal sealed class FakeYooPackage : IYooAssetPackage
    {
        private readonly bool _success;

        public FakeYooPackage(bool success)
        {
            _success = success;
        }

        public IYooAssetLoadHandle LoadGameObjectAsync(string location)
        {
            var prefab = _success ? new GameObject("fake-prefab") : null;
            return new FakeYooLoadHandle(_success, prefab, _success ? string.Empty : "asset-not-found");
        }
    }

    internal sealed class FakeYooLoadHandle : IYooAssetLoadHandle
    {
        private readonly bool _succeeded;
        private readonly GameObject _prefab;
        private readonly string _lastError;

        public FakeYooLoadHandle(bool succeeded, GameObject prefab, string lastError)
        {
            _succeeded = succeeded;
            _prefab = prefab;
            _lastError = lastError;
        }

        public System.Threading.Tasks.Task Task => System.Threading.Tasks.Task.CompletedTask;
        public bool Succeeded => _succeeded;
        public string LastError => _lastError;
        public GameObject AssetObject => _prefab;

        public void Release()
        {
            if (_prefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_prefab);
            }
        }
    }
}
```

- [ ] **Step 4: Run tests and confirm pass**

Run Step 2 command again.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef \
        UnityProject/Assets/Change/Runtime/UI \
        UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/YooUiAssetLoaderTests.cs
git commit -m "feat(runtime): add yooasset loader and fairygui window factory"
```

---

### Task 4: Add GameScript sample presenter/use-case flow and architecture guards

**Files:**
- Create: `UnityProject/Assets/GameScript/UI/Inventory/InventoryWindowViewModel.cs`
- Create: `UnityProject/Assets/GameScript/UI/Inventory/InventoryWindowContracts.cs`
- Create: `UnityProject/Assets/GameScript/UI/Inventory/OpenInventoryUseCase.cs`
- Create: `UnityProject/Assets/GameScript/UI/Inventory/InventoryWindowPresenter.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/NoReflectionBindingGuardTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/GameScriptSampleContractTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/README.md`

- [ ] **Step 1: Write failing architecture guard tests**

```csharp
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Change.Runtime.UI.Tests
{
    public class NoReflectionBindingGuardTests
    {
        [Test]
        public void RuntimeUi_MustNotUseReflectionBindingApis()
        {
            var root = Path.Combine(Application.dataPath, "Change/Runtime/UI");
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            var content = files.Select(File.ReadAllText).ToArray();

            Assert.IsFalse(content.Any(x => x.Contains("System.Reflection")));
            Assert.IsFalse(content.Any(x => x.Contains(".GetType(")));
            Assert.IsFalse(content.Any(x => x.Contains("PropertyInfo")));
        }
    }
}
```

```csharp
using System;
using NUnit.Framework;

namespace Change.Runtime.UI.Tests
{
    public class GameScriptSampleContractTests
    {
        [Test]
        public void InventoryWindowPresenter_TypeExists_InAssemblyCSharp()
        {
            var type = Type.GetType("GameScript.UI.Inventory.InventoryWindowPresenter, Assembly-CSharp");
            Assert.IsNotNull(type);
        }

        [Test]
        public void OpenInventoryUseCase_TypeExists_InAssemblyCSharp()
        {
            var type = Type.GetType("GameScript.UI.Inventory.OpenInventoryUseCase, Assembly-CSharp");
            Assert.IsNotNull(type);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.UI.Tests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-ui-guards-${TS}.xml" \
  -logFile -
```

Expected: FAIL (`GameScript` sample types do not exist).

- [ ] **Step 3: Implement GameScript sample flow and runtime README**

`UnityProject/Assets/GameScript/UI/Inventory/InventoryWindowViewModel.cs`

```csharp
namespace GameScript.UI.Inventory
{
    public readonly struct InventoryWindowViewModel
    {
        public InventoryWindowViewModel(int itemCount, int gold)
        {
            ItemCount = itemCount;
            Gold = gold;
        }

        public int ItemCount { get; }
        public int Gold { get; }
    }
}
```

`UnityProject/Assets/GameScript/UI/Inventory/InventoryWindowContracts.cs`

```csharp
using Change.Framework.Application;

namespace GameScript.UI.Inventory
{
    public readonly struct OpenInventoryRequest
    {
        public OpenInventoryRequest(int playerId)
        {
            PlayerId = playerId;
        }

        public int PlayerId { get; }
    }

    public interface IInventoryWindowView
    {
        void Apply(in InventoryWindowViewModel model);
    }

    public interface IOpenInventoryUseCase : IUseCase<OpenInventoryRequest, InventoryWindowViewModel>
    {
    }
}
```

`UnityProject/Assets/GameScript/UI/Inventory/OpenInventoryUseCase.cs`

```csharp
using Change.Framework.Cqrs;

namespace GameScript.UI.Inventory
{
    public sealed class OpenInventoryUseCase : IOpenInventoryUseCase
    {
        private readonly ICqrsBus _bus;

        public OpenInventoryUseCase(ICqrsBus bus)
        {
            _bus = bus;
        }

        public InventoryWindowViewModel Execute(in OpenInventoryRequest request)
        {
            var query = new GetInventorySummaryQuery(request.PlayerId);
            var result = _bus.Query<GetInventorySummaryQuery, InventorySummaryResult>(in query);
            return new InventoryWindowViewModel(result.ItemCount, result.Gold);
        }
    }

    public readonly struct GetInventorySummaryQuery : IQuery<InventorySummaryResult>
    {
        public GetInventorySummaryQuery(int playerId)
        {
            PlayerId = playerId;
        }

        public int PlayerId { get; }
    }

    public readonly struct InventorySummaryResult
    {
        public InventorySummaryResult(int itemCount, int gold)
        {
            ItemCount = itemCount;
            Gold = gold;
        }

        public int ItemCount { get; }
        public int Gold { get; }
    }
}
```

`UnityProject/Assets/GameScript/UI/Inventory/InventoryWindowPresenter.cs`

```csharp
using Change.Framework.Application;

namespace GameScript.UI.Inventory
{
    public sealed class InventoryWindowPresenter : IPresenter
    {
        private readonly IInventoryWindowView _view;
        private readonly IOpenInventoryUseCase _useCase;
        private readonly int _playerId;

        public InventoryWindowPresenter(IInventoryWindowView view, IOpenInventoryUseCase useCase, int playerId)
        {
            _view = view;
            _useCase = useCase;
            _playerId = playerId;
        }

        public void OnOpen()
        {
            var request = new OpenInventoryRequest(_playerId);
            var model = _useCase.Execute(in request);
            _view.Apply(in model);
        }

        public void OnClose()
        {
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/UI/README.md`

```markdown
# Change.Runtime.UI

## Rules

1. Runtime UI does not include business decisions.
2. Views delegate to presenters/use-cases; business state changes go through CQRS.
3. Runtime reflection binding is forbidden (`System.Reflection`, `.GetType(`).
4. Window loading/unloading goes through `IUiAssetLoader`.

## Core flow

`WindowManager` -> `IWindowFactory` -> `IUiAssetLoader` -> view creation -> presenter orchestration.
```

- [ ] **Step 4: Run tests and confirm pass**

Run Step 2 command again.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/GameScript/UI/Inventory \
        UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/NoReflectionBindingGuardTests.cs \
        UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/GameScriptSampleContractTests.cs \
        UnityProject/Assets/Change/Runtime/UI/README.md
git commit -m "feat(gamescript): add inventory presenter/usecase sample and ui guardrails"
```

---

### Task 5: Final validation (framework + runtime suites) and documentation sync

**Files:**
- Modify: `docs/superpowers/specs/2026-04-26-change-ui-application-shell-design.md`

- [ ] **Step 1: Run focused Framework and Runtime EditMode suites**

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.Application,Change.Runtime.UI.Tests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-ui-shell-${TS}.xml" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 2: Run Runtime PlayMode smoke tests (non-blocking but recorded)**

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform PlayMode \
  -testFilter "Change.Runtime" \
  -testResults "$(pwd)/UnityProject/TestResults/playmode-runtime-ui-shell-${TS}.xml" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 3: Update design spec with implementation status note**

Add a short section at the end of `docs/superpowers/specs/2026-04-26-change-ui-application-shell-design.md`:

```markdown
## Implementation Status

- MVP implementation tracked by `docs/superpowers/plans/2026-04-26-change-ui-application-shell-plan.md`.
- Framework contracts, runtime shell core, and GameScript sample are validated by EditMode tests.
```

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-04-26-change-ui-application-shell-design.md \
        docs/superpowers/plans/2026-04-26-change-ui-application-shell-plan.md
git commit -m "docs(spec): add ui shell implementation status note"
```

---

## Post-Plan Notes for Execution

1. Keep each task atomic; do not batch-implement across tasks before running tests.
2. If YooAsset/FairyGUI API details differ in your local package patch level, adapt only adapter internals and keep public contracts unchanged.
3. Maintain the hard rule: no runtime reflection binding.
