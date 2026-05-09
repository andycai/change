# Quest System (Main / Side / Daily) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.  
> **Worktree:** Per `AGENTS.md`, run `subagent-driven-development` / `executing-plans` under `.worktrees/` (git worktree), not directly on `main` if that is your team rule for those skills.

**Goal:** Ship a FairyGUI quest window with three tabs (main linear, side parallel, daily session-scoped), backed by in-memory mock state and CQRS commands/queries, openable from a dev-only hotkey path, with EditMode tests for domain rules.

**Architecture:** Mirror `GameScript/UI/Inventory`: Presenter → use-case interface → `ICqrsBus`; handlers mutate a singleton `QuestSessionState` and a `QuestRewardWallet`; one primary query returns a panel snapshot; commands fail-fast on illegal claims. Register handlers via `CqrsBootstrap` + `RegisterBuildCallback` in `GameHotfixInstaller`; register `WindowManager` / YooAsset loader / `IWindowPresenterHost` in `GameHotfixRootScope` (GameScript cannot construct `YooUiAssetLoader` without `YooUiAssetLoader.FromResourcePackage`). Add `GameScript.asmdef` so EditMode tests reference the same assembly.

**Tech stack:** Unity 2022.3, `Change.Framework` (CQRS, `WindowId`), `Change.Runtime` (FairyGUI window shell, optional `YooUiAssetLoader`), VContainer, FairyGUI, UniTask for `WindowManager.OpenAsync`.

---

## File map (create / modify)

| Path | Responsibility |
|------|----------------|
| `UnityProject/Assets/GameScript/GameScript.asmdef` | Name assembly `GameScript`; reference `Change.Framework`, `Change.Runtime`, `FairyGUI`, `UniTask`, `VContainer`, `YooAsset`. |
| `UnityProject/Assets/GameScript/Tests/EditMode/GameScript.EditModeTests.asmdef` | Test assembly referencing `GameScript`, `Change.Framework`, `UnityEngine.TestRunner`. |
| `UnityProject/Assets/GameScript/UI/Quest/QuestSessionState.cs` | In-memory quest rows, main pointer, daily generation on construct, reset semantics. |
| `UnityProject/Assets/GameScript/UI/Quest/QuestRewardWallet.cs` | Mutable `Gold` updated by claim commands. |
| `UnityProject/Assets/GameScript/UI/Quest/QuestMessages.cs` | `readonly struct` queries/commands + result DTOs (`QuestPanelSnapshot`, row view structs). |
| `UnityProject/Assets/GameScript/UI/Quest/QuestQueryHandlers.cs` | `GetQuestPanelQueryHandler`. |
| `UnityProject/Assets/GameScript/UI/Quest/QuestCommandHandlers.cs` | Handlers for main progress/complete, side claim, daily claim, optional `BumpProgress` dev helpers. |
| `UnityProject/Assets/GameScript/UI/Quest/QuestWindowContracts.cs` | `OpenQuestPanelRequest`, `IQuestWindowView`, `IOpenQuestPanelUseCase`, view models. |
| `UnityProject/Assets/GameScript/UI/Quest/OpenQuestPanelUseCase.cs` | Calls `GetQuestPanelQuery`. |
| `UnityProject/Assets/GameScript/UI/Quest/QuestWindowPresenter.cs` | `IPresenter`, tab refresh orchestration. |
| `UnityProject/Assets/GameScript/UI/Quest/QuestFairyGuiView.cs` | Binds `GComponent` children to `Apply(in QuestWindowViewModel)`. |
| `UnityProject/Assets/GameScript/Composition/GameHotfixInstaller.cs` | Register `QuestSessionState`, `QuestRewardWallet`, `CqrsBus`, `ICqrsBus`, use cases; `RegisterBuildCallback` registers handlers + `Build()`. |
| `UnityProject/Assets/GameScript/Composition/GameHotfixRootScope.cs` | Child `LifetimeScope`: `ResourcePackage`, `IUiAssetLoader`, `WindowManager`, resolver, `IWindowPresenterHost`, quest factory. |
| `UnityProject/Assets/Change/Runtime/UI/Loading/YooUiAssetLoader.cs` | Add `FromResourcePackage` public static factory. |
| `UnityProject/Assets/Change/Framework/UI/Abstractions/WindowIds.cs` | Add `Quest` window id constant. |
| `UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowView.cs` | Expose read-only `GComponent Root` for presenter host binding. |
| `UnityProject/Assets/Change/Runtime/UI/Core/StaticWindowLocationResolver.cs` | Map `WindowIds.Quest` → `"ui/quest_panel.prefab"` (match YooAsset collector convention used by `YooUiAssetLoaderTests`). |
| `UnityProject/Assets/Change/Runtime/UI/Abstractions/IQuestWindowPresenterFactory.cs` | Runtime abstraction so `QuestGamePresenterHost` does not reference `GameScript`. |
| `UnityProject/Assets/Change/Runtime/UI/QuestGamePresenterHost.cs` | `IWindowPresenterHost` for `WindowIds.Quest`; uses `IQuestWindowPresenterFactory.Create(fairy.Root)`. |
| `UnityProject/Assets/GameScript/UI/Quest/QuestWindowPresenterFactory.cs` | Implements `IQuestWindowPresenterFactory`. |
| `UnityProject/Assets/GameScript/GameFlow/Entry/GameFlowDemoDriver.cs` | Add dev hotkey (e.g. `F4`) to open quest window via resolved `WindowManager` + `UniTask` fire-and-forget guard. |
| `UnityProject/Assets/GameScript/Tests/EditMode/Quest/QuestSessionStateTests.cs` | NUnit tests for main ordering, side isolation, daily single-claim, illegal claim throws. |
| `UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/GameScriptSampleContractTests.cs` | Add parallel tests for `QuestWindowPresenter` / `IOpenQuestPanelUseCase` mirroring inventory rules. |
| FairyGUI package + prefab | Author `quest_panel` in FairyGUI editor; export Unity prefab with `UIPanel` at path resolved by `StaticWindowLocationResolver`. |

