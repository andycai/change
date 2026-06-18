# Change Client Shell Composition & Dual-Channel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Worktree:** Per `AGENTS.md`, run superpowers `subagent-driven-development` or `executing-plans` from a git worktree under `.worktrees/`, not directly on `main` if your team policy requires isolation.

**Goal:** Land VContainer composition (`Root` + disposable `GameRoot` child), `INetworkGateway` seam, shell-stage `IAppEventBus` whitelist, and `WindowManager` as optional **Presenter 宿主**（策略 C 钩子），with EditMode tests—without ECS.

**Architecture:** `Change.Runtime` owns `LifetimeScope`（引擎/基础设施）并 `CreateChild` 出 `GameRoot`；热更程序集实现 `IHotfixGameInstaller.Install` 仅向 **子 Scope** 注册业务绑定；网络写只走 `INetworkGateway`；本地同步仍走 `Change.Framework.Cqrs`；`WindowManager` 在 `OpenAsync`/`Close` 路径上挂载 `IPresenter` 生命周期（可选工厂，默认空实现保持兼容）。

**Tech Stack:** Unity 2022.3, C#, VContainer 1.17.0（`Packages/jp.hadashikick.vcontainer`）, UniTask 2.5.10, Change.Framework, Change.Runtime, GameScript（默认 `Assembly-CSharp`）, Unity Test Framework EditMode.

**Design baseline:** `docs/superpowers/specs/2026-05-09-change-client-shell-composition-and-channel-design.md`

---

## Scope Check

单一连贯交付：组合根 + 双通道接缝 + 窗口 Presenter 宿主钩子 + 测试护栏。ECS 不在范围。若后续 SLG/战斗仿真膨胀，应另开 spec/plan。

---

## File Structure (Create / Modify Map)

### Packages / asmdef

- Modify: `UnityProject/Packages/manifest.json`（如缺少则添加 `"jp.hadashikick.vcontainer": "file:jp.hadashikick.vcontainer"`，与 `packages-lock.json` 一致）
- Modify: `UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef`（添加 `VContainer` 程序集引用）
- Modify: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Change.Runtime.EditModeTests.asmdef`（添加 `VContainer`）

### Runtime — 网络通道（双通道 A）

- Create: `UnityProject/Assets/Change/Runtime/Net/Abstractions/INetworkGateway.cs`
- Create: `UnityProject/Assets/Change/Runtime/Net/Abstractions/NetworkCommandEnvelope.cs`（最小不可变载荷：`string OperationKey` + `int Payload` 占位即可，YAGNI）
- Create: `UnityProject/Assets/Change/Runtime/Net/NoOpNetworkGateway.cs`（`SendAsync` 立即完成）

### Runtime — 应用事件白名单（混合 C）

- Create: `UnityProject/Assets/Change/Runtime/App/Events/IAppEventBus.cs`
- Create: `UnityProject/Assets/Change/Runtime/App/Events/InProcessAppEventBus.cs`
- Create: `UnityProject/Assets/Change/Runtime/App/Events/AppEvents.cs`（例如 `LocaleChanged`, `ConnectivityChanged` 两个 `readonly struct`，仅数据）

### Runtime — 组合根

- Create: `UnityProject/Assets/Change/Runtime/Composition/EngineLifetimeScope.cs`（`LifetimeScope` 子类：只注册引擎级服务，如 `INetworkGateway`→`NoOpNetworkGateway`、`IAppEventBus`→`InProcessAppEventBus`）
- Create: `UnityProject/Assets/Change/Runtime/Composition/GameRootScope.cs`（`sealed` 包装：`LifetimeScope` 子实例 + `Dispose()`）
- Create: `UnityProject/Assets/Change/Runtime/Composition/IHotfixGameInstaller.cs`（`void Install(IContainerBuilder builder);` — 由热更实现，向 **GameRoot builder** 追加注册）
- Create: `UnityProject/Assets/Change/Runtime/Composition/GameCompositionHost.cs`（从父 `LifetimeScope` `CreateChild` 安装热更 installer，暴露 `LifetimeScope` 或 `IObjectResolver` 供窗口解析）

### Runtime — 窗口 Presenter 宿主

- Modify: `UnityProject/Assets/Change/Runtime/UI/Core/WindowManager.cs`（引入可选 `IWindowPresenterHost`；内部 `OpenedWindowEntry`）
- Create: `UnityProject/Assets/Change/Runtime/UI/Core/OpenedWindowEntry.cs`（`IWindowView` + `IPresenter?` + `IDisposable?` windowScope）
- Create: `UnityProject/Assets/Change/Runtime/UI/Core/IWindowPresenterHost.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Core/NullWindowPresenterHost.cs`（空对象：不创建 presenter）

### GameScript — 热更 Installer 样板

- Create: `UnityProject/Assets/GameScript/Composition/GameHotfixInstaller.cs`（实现 `IHotfixGameInstaller`，注册示例服务如 `InventoryWindowPresenter` 若需要—YAGNI 可先只注册 `INetworkGateway` 覆盖或 no-op）

### Tests

- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Composition/GameRootScopeTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Composition/DualChannelRegistrationTests.cs`（容器能解析 `INetworkGateway`；**不**测试 Framework CQRS，本任务只验证网关注册存在）
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/WindowManagerPresenterHostTests.cs`（host 为 fake 时 `OnOpen`/`OnClose` 顺序）
- Modify: `UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/WindowManagerCoreTests.cs`（可选：抽共享 fake factory；默认可不动）

---

### Task 1: 显式 UPM 依赖与 asmdef 引用

**Files:**

- Modify: `UnityProject/Packages/manifest.json`
- Modify: `UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef`
- Modify: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Change.Runtime.EditModeTests.asmdef`