---

### Task 1: Add `GameScript.asmdef` (unblocks explicit references)

**Files:**
- Create: `UnityProject/Assets/GameScript/GameScript.asmdef`
- Modify: none

- [ ] **Step 1: Create asmdef JSON**

```json
{
  "name": "GameScript",
  "rootNamespace": "GameScript",
  "references": [
    "Change.Framework",
    "Change.Runtime",
    "FairyGUI",
    "UniTask",
    "VContainer",
    "YooAsset"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 2: Open Unity once** so the editor regenerates `.csproj` and verifies GameScript compiles (expect zero errors before new quest files exist).

- [ ] **Step 3: Commit**

```bash
git add UnityProject/Assets/GameScript/GameScript.asmdef
git commit -m "build: add GameScript asmdef for quest work"
```

---

### Task 2: `QuestRewardWallet` + `QuestSessionState` with seeded data

**Files:**
- Create: `UnityProject/Assets/GameScript/UI/Quest/QuestRewardWallet.cs`
- Create: `UnityProject/Assets/GameScript/UI/Quest/QuestSessionState.cs`

- [ ] **Step 1: Write `QuestRewardWallet.cs`**

```csharp
namespace GameScript.UI.Quest
{
    public sealed class QuestRewardWallet
    {
        public int Gold { get; private set; }

        public void AddGold(int amount)
        {
            if (amount < 0) throw new System.ArgumentOutOfRangeException(nameof(amount));
            Gold += amount;
        }
    }
}
```

- [ ] **Step 2: Write `QuestSessionState.cs`** (seed: 3 main steps targets 2 each; 2 side quests target 3 gold 10 each; 2 dailies target 1 gold 5 each; daily `RewardClaimed` flag)

```csharp
using System;
using System.Collections.Generic;

namespace GameScript.UI.Quest
{
    public sealed class QuestSessionState
    {
        public int MainIndex { get; private set; }
        public int MainProgress { get; private set; }
        public int MainTarget { get; private set; }
        public bool MainCompleted { get; private set; }

        public readonly List<SideQuestRow> Sides = new();
        public readonly List<DailyQuestRow> Dailies = new();

        private static readonly int[] MainTargets = { 2, 2, 2 };

        public QuestSessionState()
        {
            Reset();
        }

        public void Reset()
        {
            MainIndex = 0;
            MainProgress = 0;
            MainTarget = MainTargets[0];
            MainCompleted = false;
            Sides.Clear();
            Sides.Add(new SideQuestRow(1, 3, 10));
            Sides.Add(new SideQuestRow(2, 3, 15));
            Dailies.Clear();
            Dailies.Add(new DailyQuestRow(1, 1, 5));
            Dailies.Add(new DailyQuestRow(2, 1, 5));
        }

        public void BumpMainProgress(int delta)
        {
            if (MainCompleted) throw new InvalidOperationException("Main quest already finished.");
            if (delta <= 0) throw new ArgumentOutOfRangeException(nameof(delta));
            MainProgress = Math.Min(MainProgress + delta, MainTarget);
        }

        public void CompleteMainIfReady()
        {
            if (MainCompleted) throw new InvalidOperationException("Main quest already finished.");
            if (MainProgress < MainTarget) throw new InvalidOperationException("Main quest progress insufficient.");
            if (MainIndex >= MainTargets.Length - 1)
            {
                MainCompleted = true;
                return;
            }
            MainIndex++;
            MainProgress = 0;
            MainTarget = MainTargets[MainIndex];
        }

        public SideQuestRow GetSideOrThrow(int id)
        {
            var row = Sides.Find(x => x.Id == id);
            if (row == null) throw new InvalidOperationException($"Unknown side quest {id}.");
            return row;
        }

        public DailyQuestRow GetDailyOrThrow(int id)
        {
            var row = Dailies.Find(x => x.Id == id);
            if (row == null) throw new InvalidOperationException($"Unknown daily quest {id}.");
            return row;
        }
    }

    public sealed class SideQuestRow
    {
        public SideQuestRow(int id, int target, int rewardGold)
        {
            Id = id;
            Target = target;
            RewardGold = rewardGold;
        }

        public int Id { get; }
        public int Target { get; }
        public int RewardGold { get; }
        public int Progress { get; set; }
        public bool RewardClaimed { get; set; }

        public bool CanClaim => !RewardClaimed && Progress >= Target;
    }

    public sealed class DailyQuestRow
    {
        public DailyQuestRow(int id, int target, int rewardGold)
        {
            Id = id;
            Target = target;
            RewardGold = rewardGold;
        }

        public int Id { get; }
        public int Target { get; }
        public int RewardGold { get; }
        public int Progress { get; set; }
        public bool RewardClaimed { get; set; }