- [ ] **Step 1: 若 manifest 缺少 VContainer 条目则补上**

在 `dependencies` 中加入（与已嵌入包一致）：

```json
"jp.hadashikick.vcontainer": "file:jp.hadashikick.vcontainer"
```

- [ ] **Step 2: 更新 `Change.Runtime.asmdef` 的 `references`**

追加字符串：`"VContainer"`（UPM 包 id 为 `jp.hadashikick.vcontainer`，但 **`.asmdef` 的 `references` 必须使用程序集名**，与 `Packages/jp.hadashikick.vcontainer/Runtime/VContainer.asmdef` 的 `name` 一致）。

完整示例：

```json
{
    "name": "Change.Runtime",
    "rootNamespace": "Change.Runtime",
    "references": [
        "Change.Framework",
        "UniTask",
        "YooAsset",
        "FairyGUI",
        "VContainer"
    ],
    "optionalUnityReferences": [],
    "includePlatforms": [],
    "excludePlatforms": []
}
```

- [ ] **Step 3: 更新 `Change.Runtime.EditModeTests.asmdef`**

在 `references` 中追加：`"VContainer"`。

- [ ] **Step 4: Commit**

```bash
git add UnityProject/Packages/manifest.json UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef UnityProject/Assets/Change/Runtime/Tests/EditMode/Change.Runtime.EditModeTests.asmdef
git commit -m "chore: reference VContainer from Change.Runtime and EditMode tests"
```

---

### Task 2: `INetworkGateway` 接缝 + NoOp 实现

**Files:**

- Create: `UnityProject/Assets/Change/Runtime/Net/Abstractions/INetworkGateway.cs`
- Create: `UnityProject/Assets/Change/Runtime/Net/Abstractions/NetworkCommandEnvelope.cs`
- Create: `UnityProject/Assets/Change/Runtime/Net/NoOpNetworkGateway.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Composition/DualChannelRegistrationTests.cs`

- [ ] **Step 1: 编写失败测试（解析接口）**

```csharp
using Change.Runtime.Net;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Change.Runtime.Tests.Composition
{
    public sealed class DualChannelRegistrationTests
    {
        private sealed class TestEngineScope : LifetimeScope
        {
            protected override void Configure(IContainerBuilder builder)
            {
                builder.Register<INetworkGateway, NoOpNetworkGateway>(Lifetime.Singleton);
            }
        }

        [Test]
        public void EngineScope_Resolves_INetworkGateway()
        {
            var go = new GameObject("TestEngineScope");
            var scope = go.AddComponent<TestEngineScope>();
            scope.Build();

            var gateway = scope.Container.Resolve<INetworkGateway>();
            Assert.IsInstanceOf<NoOpNetworkGateway>(gateway);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run（AGENTS.md 路径规则）：

```bash
/Applications/Unity/Unity.app/Contents/MacOS/Unity -projectPath "/Users/andy/Workspace/github/andycai/fun/UnityProject" -batchmode -nographics -runTests -testPlatform EditMode -assemblyNames "Change.Runtime.EditModeTests" -testResults "/Users/andy/Workspace/github/andycai/fun/UnityProject/TestResults/editmode-composition-$(date +%Y%m%d-%H%M%S).xml" -logFile -
```

Expected: 编译失败（类型不存在）或测试失败。

- [ ] **Step 3: 添加最小接口与实现**

`INetworkGateway.cs`:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.Net
{
    public interface INetworkGateway
    {
        UniTask SendAsync(in NetworkCommandEnvelope envelope, CancellationToken cancellationToken = default);
    }
}
```

`NetworkCommandEnvelope.cs`:

```csharp
namespace Change.Runtime.Net
{
    public readonly struct NetworkCommandEnvelope
    {
        public NetworkCommandEnvelope(string operationKey, int payload)
        {
            OperationKey = operationKey;
            Payload = payload;
        }

        public string OperationKey { get; }
        public int Payload { get; }
    }
}
```

`NoOpNetworkGateway.cs`:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.Net
{
    public sealed class NoOpNetworkGateway : INetworkGateway
    {
        public UniTask SendAsync(in NetworkCommandEnvelope envelope, CancellationToken cancellationToken = default)
        {
            return UniTask.CompletedTask;
        }
    }
}
```

- [ ] **Step 4: 再跑测试，期望 PASS**

同上 Unity CLI。

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Net UnityProject/Assets/Change/Runtime/Tests/EditMode/Composition/DualChannelRegistrationTests.cs
git commit -m "feat(runtime): add INetworkGateway seam and no-op implementation"
```

---

### Task 3: 壳阶段 `IAppEventBus` + 白名单 struct

**Files:**

- Create: `UnityProject/Assets/Change/Runtime/App/Events/IAppEventBus.cs`
- Create: `UnityProject/Assets/Change/Runtime/App/Events/InProcessAppEventBus.cs`
- Create: `UnityProject/Assets/Change/Runtime/App/Events/AppEvents.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Composition/AppEventBusTests.cs`

- [ ] **Step 1: 失败测试（订阅收到发布）**

```csharp
using System;
using Change.Runtime.App.Events;
using NUnit.Framework;

namespace Change.Runtime.Tests.Composition
{
    public sealed class AppEventBusTests
    {
        [Test]
        public void Publish_LocaleChanged_InvokesSubscriber()
        {
            var bus = new InProcessAppEventBus();
            AppLocaleChanged? received = null;
            IDisposable sub = bus.Subscribe<AppLocaleChanged>(e => received = e);

            bus.Publish(new AppLocaleChanged("zh-CN"));

            Assert.IsNotNull(received);
            Assert.IsTrue(received.HasValue);
            Assert.AreEqual("zh-CN", received.Value.CultureName);
            sub.Dispose();
        }
    }
}
```

`AppEvents.cs` 中先有：

```csharp
namespace Change.Runtime.App.Events
{
    public readonly struct AppLocaleChanged
    {
        public AppLocaleChanged(string cultureName) => CultureName = cultureName;
        public string CultureName { get; }
    }
}
```

- [ ] **Step 2: 运行测试至失败**

同上 EditMode 程序集。

- [ ] **Step 3: 实现总线（进程内，单线程假设）**

`IAppEventBus.cs`:

```csharp
using System;

namespace Change.Runtime.App.Events
{
    public interface IAppEventBus
    {
        void Publish<T>(in T evt) where T : struct;
        IDisposable Subscribe<T>(Action<T> handler) where T : struct;
    }
}
```