        public bool CanClaim => !RewardClaimed && Progress >= Target;
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add UnityProject/Assets/GameScript/UI/Quest/QuestRewardWallet.cs \
        UnityProject/Assets/GameScript/UI/Quest/QuestSessionState.cs
git commit -m "feat(quest): add in-memory session state and wallet"
```

---

### Task 3: CQRS messages + query handler

**Files:**
- Create: `UnityProject/Assets/GameScript/UI/Quest/QuestMessages.cs`
- Create: `UnityProject/Assets/GameScript/UI/Quest/QuestQueryHandlers.cs`

- [ ] **Step 1: Write structs + DTOs in `QuestMessages.cs`**

```csharp
using System.Collections.Generic;
using Change.Framework.Cqrs;

namespace GameScript.UI.Quest
{
    public readonly struct GetQuestPanelQuery : IQuery<QuestPanelSnapshot>
    {
    }

    public sealed class QuestPanelSnapshot
    {
        public MainQuestVm Main;
        public IReadOnlyList<SideQuestVm> Sides;
        public IReadOnlyList<DailyQuestVm> Dailies;
        public int WalletGold;
    }

    public readonly struct MainQuestVm
    {
        public MainQuestVm(int index, int progress, int target, bool completed)
        {
            Index = index;
            Progress = progress;
            Target = target;
            Completed = completed;
        }

        public int Index { get; }
        public int Progress { get; }
        public int Target { get; }
        public bool Completed { get; }
    }

    public readonly struct SideQuestVm
    {
        public SideQuestVm(int id, int progress, int target, int rewardGold, bool canClaim, bool claimed)
        {
            Id = id;
            Progress = progress;
            Target = target;
            RewardGold = rewardGold;
            CanClaim = canClaim;
            Claimed = claimed;
        }

        public int Id { get; }
        public int Progress { get; }
        public int Target { get; }
        public int RewardGold { get; }
        public bool CanClaim { get; }
        public bool Claimed { get; }
    }

    public readonly struct DailyQuestVm
    {
        public DailyQuestVm(int id, int progress, int target, int rewardGold, bool canClaim, bool claimed)
        {
            Id = id;
            Progress = progress;
            Target = target;
            RewardGold = rewardGold;
            CanClaim = canClaim;
            Claimed = claimed;
        }

        public int Id { get; }
        public int Progress { get; }
        public int Target { get; }
        public int RewardGold { get; }
        public bool CanClaim { get; }
        public bool Claimed { get; }
    }
}
```

- [ ] **Step 2: Implement `GetQuestPanelQueryHandler` in `QuestQueryHandlers.cs`**

```csharp
using System.Collections.Generic;
using Change.Framework.Cqrs;

namespace GameScript.UI.Quest
{
    public sealed class GetQuestPanelQueryHandler : IQueryHandler<GetQuestPanelQuery, QuestPanelSnapshot>
    {
        private readonly QuestSessionState _state;
        private readonly QuestRewardWallet _wallet;

        public GetQuestPanelQueryHandler(QuestSessionState state, QuestRewardWallet wallet)
        {
            _state = state;
            _wallet = wallet;
        }

        public QuestPanelSnapshot Handle(in GetQuestPanelQuery query)
        {
            var sides = new List<SideQuestVm>(_state.Sides.Count);
            foreach (var row in _state.Sides)
            {
                sides.Add(new SideQuestVm(row.Id, row.Progress, row.Target, row.RewardGold, row.CanClaim, row.RewardClaimed));
            }

            var dailies = new List<DailyQuestVm>(_state.Dailies.Count);
            foreach (var row in _state.Dailies)
            {
                dailies.Add(new DailyQuestVm(row.Id, row.Progress, row.Target, row.RewardGold, row.CanClaim, row.RewardClaimed));
            }

            return new QuestPanelSnapshot
            {
                Main = new MainQuestVm(_state.MainIndex, _state.MainProgress, _state.MainTarget, _state.MainCompleted),
                Sides = sides,
                Dailies = dailies,
                WalletGold = _wallet.Gold
            };
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add UnityProject/Assets/GameScript/UI/Quest/QuestMessages.cs \
        UnityProject/Assets/GameScript/UI/Quest/QuestQueryHandlers.cs
git commit -m "feat(quest): add panel query and handler"
```

---

### Task 4: Command handlers (main bump/complete, side/daily claim)

**Files:**
- Create: `UnityProject/Assets/GameScript/UI/Quest/QuestCommandHandlers.cs`
- Modify: `UnityProject/Assets/GameScript/UI/Quest/QuestMessages.cs` (append commands)

- [ ] **Step 1: Append command structs to `QuestMessages.cs`**

```csharp
    public readonly struct BumpMainQuestProgressCommand : ICommand
    {
        public BumpMainQuestProgressCommand(int delta)
        {
            Delta = delta;
        }

        public int Delta { get; }
    }

    public readonly struct AdvanceMainQuestStepCommand : ICommand
    {
    }

    public readonly struct ClaimSideQuestRewardCommand : ICommand
    {
        public ClaimSideQuestRewardCommand(int sideId)
        {
            SideId = sideId;
        }

        public int SideId { get; }
    }

    public readonly struct ClaimDailyQuestRewardCommand : ICommand
    {
        public ClaimDailyQuestRewardCommand(int dailyId)
        {
            DailyId = dailyId;
        }

        public int DailyId { get; }
    }

    public readonly struct BumpSideQuestProgressCommand : ICommand
    {
        public BumpSideQuestProgressCommand(int sideId, int delta)
        {
            SideId = sideId;
            Delta = delta;
        }

        public int SideId { get; }
        public int Delta { get; }
    }

    public readonly struct BumpDailyQuestProgressCommand : ICommand
    {
        public BumpDailyQuestProgressCommand(int dailyId, int delta)
        {
            DailyId = dailyId;
            Delta = delta;
        }

        public int DailyId { get; }
        public int Delta { get; }
    }
```

- [ ] **Step 2: Implement handlers in `QuestCommandHandlers.cs`**

```csharp
using System;
using Change.Framework.Cqrs;

namespace GameScript.UI.Quest
{
    public sealed class BumpMainQuestProgressHandler : ICommandHandler<BumpMainQuestProgressCommand>
    {
        private readonly QuestSessionState _state;

        public BumpMainQuestProgressHandler(QuestSessionState state) => _state = state;

        public void Handle(in BumpMainQuestProgressCommand command)
        {
            _state.BumpMainProgress(command.Delta);
        }
    }

    public sealed class AdvanceMainQuestStepHandler : ICommandHandler<AdvanceMainQuestStepCommand>
    {
        private readonly QuestSessionState _state;
        private readonly QuestRewardWallet _wallet;

        public AdvanceMainQuestStepHandler(QuestSessionState state, QuestRewardWallet wallet)
        {
            _state = state;
            _wallet = wallet;
        }

        public void Handle(in AdvanceMainQuestStepCommand command)
        {
            _state.CompleteMainIfReady();
            _wallet.AddGold(20);
        }
    }

    public sealed class BumpSideQuestProgressHandler : ICommandHandler<BumpSideQuestProgressCommand>
    {
        private readonly QuestSessionState _state;

        public BumpSideQuestProgressHandler(QuestSessionState state) => _state = state;

        public void Handle(in BumpSideQuestProgressCommand command)
        {
            var row = _state.GetSideOrThrow(command.SideId);
            if (row.RewardClaimed) throw new InvalidOperationException("Side quest already claimed.");
            row.Progress = Math.Min(row.Progress + command.Delta, row.Target);
        }
    }

    public sealed class ClaimSideQuestRewardHandler : ICommandHandler<ClaimSideQuestRewardCommand>
    {
        private readonly QuestSessionState _state;
        private readonly QuestRewardWallet _wallet;

        public ClaimSideQuestRewardHandler(QuestSessionState state, QuestRewardWallet wallet)
        {
            _state = state;
            _wallet = wallet;
        }

        public void Handle(in ClaimSideQuestRewardCommand command)
        {
            var row = _state.GetSideOrThrow(command.SideId);
            if (!row.CanClaim) throw new InvalidOperationException("Side quest not claimable.");
            row.RewardClaimed = true;
            _wallet.AddGold(row.RewardGold);
        }
    }

    public sealed class BumpDailyQuestProgressHandler : ICommandHandler<BumpDailyQuestProgressCommand>
    {
        private readonly QuestSessionState _state;

        public BumpDailyQuestProgressHandler(QuestSessionState state) => _state = state;

        public void Handle(in BumpDailyQuestProgressCommand command)
        {
            var row = _state.GetDailyOrThrow(command.DailyId);
            if (row.RewardClaimed) throw new InvalidOperationException("Daily quest reward already claimed.");
            row.Progress = Math.Min(row.Progress + command.Delta, row.Target);
        }
    }

    public sealed class ClaimDailyQuestRewardHandler : ICommandHandler<ClaimDailyQuestRewardCommand>
    {
        private readonly QuestSessionState _state;
        private readonly QuestRewardWallet _wallet;

        public ClaimDailyQuestRewardHandler(QuestSessionState state, QuestRewardWallet wallet)
        {
            _state = state;
            _wallet = wallet;
        }

        public void Handle(in ClaimDailyQuestRewardCommand command)
        {
            var row = _state.GetDailyOrThrow(command.DailyId);
            if (!row.CanClaim) throw new InvalidOperationException("Daily quest not claimable.");
            row.RewardClaimed = true;
            _wallet.AddGold(row.RewardGold);
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add UnityProject/Assets/GameScript/UI/Quest/QuestMessages.cs \
        UnityProject/Assets/GameScript/UI/Quest/QuestCommandHandlers.cs
git commit -m "feat(quest): add command handlers for progress and claims"
```

---

### Task 5: Use case, contracts, presenter, FairyGUI view

**Files:**
- Create: `UnityProject/Assets/GameScript/UI/Quest/QuestWindowContracts.cs`
- Create: `UnityProject/Assets/GameScript/UI/Quest/OpenQuestPanelUseCase.cs`
- Create: `UnityProject/Assets/GameScript/UI/Quest/QuestWindowPresenter.cs`
- Create: `UnityProject/Assets/GameScript/UI/Quest/QuestFairyGuiView.cs`

- [ ] **Step 1: Write contracts + view model**

```csharp
using Change.Framework.Application;

namespace GameScript.UI.Quest
{
    public readonly struct OpenQuestPanelRequest
    {
        public OpenQuestPanelRequest(int activeTabIndex)
        {
            ActiveTabIndex = activeTabIndex;
        }

        public int ActiveTabIndex { get; }
    }

    public interface IQuestWindowView
    {
        void Apply(in QuestWindowViewModel model);
    }

    public interface IOpenQuestPanelUseCase : IUseCase<OpenQuestPanelRequest, QuestWindowViewModel>
    {
    }

    public readonly struct QuestWindowViewModel
    {
        public QuestWindowViewModel(QuestPanelSnapshot snapshot, int activeTabIndex)
        {
            Snapshot = snapshot;
            ActiveTabIndex = activeTabIndex;
        }

        public QuestPanelSnapshot Snapshot { get; }
        public int ActiveTabIndex { get; }
    }
}
```

- [ ] **Step 2: Write `OpenQuestPanelUseCase.cs`**

```csharp
using Change.Framework.Cqrs;

namespace GameScript.UI.Quest
{
    public sealed class OpenQuestPanelUseCase : IOpenQuestPanelUseCase
    {
        private readonly ICqrsBus _bus;

        public OpenQuestPanelUseCase(ICqrsBus bus)
        {
            _bus = bus;
        }

        public QuestWindowViewModel Execute(in OpenQuestPanelRequest request)
        {
            var snapshot = _bus.Ask<GetQuestPanelQuery, QuestPanelSnapshot>(new GetQuestPanelQuery());
            return new QuestWindowViewModel(snapshot, request.ActiveTabIndex);
        }
    }
}
```

- [ ] **Step 3: Write `QuestWindowPresenter.cs`**

```csharp
using System;
using Change.Framework.Application;

namespace GameScript.UI.Quest
{
    public sealed class QuestWindowPresenter : IPresenter
    {
        private readonly IQuestWindowView _view;
        private readonly IOpenQuestPanelUseCase _openPanel;
        private int _activeTab;

        public QuestWindowPresenter(IQuestWindowView view, IOpenQuestPanelUseCase openPanel)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _openPanel = openPanel ?? throw new ArgumentNullException(nameof(openPanel));
        }

        public void SetActiveTab(int index)
        {
            _activeTab = index;
        }

        public void OnOpen()
        {
            var vm = _openPanel.Execute(new OpenQuestPanelRequest(_activeTab));
            _view.Apply(in vm);
        }

        public void OnClose()
        {
        }
    }
}
```

- [ ] **Step 4: Write `QuestFairyGuiView.cs`** (names MUST match FairyGUI child names you publish; adjust strings after publishing UI)

```csharp
using FairyGUI;

namespace GameScript.UI.Quest
{
    public sealed class QuestFairyGuiView : IQuestWindowView
    {
        private readonly GComponent _root;

        public QuestFairyGuiView(GComponent root)
        {
            _root = root;
        }

        public void Apply(in QuestWindowViewModel model)
        {
            var snap = model.Snapshot;
            _root.GetChild("txtWallet").asTextField.text = snap.WalletGold.ToString();
            _root.GetChild("txtMainTitle").asTextField.text =
                snap.Main.Completed ? "主线已完成" : $"主线 第{snap.Main.Index + 1}步";
            _root.GetChild("txtMainProgress").asTextField.text =
                snap.Main.Completed ? "-" : $"{snap.Main.Progress}/{snap.Main.Target}";

            var listSide = _root.GetChild("listSide").asList;
            listSide.RemoveChildrenToPool();
            foreach (var row in snap.Sides)
            {
                var item = listSide.AddItemFromPool().asCom;
                item.GetChild("title").asTextField.text = $"支线 {row.Id}";
                item.GetChild("progress").asTextField.text = $"{row.Progress}/{row.Target}";
                item.GetChild("btnClaim").asButton.touchable = row.CanClaim;
            }

            var listDaily = _root.GetChild("listDaily").asList;
            listDaily.RemoveChildrenToPool();
            foreach (var row in snap.Dailies)
            {
                var item = listDaily.AddItemFromPool().asCom;
                item.GetChild("title").asTextField.text = $"日常 {row.Id}";
                item.GetChild("progress").asTextField.text = $"{row.Progress}/{row.Target}";
                item.GetChild("btnClaim").asButton.touchable = row.CanClaim;
            }

            var tabs = _root.GetChild("tabs").asController;
            if (tabs != null)
            {
                tabs.selectedIndex = model.ActiveTabIndex;
            }
        }
    }
}
```

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/GameScript/UI/Quest/QuestWindowContracts.cs \
        UnityProject/Assets/GameScript/UI/Quest/OpenQuestPanelUseCase.cs \
        UnityProject/Assets/GameScript/UI/Quest/QuestWindowPresenter.cs \
        UnityProject/Assets/GameScript/UI/Quest/QuestFairyGuiView.cs
git commit -m "feat(quest): add presenter, use case, and fairygui view"
```

---

### Task 6: `GameHotfixInstaller` CQRS wiring

**Files:**
- Modify: `UnityProject/Assets/GameScript/Composition/GameHotfixInstaller.cs`

- [ ] **Step 1: Replace installer body**

```csharp
using Change.Framework.Cqrs;
using GameScript.UI.Quest;
using VContainer;

namespace GameScript.Composition
{
    public sealed class GameHotfixInstaller : IHotfixGameInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<QuestSessionState>(Lifetime.Singleton);
            builder.Register<QuestRewardWallet>(Lifetime.Singleton);
            builder.Register<CqrsBus>(Lifetime.Singleton);
            builder.Register<ICqrsBus>(c => c.Resolve<CqrsBus>(), Lifetime.Singleton);
            builder.Register<IOpenQuestPanelUseCase, OpenQuestPanelUseCase>(Lifetime.Transient);

            builder.RegisterBuildCallback(c =>
            {
                var bus = c.Resolve<CqrsBus>();
                var bootstrap = new CqrsBootstrap(bus);
                var state = c.Resolve<QuestSessionState>();
                var wallet = c.Resolve<QuestRewardWallet>();

                bootstrap.RegisterQuery(new GetQuestPanelQueryHandler(state, wallet));
                bootstrap.RegisterCommand(new BumpMainQuestProgressHandler(state));
                bootstrap.RegisterCommand(new AdvanceMainQuestStepHandler(state, wallet));
                bootstrap.RegisterCommand(new BumpSideQuestProgressHandler(state));
                bootstrap.RegisterCommand(new ClaimSideQuestRewardHandler(state, wallet));
                bootstrap.RegisterCommand(new BumpDailyQuestProgressHandler(state));
                bootstrap.RegisterCommand(new ClaimDailyQuestRewardHandler(state, wallet));

                bootstrap.Build();
            });
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add UnityProject/Assets/GameScript/Composition/GameHotfixInstaller.cs
git commit -m "feat(quest): wire CQRS handlers in hotfix installer"
```

---

### Task 7: Window id + FairyGUI root exposure + resolver + presenter host

**Files:**
- Modify: `UnityProject/Assets/Change/Framework/UI/Abstractions/WindowIds.cs`
- Modify: `UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowView.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Core/StaticWindowLocationResolver.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/QuestGamePresenterHost.cs`
- Create: `UnityProject/Assets/Change/Runtime/UI/Abstractions/IQuestWindowPresenterFactory.cs`

- [ ] **Step 1: Add `WindowIds.Quest`**

```csharp
public static readonly WindowId Quest = new("Quest");
```

- [ ] **Step 2: Expose root on `FairyGuiWindowView`**

```csharp
public GComponent Root => _root;
```

- [ ] **Step 3: Implement `StaticWindowLocationResolver`**

```csharp
using System;
using System.Collections.Generic;
using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public sealed class StaticWindowLocationResolver : IWindowLocationResolver
    {
        private readonly IReadOnlyDictionary<string, string> _map;

        public StaticWindowLocationResolver(IReadOnlyDictionary<string, string> map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public string ResolvePrefabLocation(WindowId id)
        {
            if (_map.TryGetValue(id.Value, out var path))
            {
                return path;
            }

            throw new InvalidOperationException($"No prefab mapping for window `{id.Value}`.");
        }
    }
}
```

- [ ] **Step 4: Add `UnityProject/Assets/Change/Runtime/UI/Abstractions/IQuestWindowPresenterFactory.cs`**

```csharp
using Change.Framework.Application;
using FairyGUI;

namespace Change.Runtime.UI
{
    public interface IQuestWindowPresenterFactory
    {
        IPresenter Create(GComponent root);
    }
}
```

- [ ] **Step 5: Implement `QuestGamePresenterHost` in `Change/Runtime/UI/QuestGamePresenterHost.cs`**

```csharp
using System;
using Change.Framework.Application;
using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public sealed class QuestGamePresenterHost : IWindowPresenterHost
    {
        private readonly IQuestWindowPresenterFactory _factory;

        public QuestGamePresenterHost(IQuestWindowPresenterFactory factory)
        {
            _factory = factory;
        }

        public void OnOpened(in WindowRequest request, IWindowView view, out IDisposable windowScope, out IPresenter presenter)
        {
            if (view is FairyGuiWindowView fairy && request.Id == WindowIds.Quest)
            {
                presenter = _factory.Create(fairy.Root);
                windowScope = null;
                return;
            }

            throw new InvalidOperationException($"Unsupported window `{request.Id.Value}` for QuestGamePresenterHost.");
        }

        public void OnClosing(in WindowRequest request, IWindowView view, IPresenter presenter, IDisposable windowScope)
        {
            windowScope?.Dispose();
        }
    }
}
```

- [ ] **Step 6: Add `QuestWindowPresenterFactory` in GameScript** (`GameScript/UI/Quest/QuestWindowPresenterFactory.cs`)

```csharp
using Change.Framework.Application;
using Change.Runtime.UI;
using FairyGUI;

namespace GameScript.UI.Quest
{
    public sealed class QuestWindowPresenterFactory : IQuestWindowPresenterFactory
    {
        private readonly IOpenQuestPanelUseCase _useCase;

        public QuestWindowPresenterFactory(IOpenQuestPanelUseCase useCase)
        {
            _useCase = useCase;
        }

        public IPresenter Create(GComponent root)
        {
            return new QuestWindowPresenter(new QuestFairyGuiView(root), _useCase);
        }
    }
}
```

- [ ] **Step 7:** Defer `IQuestWindowPresenterFactory` / `IWindowPresenterHost` / `WindowManager` registration to **Task 8** (`GameHotfixRootScope`) so `ResourcePackage` and `IUiAssetLoader` resolve in one place. Task 6 installer stays CQRS-only until Task 8 merges window registrations.

- [ ] **Step 8: Commit**

```bash
git add UnityProject/Assets/Change/Framework/UI/Abstractions/WindowIds.cs \
        UnityProject/Assets/Change/Runtime/UI/FairyGui/FairyGuiWindowView.cs \
        UnityProject/Assets/Change/Runtime/UI/Core/StaticWindowLocationResolver.cs \
        UnityProject/Assets/Change/Runtime/UI/Abstractions/IQuestWindowPresenterFactory.cs \
        UnityProject/Assets/Change/Runtime/UI/QuestGamePresenterHost.cs \
        UnityProject/Assets/GameScript/UI/Quest/QuestWindowPresenterFactory.cs
git commit -m "feat(quest): window id, resolver, presenter host, factory"
```

---

### Task 8: `YooUiAssetLoader` public entry + hotfix DI + demo hotkey

**Files:**
- Modify: `UnityProject/Assets/Change/Runtime/UI/Loading/YooUiAssetLoader.cs` — add **public** static factory so **GameScript** (external assembly) can construct without calling `internal` ctor.
- Modify: `UnityProject/Assets/GameScript/Composition/GameHotfixInstaller.cs` — CQRS only (window stack lives in `GameHotfixRootScope`).
- Create: `UnityProject/Assets/GameScript/Composition/GameHotfixRootScope.cs` — `LifetimeScope` child of engine scope; `Configure` calls `new GameHotfixInstaller().Install(builder)` plus `WindowManager` / `FairyGuiWindowFactory` / `IUiAssetLoader` / resolver / host registrations.
- Modify: `UnityProject/Assets/GameScript/GameFlow/Entry/GameFlowDemoDriver.cs`

- [ ] **Step 1: Add to `YooUiAssetLoader` (same file, `Change.Runtime` assembly)**

```csharp
public static IUiAssetLoader FromResourcePackage(ResourcePackage package)
{
    if (package == null) throw new ArgumentNullException(nameof(package));
    return new YooUiAssetLoader(new YooAssetPackageAdapter(package));
}
```

(`YooAssetPackageAdapter` is `internal` in the same file — the static factory stays inside `Change.Runtime`, so this compiles.)

- [ ] **Step 2: `GameHotfixRootScope.cs`** — minimal child scope; assign engine parent in Inspector or `Awake`:

```csharp
using UnityEngine;
using VContainer;
using VContainer.Unity;
using YooAsset;

namespace GameScript.Composition
{
    public sealed class GameHotfixRootScope : LifetimeScope
    {
        [SerializeField] private LifetimeScope _engineScope;
        [SerializeField] private ResourcePackage _uiPackage;

        protected override void Awake()
        {
            if (_engineScope != null)
            {
                var p = parentReference;
                p.Object = _engineScope;
                parentReference = p;
            }
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            new GameHotfixInstaller().Install(builder);
            // Immediately after: paste the window-stack registrations from Step 3 (RegisterInstance _uiPackage, IUiAssetLoader, …).
        }
    }
}
```

- [ ] **Step 3: Window stack registration (single pattern)** — keep `GameHotfixInstaller` **CQRS-only**. In `GameHotfixRootScope.Configure`, **after** `new GameHotfixInstaller().Install(builder)`:

  1. `builder.RegisterInstance(_uiPackage);` where `_uiPackage` is `[SerializeField] private ResourcePackage _uiPackage;` on `GameHotfixRootScope`.
  2. Register window services:

```csharp
using System.Collections.Generic;
using Change.Framework.UI;
using Change.Runtime.UI;
using GameScript.UI.Quest;
using VContainer;
using YooAsset;

// GameHotfixRootScope.Configure, after GameHotfixInstaller:
builder.RegisterInstance(_uiPackage);
builder.Register<IUiAssetLoader>(c => YooUiAssetLoader.FromResourcePackage(c.Resolve<ResourcePackage>()), Lifetime.Singleton);
builder.Register<IWindowLocationResolver>(_ =>
    new StaticWindowLocationResolver(new Dictionary<string, string>
    {
        [WindowIds.Inventory.Value] = "ui/inventory.prefab",
        [WindowIds.Quest.Value] = "ui/quest_panel.prefab"
    }), Lifetime.Singleton);
builder.Register<IWindowFactory, FairyGuiWindowFactory>(Lifetime.Singleton);
builder.Register(c => new WindowManager(c.Resolve<IWindowFactory>(), c.Resolve<IWindowPresenterHost>()), Lifetime.Singleton);
builder.Register<IQuestWindowPresenterFactory, QuestWindowPresenterFactory>(Lifetime.Singleton);
builder.Register<IWindowPresenterHost, QuestGamePresenterHost>(Lifetime.Singleton);
```

- [ ] **Step 4: `GameFlowDemoDriver`** — add `[SerializeField] private GameHotfixRootScope _hotfixScope;` and `KeyCode.F4` branch:

```csharp
using Change.Framework.UI;
using Change.Runtime.UI;
using Cysharp.Threading.Tasks;

// inside Update, after null checks:
if (Input.GetKeyDown(KeyCode.F4) && _hotfixScope != null && _hotfixScope.Container != null)
{
    var wm = _hotfixScope.Container.Resolve<WindowManager>();
    var request = new WindowRequest(WindowIds.Quest, WindowOpenOptions.Default);
    wm.OpenAsync(in request, this.GetCancellationTokenOnDestroy()).Forget();
}
```

(`GetCancellationTokenOnDestroy()` requires `MonoBehaviour` — `GameFlowDemoDriver` already inherits `MonoBehaviour`.)

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/UI/Loading/YooUiAssetLoader.cs \
        UnityProject/Assets/GameScript/Composition/GameHotfixRootScope.cs \
        UnityProject/Assets/GameScript/Composition/GameHotfixInstaller.cs \
        UnityProject/Assets/GameScript/GameFlow/Entry/GameFlowDemoDriver.cs
git commit -m "feat(quest): yoo loader factory, hotfix root scope, F4 opens quest"
```

---

### Task 9: EditMode tests (`GameScript.EditModeTests`)

**Files:**
- Create: `UnityProject/Assets/GameScript/Tests/EditMode/GameScript.EditModeTests.asmdef`
- Create: `UnityProject/Assets/GameScript/Tests/EditMode/Quest/QuestSessionStateTests.cs`

`GameScript.EditModeTests.asmdef`:

```json
{
  "name": "GameScript.EditModeTests",
  "rootNamespace": "GameScript.Tests",
  "references": [
    "GameScript",
    "Change.Framework",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "optionalUnityReferences": [
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ]
}
```

- [ ] **Step 1: Write `QuestSessionStateTests.cs`** (construct bus via same pattern as `GameHotfixInstaller` test helper)

```csharp
using System;
using Change.Framework.Cqrs;
using GameScript.UI.Quest;
using NUnit.Framework;

namespace GameScript.Tests.Quest
{
    public sealed class QuestSessionStateTests
    {
        private static ICqrsBus CreateBus(out QuestSessionState state, out QuestRewardWallet wallet)
        {
            var bus = new CqrsBus();
            var bootstrap = new CqrsBootstrap(bus);
            state = new QuestSessionState();
            wallet = new QuestRewardWallet();
            bootstrap.RegisterQuery(new GetQuestPanelQueryHandler(state, wallet));
            bootstrap.RegisterCommand(new BumpMainQuestProgressHandler(state));
            bootstrap.RegisterCommand(new AdvanceMainQuestStepHandler(state, wallet));
            bootstrap.RegisterCommand(new BumpSideQuestProgressHandler(state));
            bootstrap.RegisterCommand(new ClaimSideQuestRewardHandler(state, wallet));
            bootstrap.RegisterCommand(new BumpDailyQuestProgressHandler(state));
            bootstrap.RegisterCommand(new ClaimDailyQuestRewardHandler(state, wallet));
            bootstrap.Build();
            return bus;
        }

        [Test]
        public void MainQuest_Linear_AcrossSteps()
        {
            var bus = CreateBus(out var state, out _);
            bus.Send(new BumpMainQuestProgressCommand(2));
            bus.Send(new AdvanceMainQuestStepCommand());
            Assert.AreEqual(1, state.MainIndex);
            bus.Send(new BumpMainQuestProgressCommand(2));
            bus.Send(new AdvanceMainQuestStepCommand());
            Assert.AreEqual(2, state.MainIndex);
        }

        [Test]
        public void SideQuests_ClaimIndependent()
        {
            var bus = CreateBus(out _, out var wallet);
            bus.Send(new BumpSideQuestProgressCommand(1, 3));
            bus.Send(new ClaimSideQuestRewardCommand(1));
            bus.Send(new BumpSideQuestProgressCommand(2, 3));
            bus.Send(new ClaimSideQuestRewardCommand(2));
            Assert.AreEqual(25, wallet.Gold);
        }

        [Test]
        public void Daily_CannotClaimTwice()
        {
            var bus = CreateBus(out _, out _);
            bus.Send(new BumpDailyQuestProgressCommand(1, 1));
            bus.Send(new ClaimDailyQuestRewardCommand(1));
            Assert.Throws<InvalidOperationException>(() => bus.Send(new ClaimDailyQuestRewardCommand(1)));
        }
    }
}
```

- [ ] **Step 2: Run Unity EditMode tests** targeting assembly `GameScript.EditModeTests` (Unity Test Runner UI or CLI per `AGENTS.md` naming under `UnityProject/TestResults/`).

- [ ] **Step 3: Commit**

```bash
git add UnityProject/Assets/GameScript/Tests/EditMode/GameScript.EditModeTests.asmdef \
        UnityProject/Assets/GameScript/Tests/EditMode/Quest/QuestSessionStateTests.cs
git commit -m "test(quest): add editmode coverage for quest rules"
```

---

### Task 10: Contract tests + FairyGUI prefab

**Files:**
- Modify: `UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/GameScriptSampleContractTests.cs`
- Assets: publish `ui/quest_panel.prefab` via FairyGUI + YooAsset pipeline

- [ ] **Step 1: Duplicate inventory contract tests for `GameScript.UI.Quest.QuestWindowPresenter` and `IOpenQuestPanelUseCase`.**

- [ ] **Step 2: Author FairyGUI package** with controller `tabs` (3 pages), lists `listSide` / `listDaily`, labels `txtWallet`, `txtMainTitle`, `txtMainProgress`, list item children `title`, `progress`, `btnClaim`. Export prefab to path used in resolver.

- [ ] **Step 3: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Tests/EditMode/UI/GameScriptSampleContractTests.cs
git add <fairygui-exported-assets>
git commit -m "feat(quest): contract tests and quest_panel prefab"
```

---

## Plan self-review (checklist)

1. **Spec coverage:** §2 goals → Tasks 2–10; §3 memory + wallet → Tasks 2–4; §4 CQRS → Tasks 3–6; §5 UI → Tasks 5,7,10; §6 dev entry → Task 8; §7 tests → Task 9–10. **Gap closed:** composition split corrected in Task 8 narrative (Runtime interface + GameScript factory).
2. **Placeholder scan:** None intentional; Task 8 notes scene wiring responsibility explicitly.
3. **Type consistency:** `WindowIds.Quest.Value` must match resolver dictionary key and YooAsset address; `QuestGamePresenterHost` compares `request.Id == WindowIds.Quest` (`WindowId` supports `==`).

---

## Execution handoff

**Plan complete and saved to** `docs/superpowers/plans/2026-05-09-quest-system-implementation-plan.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — dispatch a fresh subagent per task, review between tasks, fast iteration (**use** `superpowers:subagent-driven-development`, in a **git worktree** under `.worktrees/` per `AGENTS.md`).

2. **Inline Execution** — run tasks sequentially in one session with checkpoints (**use** `superpowers:executing-plans`, also prefer worktree).

**Which approach do you want?**