`InProcessAppEventBus.cs`（最小：字典 `Type`→`Delegate` 列表，`lock` 保护；`Publish` 同步调用 handler；YAGNI 不做跨线程）：

```csharp
using System;
using System.Collections.Generic;

namespace Change.Runtime.App.Events
{
    public sealed class InProcessAppEventBus : IAppEventBus
    {
        private readonly object _gate = new();
        private readonly Dictionary<Type, List<Delegate>> _subs = new();

        public void Publish<T>(in T evt) where T : struct
        {
            List<Delegate> copy;
            lock (_gate)
            {
                if (!_subs.TryGetValue(typeof(T), out var list))
                {
                    return;
                }

                copy = new List<Delegate>(list);
            }

            var boxed = evt;
            for (var i = 0; i < copy.Count; i++)
            {
                ((Action<T>)copy[i]).Invoke(boxed);
            }
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var key = typeof(T);
            lock (_gate)
            {
                if (!_subs.TryGetValue(key, out var list))
                {
                    list = new List<Delegate>();
                    _subs[key] = list;
                }

                list.Add(handler);
            }

            return new Subscription(this, key, handler);
        }

        private void Remove(Type key, Delegate handler)
        {
            lock (_gate)
            {
                if (!_subs.TryGetValue(key, out var list))
                {
                    return;
                }

                list.Remove(handler);
                if (list.Count == 0)
                {
                    _subs.Remove(key);
                }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly InProcessAppEventBus _owner;
            private readonly Type _key;
            private readonly Delegate _handler;
            private bool _disposed;

            public Subscription(InProcessAppEventBus owner, Type key, Delegate handler)
            {
                _owner = owner;
                _key = key;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.Remove(_key, _handler);
            }
        }
    }
}
```

- [ ] **Step 4: 测试 PASS + Commit**

```bash
git add UnityProject/Assets/Change/Runtime/App UnityProject/Assets/Change/Runtime/Tests/EditMode/Composition/AppEventBusTests.cs
git commit -m "feat(runtime): add in-process app event bus for shell whitelist"
```

---

### Task 4: `EngineLifetimeScope` + `GameCompositionHost` + `IHotfixGameInstaller`

**Files:**

- Create: `UnityProject/Assets/Change/Runtime/Composition/EngineLifetimeScope.cs`
- Create: `UnityProject/Assets/Change/Runtime/Composition/IHotfixGameInstaller.cs`
- Create: `UnityProject/Assets/Change/Runtime/Composition/GameCompositionHost.cs`
- Create: `UnityProject/Assets/GameScript/Composition/GameHotfixInstaller.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Composition/GameRootScopeTests.cs`

- [ ] **Step 1: 失败测试（子 Scope Dispose 释放 Transient 跟踪对象）**

```csharp
using System;
using Change.Runtime.Composition;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Change.Runtime.Tests.Composition
{
    public sealed class GameRootScopeTests
    {
        private sealed class Track : IDisposable
        {
            public bool Disposed;

            public void Dispose() => Disposed = true;
        }

        private sealed class Engine : LifetimeScope
        {
            protected override void Configure(IContainerBuilder builder)
            {
                builder.Register<INetworkGateway, Change.Runtime.Net.NoOpNetworkGateway>(Lifetime.Singleton);
            }
        }

        private sealed class HotfixInstaller : IHotfixGameInstaller
        {
            public void Install(IContainerBuilder builder)
            {
                builder.Register<Track>(Lifetime.Transient);
            }
        }

        [Test]
        public void DisposeGameRoot_DisposesTransientResolvedFromChild()
        {
            var go = new GameObject("Engine");
            var engine = go.AddComponent<Engine>();
            engine.Build();

            var host = new GameCompositionHost(engine);
            var child = host.CreateGameRoot(new HotfixInstaller());

            var t = child.Container.Resolve<Track>();
            Assert.IsFalse(t.Disposed);

            child.Dispose();

            Assert.IsTrue(t.Disposed);
        }
    }
}
```

（命名空间按你实现调整：`NoOpNetworkGateway` 所在命名空间用 `using Change.Runtime.Net;`。）

- [ ] **Step 2: 运行至失败**

同上。

- [ ] **Step 3: 实现 `IHotfixGameInstaller` + `GameCompositionHost`**

`IHotfixGameInstaller.cs`:

```csharp
using VContainer;

namespace Change.Runtime.Composition
{
    public interface IHotfixGameInstaller
    {
        void Install(IContainerBuilder builder);
    }
}
```

`GameCompositionHost.cs`（核心：`CreateGameRoot` 调用 `_engine.CreateChild(builder => { installer.Install(builder); })`，返回 `LifetimeScope` 子对象；`Dispose` 转发）：

```csharp
using System;
using VContainer.Unity;

namespace Change.Runtime.Composition
{
    public sealed class GameCompositionHost
    {
        private readonly LifetimeScope _engine;

        public GameCompositionHost(LifetimeScope engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public LifetimeScope CreateGameRoot(IHotfixGameInstaller installer)
        {
            if (installer == null) throw new ArgumentNullException(nameof(installer));
            return _engine.CreateChild(builder => installer.Install(builder));
        }
    }
}
```

`EngineLifetimeScope.cs`：在 `Configure` 注册 `INetworkGateway`、`IAppEventBus`（具体实现来自 Task 2/3）。

- [ ] **Step 4: `GameHotfixInstaller`（GameScript）**

```csharp
using Change.Runtime.Composition;
using VContainer;

namespace GameScript.Composition
{
    public sealed class GameHotfixInstaller : IHotfixGameInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            // YAGNI: 仅示例注册点；后续任务再绑窗口与 Gateway 实现
        }
    }
}
```

- [ ] **Step 5: 测试 PASS + Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Composition UnityProject/Assets/GameScript/Composition UnityProject/Assets/Change/Runtime/Tests/EditMode/Composition/GameRootScopeTests.cs
git commit -m "feat(runtime): add engine/game composition host and hotfix installer hook"
```

---

### Task 5: `WindowManager` Presenter 宿主（可选 + 策略 C 钩子）

**Files:**

- Create: `UnityProject/Assets/Change/Runtime/UI/Core/IWindowPresenterHost.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Core/NullWindowPresenterHost.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Core/OpenedWindowEntry.cs`
- Modify: `UnityProject/Assets/Change/Runtime/UI/Core/WindowManager.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/WindowManagerPresenterHostTests.cs`

**设计要点（必须遵守）**

1. `WindowManager` 构造函数增加 `IWindowPresenterHost host` 参数，默认传 `NullWindowPresenterHost.Instance` 的**重载**或要求调用点显式传入—为减少破坏，推荐 **新增重载构造函数** `WindowManager(IWindowFactory factory, IWindowPresenterHost host)`，旧单参构造调用 `this(factory, NullWindowPresenterHost.Instance)`。
2. `_opened` 从 `Dictionary<WindowRequest, IWindowView>` 改为 `Dictionary<WindowRequest, OpenedWindowEntry>`；`TryGet` 返回 `view` 时取 `entry.View`。
3. `OpenAsyncInternal` 在 `entry.Completion.TrySetResult(created)` 成功路径之后：先 `host.OnOpened(in request, created, out var scope, out var presenter)`，写入 `OpenedWindowEntry`；随后 **`WindowManager` 调用 `presenter?.OnOpen()`**（`host` 不调用 `OnOpen`）。策略 C：`host` 内部可通过 `CreateChild` 创建 `windowScope`。
4. `Close`：顺序固定为 **`presenter?.OnClose()`（由 `WindowManager` 调用）→ `entry.WindowScope?.Dispose()` → `host.OnClosing(...)`（仅用于 host 侧遥测/额外清理，不得再调用 `presenter.OnClose()`）→ `view.SetState(Closing)` / `view.Dispose()`**。

`IWindowPresenterHost.cs`:

```csharp
using System;
using Change.Framework.Application;
using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public interface IWindowPresenterHost
    {
        void OnOpened(in WindowRequest request, IWindowView view, out IDisposable windowScope, out IPresenter presenter);
        void OnClosing(in WindowRequest request, IWindowView view, IPresenter presenter, IDisposable windowScope);
    }
}
```

`NullWindowPresenterHost.cs`：`presenter = null; windowScope = null;`；`OnClosing` no-op。

- [ ] **Step 1: 新建 `WindowManagerPresenterHostTests` + FakeHost 断言顺序**

```csharp
using System;
using Change.Framework.Application;
using Change.Framework.UI;
using Change.Runtime.UI;
using NUnit.Framework;

namespace Change.Runtime.Tests.UI
{
    public sealed class WindowManagerPresenterHostTests
    {
        private sealed class SequenceHost : IWindowPresenterHost
        {
            public int Opened;
            public int Closed;

            public void OnOpened(in WindowRequest request, IWindowView view, out IDisposable windowScope, out IPresenter presenter)
            {
                Opened++;
                windowScope = null;
                presenter = new DummyPresenter();
            }

            public void OnClosing(in WindowRequest request, IWindowView view, IPresenter presenter, IDisposable windowScope)
            {
                Closed++;
            }

            private sealed class DummyPresenter : IPresenter
            {
                public int CloseCount;
                public void OnOpen() { }
                public void OnClose() => CloseCount++;
            }
        }
    }
}
```

按 `WindowManagerCoreTests` 的同一套 `IWindowFactory` fake 风格补全 `OpenAsync`/`Close` 用例：`Open` 后 `SequenceHost.Opened==1` 且 `DummyPresenter` 收到 `OnOpen`；`Close` 后 **`WindowManager` 已调用 `presenter.OnClose()`** 故 `CloseCount==1`，且 `SequenceHost.Closed==1`（`OnClosing` 在 `OnClose` 与 scope dispose 之后调用）。

- [ ] **Step 2: 运行失败 → 实现 `WindowManager` 改动 → PASS**

- [ ] **Step 3: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/UI/Core UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/WindowManagerPresenterHostTests.cs
git commit -m "feat(runtime): add optional presenter host to WindowManager"
```

---

### Task 6: 文档同步（短）

**Files:**

- Modify: `UnityProject/Assets/Change/Runtime/UI/README.md`（如存在；否则 Create）—增加 **Presenter 宿主**、**策略 C**、**与 VContainer 组合** 各 3~5 行说明并链接到 spec。

- [ ] **Step 1: 更新 README + Commit**

```bash
git add UnityProject/Assets/Change/Runtime/UI/README.md
git commit -m "docs(runtime): document presenter host and composition usage"
```

---

## Self-Review（计划自检）

**1. Spec coverage**

| Spec 章节 | 对应 Task |
|-----------|-----------|
| Root / GameRoot / 热更 Install | Task 4 |
| 双通道 A（Gateway） | Task 2 |
| 混合通信 C + 白名单 | Task 3 |
| `WindowManager` 宿主 + 策略 C 钩子 | Task 5 |
| 测试与可替换 fake | Task 2/3/4/5 |
| ECS 不在范围 | 未安排任务 ✓ |

**2. Placeholder scan**

无 TBD/TODO 占位；每步有可执行命令或代码。

**3. Type consistency**

- `IWindowPresenterHost` 与 `IPresenter`（`Change.Framework.Application`）一致。
- `INetworkGateway` / `NoOpNetworkGateway` 在 `EngineLifetimeScope` 与测试中同名。

**已知缺口（刻意 YAGNI，可在下一 plan 补）**

- HybridCLR 真机入口 `RuntimeApi` 调用链、YooAsset 下载后 `LoadAssembly` 的 **具体 MonoBehaviour 放置场景** 未在本 plan 展开（避免与壳任务耦合）；应在 `GameCompositionHost` 稳定后单独立项。
- `WindowScope` 的 **VContainer `CreateChild` 封装类** 可在 Task 5 的 FakeHost 示例中先出现测试专用实现，生产路径后续任务化。

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-05-09-change-client-shell-composition-implementation-plan.md`. Two execution options:**

1. **Subagent-Driven（推荐）** — 每个 Task 派生子代理，任务间 review，迭代快（配合 `superpowers:subagent-driven-development`）。
2. **Inline Execution** — 本会话内按 Task 批量执行，带检查点（配合 `superpowers:executing-plans`）。

**Which approach?**
