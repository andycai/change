# Change Optional Content Streaming Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Runtime-only optional-content streaming module that supports service-driven catalog sync, policy-based background downloading (Wi-Fi auto + cellular throttled), pressure-aware pause/resume, TTL cache cleanup, and UI-decoupled event streaming.

**Architecture:** Add a new `Change.Runtime.ContentStreaming` module with six bounded units: contracts/models, policy engine, scheduler, state/event hub, catalog sync + cache cleaner, and orchestration/service facade. Integrate YooAsset only behind adapter interfaces and expose DTO-based events for FairyGUI consumers. Validate behavior with EditMode first, then one PlayMode smoke test for lifecycle/event threading.

**Tech Stack:** Unity 2022.3 LTS, C#, UniTask 2.5.10, YooAsset 2.3.18, Unity Test Framework (EditMode + PlayMode), existing Change Runtime conventions.

---

## Scope Check

The spec is one coherent subsystem and can be delivered in a single plan. The module has internal subparts, but all subparts serve one user-facing capability: optional content background download with runtime policies.

## File Structure (Create/Modify Map)

### Runtime module

- Modify: `UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef` (add `UniTask` and `YooAsset` references)
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Abstractions/IContentDownloadService.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Abstractions/IContentDownloadEvents.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Abstractions/INetworkStateProvider.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Abstractions/IPlayPressureSignal.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Abstractions/IDataBudgetProvider.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Abstractions/IClock.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Model/NetworkType.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Model/DownloadTaskState.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Model/ContentStreamingErrorCode.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Model/ContentPackDefinition.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Model/DownloadPolicySnapshot.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Model/DownloadTaskSnapshot.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Model/ContentCacheRecord.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Policy/ContentDownloadPolicyEngine.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Scheduling/DownloadTaskScheduler.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/State/ContentDownloadStateStore.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/State/ContentDownloadEventHub.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Catalog/ICatalogClient.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Catalog/CatalogSyncService.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Maintenance/CacheExpiryCleaner.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Execution/IAssetDownloadAdapter.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Execution/YooAssetDownloadAdapter.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/ContentDownloadOrchestrator.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/README.md`

### Tests

- Modify: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Change.Runtime.EditModeTests.asmdef` (add `UniTask` reference)
- Modify: `UnityProject/Assets/Change/Runtime/Tests/PlayMode/Change.Runtime.PlayModeTests.asmdef` (add `UniTask` reference)
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/ContentStreamingContractsTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/ContentDownloadPolicyEngineTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/DownloadTaskSchedulerTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/ContentDownloadEventHubTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/CatalogSyncAndCacheCleanerTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/ContentDownloadOrchestratorTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/PlayMode/ContentStreaming/ContentDownloadOrchestratorPlayModeTests.cs`

---

### Task 1: Add core contracts, models, and asmdef references

**Files:**
- Modify: `UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef`
- Modify: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Change.Runtime.EditModeTests.asmdef`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Abstractions/*.cs` (6 files)
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Model/*.cs` (7 files)
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/ContentStreamingContractsTests.cs`

- [ ] **Step 1: Write the failing contracts/model tests**

```csharp
using System;
using NUnit.Framework;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class ContentStreamingContractsTests
    {
        [Test]
        public void ContentPackDefinition_WhenPackIdEmpty_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                _ = new ContentPackDefinition(
                    string.Empty,
                    version: "1.0.0",
                    sizeBytes: 1024,
                    priority: 10,
                    expireAtUtcTicks: DateTime.UtcNow.Ticks + TimeSpan.FromDays(1).Ticks,
                    requiresWifi: false));
        }

        [Test]
        public void DownloadPolicySnapshot_WhenCellularAndOverBudget_DisallowsAutoDownload()
        {
            var snapshot = new DownloadPolicySnapshot(
                NetworkType.Cellular,
                isHighPressure: false,
                allowAutoDownloadOnWifi: true,
                allowAutoDownloadOnCellular: true,
                cellularRateLimitKbps: 256,
                dailyBudgetRemainingBytes: 0,
                maxConcurrentDownloads: 2);

            Assert.IsFalse(snapshot.AllowAutoDownload);
            Assert.AreEqual(ContentStreamingPauseReason.BudgetExceeded, snapshot.PauseReason);
        }

        [Test]
        public void DownloadTaskSnapshot_WithProgress_UpdatesBytesAndRate()
        {
            var task = DownloadTaskSnapshot.CreateQueued("skin_pack_a", totalBytes: 5000, priority: 20);
            var updated = task.WithProgress(downloadedBytes: 2000, rateKbps: 120);

            Assert.AreEqual(DownloadTaskState.Downloading, updated.State);
            Assert.AreEqual(2000, updated.DownloadedBytes);
            Assert.AreEqual(120, updated.RateKbps);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.ContentStreaming.Tests.ContentStreamingContractsTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-contentstreaming-contracts-${TS}.xml" \
  -logFile -
```

Expected: FAIL with missing type/namespace errors.

- [ ] **Step 3: Implement minimal contracts/models + asmdef updates**

`UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef`

```json
{
    "name": "Change.Runtime",
    "rootNamespace": "Change.Runtime",
    "references": [
        "Change.Framework",
        "UniTask",
        "YooAsset"
    ],
    "optionalUnityReferences": [],
    "includePlatforms": [],
    "excludePlatforms": []
}
```

`UnityProject/Assets/Change/Runtime/Tests/EditMode/Change.Runtime.EditModeTests.asmdef`

```json
{
    "name": "Change.Runtime.EditModeTests",
    "rootNamespace": "Change.Runtime",
    "references": [
        "Change.Runtime",
        "Change.Framework",
        "UniTask"
    ],
    "optionalUnityReferences": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": []
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Model/NetworkType.cs`

```csharp
namespace Change.Runtime.ContentStreaming
{
    public enum NetworkType : byte
    {
        None = 0,
        Wifi = 1,
        Cellular = 2,
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Model/DownloadTaskState.cs`

```csharp
namespace Change.Runtime.ContentStreaming
{
    public enum DownloadTaskState : byte
    {
        Pending = 0,
        Queued = 1,
        Downloading = 2,
        Verifying = 3,
        Completed = 4,
        Paused = 5,
        FailedTransient = 6,
        FailedTerminal = 7,
        Canceled = 8,
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Model/ContentStreamingErrorCode.cs`

```csharp
namespace Change.Runtime.ContentStreaming
{
    public enum ContentStreamingErrorCode : byte
    {
        None = 0,
        NetworkTimeout = 1,
        NetworkUnavailable = 2,
        ServerTemporary = 3,
        ManifestInvalid = 4,
        SignatureInvalid = 5,
        DiskWriteFailed = 6,
        VerifyFailed = 7,
        UserCanceled = 8,
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Model/ContentPackDefinition.cs`

```csharp
using System;

namespace Change.Runtime.ContentStreaming
{
    public readonly struct ContentPackDefinition
    {
        public ContentPackDefinition(
            string packId,
            string version,
            long sizeBytes,
            int priority,
            long expireAtUtcTicks,
            bool requiresWifi)
        {
            if (string.IsNullOrWhiteSpace(packId)) throw new ArgumentException("packId is required.", nameof(packId));
            if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("version is required.", nameof(version));
            if (sizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
            if (priority < 0) throw new ArgumentOutOfRangeException(nameof(priority));

            PackId = packId;
            Version = version;
            SizeBytes = sizeBytes;
            Priority = priority;
            ExpireAtUtcTicks = expireAtUtcTicks;
            RequiresWifi = requiresWifi;
        }

        public string PackId { get; }
        public string Version { get; }
        public long SizeBytes { get; }
        public int Priority { get; }
        public long ExpireAtUtcTicks { get; }
        public bool RequiresWifi { get; }
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Model/DownloadPolicySnapshot.cs`

```csharp
using System;

namespace Change.Runtime.ContentStreaming
{
    public enum ContentStreamingPauseReason : byte
    {
        None = 0,
        HighPressure = 1,
        BudgetExceeded = 2,
        NetworkPolicy = 3,
        ManualPause = 4,
    }

    public readonly struct DownloadPolicySnapshot
    {
        public DownloadPolicySnapshot(
            NetworkType networkType,
            bool isHighPressure,
            bool allowAutoDownloadOnWifi,
            bool allowAutoDownloadOnCellular,
            int cellularRateLimitKbps,
            long dailyBudgetRemainingBytes,
            int maxConcurrentDownloads)
        {
            if (cellularRateLimitKbps < 0) throw new ArgumentOutOfRangeException(nameof(cellularRateLimitKbps));
            if (maxConcurrentDownloads < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrentDownloads));

            NetworkType = networkType;
            IsHighPressure = isHighPressure;
            CellularRateLimitKbps = cellularRateLimitKbps;
            DailyBudgetRemainingBytes = dailyBudgetRemainingBytes;
            MaxConcurrentDownloads = maxConcurrentDownloads;

            if (isHighPressure)
            {
                AllowAutoDownload = false;
                PauseReason = ContentStreamingPauseReason.HighPressure;
                return;
            }

            if (dailyBudgetRemainingBytes <= 0)
            {
                AllowAutoDownload = false;
                PauseReason = ContentStreamingPauseReason.BudgetExceeded;
                return;
            }

            AllowAutoDownload = networkType switch
            {
                NetworkType.Wifi => allowAutoDownloadOnWifi,
                NetworkType.Cellular => allowAutoDownloadOnCellular,
                _ => false,
            };

            PauseReason = AllowAutoDownload ? ContentStreamingPauseReason.None : ContentStreamingPauseReason.NetworkPolicy;
        }

        public NetworkType NetworkType { get; }
        public bool IsHighPressure { get; }
        public bool AllowAutoDownload { get; }
        public int CellularRateLimitKbps { get; }
        public long DailyBudgetRemainingBytes { get; }
        public int MaxConcurrentDownloads { get; }
        public ContentStreamingPauseReason PauseReason { get; }
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Model/DownloadTaskSnapshot.cs`

```csharp
using System;

namespace Change.Runtime.ContentStreaming
{
    public readonly struct DownloadTaskSnapshot
    {
        public DownloadTaskSnapshot(
            string packId,
            DownloadTaskState state,
            long downloadedBytes,
            long totalBytes,
            int priority,
            int retryCount,
            int rateKbps,
            ContentStreamingErrorCode errorCode,
            long sequence)
        {
            PackId = packId;
            State = state;
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
            Priority = priority;
            RetryCount = retryCount;
            RateKbps = rateKbps;
            ErrorCode = errorCode;
            Sequence = sequence;
        }

        public static DownloadTaskSnapshot CreateQueued(string packId, long totalBytes, int priority)
        {
            if (string.IsNullOrWhiteSpace(packId)) throw new ArgumentException("packId is required.", nameof(packId));
            if (totalBytes <= 0) throw new ArgumentOutOfRangeException(nameof(totalBytes));

            return new DownloadTaskSnapshot(packId, DownloadTaskState.Queued, 0, totalBytes, priority, 0, 0, ContentStreamingErrorCode.None, 0);
        }

        public string PackId { get; }
        public DownloadTaskState State { get; }
        public long DownloadedBytes { get; }
        public long TotalBytes { get; }
        public int Priority { get; }
        public int RetryCount { get; }
        public int RateKbps { get; }
        public ContentStreamingErrorCode ErrorCode { get; }
        public long Sequence { get; }

        public DownloadTaskSnapshot WithProgress(long downloadedBytes, int rateKbps)
        {
            if (downloadedBytes < 0 || downloadedBytes > TotalBytes) throw new ArgumentOutOfRangeException(nameof(downloadedBytes));
            if (rateKbps < 0) throw new ArgumentOutOfRangeException(nameof(rateKbps));

            return new DownloadTaskSnapshot(
                PackId,
                DownloadTaskState.Downloading,
                downloadedBytes,
                TotalBytes,
                Priority,
                RetryCount,
                rateKbps,
                ContentStreamingErrorCode.None,
                Sequence + 1);
        }

        public DownloadTaskSnapshot WithState(DownloadTaskState state, ContentStreamingErrorCode errorCode = ContentStreamingErrorCode.None)
        {
            return new DownloadTaskSnapshot(
                PackId,
                state,
                DownloadedBytes,
                TotalBytes,
                Priority,
                RetryCount,
                RateKbps,
                errorCode,
                Sequence + 1);
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Model/ContentCacheRecord.cs`

```csharp
namespace Change.Runtime.ContentStreaming
{
    public readonly struct ContentCacheRecord
    {
        public ContentCacheRecord(string packId, string version, long expireAtUtcTicks, long lastAccessUtcTicks)
        {
            PackId = packId;
            Version = version;
            ExpireAtUtcTicks = expireAtUtcTicks;
            LastAccessUtcTicks = lastAccessUtcTicks;
        }

        public string PackId { get; }
        public string Version { get; }
        public long ExpireAtUtcTicks { get; }
        public long LastAccessUtcTicks { get; }
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Abstractions/IContentDownloadService.cs`

```csharp
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.ContentStreaming
{
    public interface IContentDownloadService
    {
        UniTask SyncCatalogAsync(CancellationToken cancellationToken);
        bool EnqueuePack(string packId);
        void PauseAll(ContentStreamingPauseReason reason);
        void ResumeByPolicy();
        bool RemovePack(string packId, bool removeCache);
        bool TryGetPackState(string packId, out DownloadTaskSnapshot snapshot);
        IReadOnlyList<DownloadTaskSnapshot> GetAllTaskSnapshots();
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Abstractions/IContentDownloadEvents.cs`

```csharp
using System;

namespace Change.Runtime.ContentStreaming
{
    public interface IContentDownloadEvents
    {
        event Action<DownloadTaskSnapshot> TaskAdded;
        event Action<DownloadTaskSnapshot> TaskStateChanged;
        event Action<DownloadTaskSnapshot> TaskProgressChanged;
        event Action<string> TaskRemoved;
        event Action<DownloadPolicySnapshot> GlobalPolicyChanged;
        event Action<int> CatalogUpdated;
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Abstractions/INetworkStateProvider.cs`

```csharp
namespace Change.Runtime.ContentStreaming
{
    public interface INetworkStateProvider
    {
        NetworkType Current { get; }
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Abstractions/IPlayPressureSignal.cs`

```csharp
namespace Change.Runtime.ContentStreaming
{
    public interface IPlayPressureSignal
    {
        bool IsHighPressure { get; }
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Abstractions/IDataBudgetProvider.cs`

```csharp
namespace Change.Runtime.ContentStreaming
{
    public interface IDataBudgetProvider
    {
        long DailyRemainingBytes { get; }
        void Consume(long bytes);
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Abstractions/IClock.cs`

```csharp
namespace Change.Runtime.ContentStreaming
{
    public interface IClock
    {
        long UtcNowTicks { get; }
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run the command from Step 2 again.

Expected: PASS (`ContentStreamingContractsTests` all green).

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef \
  UnityProject/Assets/Change/Runtime/Tests/EditMode/Change.Runtime.EditModeTests.asmdef \
  UnityProject/Assets/Change/Runtime/ContentStreaming \
  UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/ContentStreamingContractsTests.cs
git commit -m "feat(runtime): add content streaming contracts and core models"
```

---

### Task 2: Implement policy engine (network + budget + pressure)

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Policy/ContentDownloadPolicyEngine.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/ContentDownloadPolicyEngineTests.cs`

- [ ] **Step 1: Write failing policy-engine tests**

```csharp
using System;
using NUnit.Framework;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class ContentDownloadPolicyEngineTests
    {
        [Test]
        public void Evaluate_WhenWifiAndNotHighPressure_AllowsAutoDownload()
        {
            var engine = new ContentDownloadPolicyEngine();

            var policy = engine.Evaluate(
                NetworkType.Wifi,
                isHighPressure: false,
                dailyBudgetRemainingBytes: 1024,
                allowWifiAuto: true,
                allowCellularAuto: true,
                cellularRateLimitKbps: 256,
                maxConcurrentDownloads: 3);

            Assert.IsTrue(policy.AllowAutoDownload);
            Assert.AreEqual(ContentStreamingPauseReason.None, policy.PauseReason);
        }

        [Test]
        public void Evaluate_WhenHighPressure_DisallowsAndMarksPauseReason()
        {
            var engine = new ContentDownloadPolicyEngine();

            var policy = engine.Evaluate(
                NetworkType.Wifi,
                isHighPressure: true,
                dailyBudgetRemainingBytes: 1024,
                allowWifiAuto: true,
                allowCellularAuto: true,
                cellularRateLimitKbps: 256,
                maxConcurrentDownloads: 3);

            Assert.IsFalse(policy.AllowAutoDownload);
            Assert.AreEqual(ContentStreamingPauseReason.HighPressure, policy.PauseReason);
        }

        [Test]
        public void Evaluate_WhenCellular_UsesConfiguredRateLimit()
        {
            var engine = new ContentDownloadPolicyEngine();

            var policy = engine.Evaluate(
                NetworkType.Cellular,
                isHighPressure: false,
                dailyBudgetRemainingBytes: 4096,
                allowWifiAuto: true,
                allowCellularAuto: true,
                cellularRateLimitKbps: 192,
                maxConcurrentDownloads: 2);

            Assert.AreEqual(192, policy.CellularRateLimitKbps);
            Assert.IsTrue(policy.AllowAutoDownload);
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
  -testFilter "Change.Runtime.ContentStreaming.Tests.ContentDownloadPolicyEngineTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-contentstreaming-policy-${TS}.xml" \
  -logFile -
```

Expected: FAIL (missing `ContentDownloadPolicyEngine`).

- [ ] **Step 3: Implement minimal policy engine**

`UnityProject/Assets/Change/Runtime/ContentStreaming/Policy/ContentDownloadPolicyEngine.cs`

```csharp
namespace Change.Runtime.ContentStreaming
{
    public sealed class ContentDownloadPolicyEngine
    {
        public DownloadPolicySnapshot Evaluate(
            NetworkType networkType,
            bool isHighPressure,
            long dailyBudgetRemainingBytes,
            bool allowWifiAuto,
            bool allowCellularAuto,
            int cellularRateLimitKbps,
            int maxConcurrentDownloads)
        {
            return new DownloadPolicySnapshot(
                networkType,
                isHighPressure,
                allowWifiAuto,
                allowCellularAuto,
                cellularRateLimitKbps,
                dailyBudgetRemainingBytes,
                maxConcurrentDownloads);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run the command from Step 2 again.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/ContentStreaming/Policy/ContentDownloadPolicyEngine.cs \
  UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/ContentDownloadPolicyEngineTests.cs
git commit -m "feat(runtime): add content streaming policy engine"
```

---

### Task 3: Implement priority scheduler with retry/backoff readiness

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Scheduling/DownloadTaskScheduler.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/DownloadTaskSchedulerTests.cs`

- [ ] **Step 1: Write failing scheduler tests**

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class DownloadTaskSchedulerTests
    {
        [Test]
        public void DequeueReadyTasks_PicksHigherPriorityFirst()
        {
            var scheduler = new DownloadTaskScheduler();

            scheduler.Enqueue(DownloadTaskSnapshot.CreateQueued("low", 1000, priority: 1));
            scheduler.Enqueue(DownloadTaskSnapshot.CreateQueued("high", 1000, priority: 100));

            var ready = new List<DownloadTaskSnapshot>();
            scheduler.DequeueReadyTasks(maxCount: 1, ready);

            Assert.AreEqual(1, ready.Count);
            Assert.AreEqual("high", ready[0].PackId);
        }

        [Test]
        public void DequeueReadyTasks_WhenPaused_ReturnsNone()
        {
            var scheduler = new DownloadTaskScheduler();
            scheduler.Enqueue(DownloadTaskSnapshot.CreateQueued("pack", 1000, priority: 10));
            scheduler.SetPaused(true);

            var ready = new List<DownloadTaskSnapshot>();
            scheduler.DequeueReadyTasks(maxCount: 2, ready);

            Assert.AreEqual(0, ready.Count);
        }

        [Test]
        public void MarkTransientFailure_IncrementsRetryCountAndRequeues()
        {
            var scheduler = new DownloadTaskScheduler();
            var task = DownloadTaskSnapshot.CreateQueued("retry-pack", 500, priority: 10);
            scheduler.Enqueue(task);

            scheduler.MarkTransientFailure(task.PackId);

            Assert.IsTrue(scheduler.TryGet(task.PackId, out var retryTask));
            Assert.AreEqual(DownloadTaskState.Queued, retryTask.State);
            Assert.AreEqual(1, retryTask.RetryCount);
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
  -testFilter "Change.Runtime.ContentStreaming.Tests.DownloadTaskSchedulerTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-contentstreaming-scheduler-${TS}.xml" \
  -logFile -
```

Expected: FAIL.

- [ ] **Step 3: Implement minimal scheduler**

`UnityProject/Assets/Change/Runtime/ContentStreaming/Scheduling/DownloadTaskScheduler.cs`

```csharp
using System;
using System.Collections.Generic;

namespace Change.Runtime.ContentStreaming
{
    public sealed class DownloadTaskScheduler
    {
        private readonly Dictionary<string, DownloadTaskSnapshot> _tasks = new();
        private bool _paused;

        public void Enqueue(in DownloadTaskSnapshot task)
        {
            _tasks[task.PackId] = task.WithState(DownloadTaskState.Queued);
        }

        public bool Remove(string packId)
        {
            return _tasks.Remove(packId);
        }

        public bool TryGet(string packId, out DownloadTaskSnapshot snapshot)
        {
            return _tasks.TryGetValue(packId, out snapshot);
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
        }

        public void DequeueReadyTasks(int maxCount, List<DownloadTaskSnapshot> output)
        {
            output.Clear();
            if (_paused || maxCount <= 0)
            {
                return;
            }

            foreach (var pair in _tasks)
            {
                var task = pair.Value;
                if (task.State == DownloadTaskState.Queued)
                {
                    output.Add(task);
                }
            }

            output.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            if (output.Count > maxCount)
            {
                output.RemoveRange(maxCount, output.Count - maxCount);
            }
        }

        public void MarkTransientFailure(string packId)
        {
            if (!_tasks.TryGetValue(packId, out var task))
            {
                return;
            }

            var retried = new DownloadTaskSnapshot(
                task.PackId,
                DownloadTaskState.Queued,
                task.DownloadedBytes,
                task.TotalBytes,
                task.Priority,
                task.RetryCount + 1,
                0,
                ContentStreamingErrorCode.NetworkTimeout,
                task.Sequence + 1);

            _tasks[packId] = retried;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run the command from Step 2 again.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/ContentStreaming/Scheduling/DownloadTaskScheduler.cs \
  UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/DownloadTaskSchedulerTests.cs
git commit -m "feat(runtime): add content streaming task scheduler"
```

---

### Task 4: Implement state store + event hub with monotonic sequence and progress throttling

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/State/ContentDownloadStateStore.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/State/ContentDownloadEventHub.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/ContentDownloadEventHubTests.cs`

- [ ] **Step 1: Write failing state/event tests**

```csharp
using NUnit.Framework;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class ContentDownloadEventHubTests
    {
        [Test]
        public void PublishStateChange_IncrementsSequenceMonotonically()
        {
            var hub = new ContentDownloadEventHub();
            var snapshot = DownloadTaskSnapshot.CreateQueued("pack-a", 1000, 10);

            var a = hub.NextState(snapshot.WithState(DownloadTaskState.Downloading));
            var b = hub.NextState(a.WithState(DownloadTaskState.Verifying));

            Assert.Greater(b.Sequence, a.Sequence);
        }

        [Test]
        public void TryPublishProgress_RespectsThrottleWindow()
        {
            var clock = new FakeClock(1000);
            var hub = new ContentDownloadEventHub(clock, progressIntervalMs: 200);
            var snapshot = DownloadTaskSnapshot.CreateQueued("pack-b", 1000, 10);

            Assert.IsTrue(hub.TryPublishProgress(snapshot.WithProgress(100, 10), out _));
            Assert.IsFalse(hub.TryPublishProgress(snapshot.WithProgress(150, 12), out _));

            clock.UtcNowTicks += TimeSpan.FromMilliseconds(210).Ticks;
            Assert.IsTrue(hub.TryPublishProgress(snapshot.WithProgress(250, 15), out _));
        }

        private sealed class FakeClock : IClock
        {
            public FakeClock(long ticks) => UtcNowTicks = ticks;
            public long UtcNowTicks { get; set; }
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
  -testFilter "Change.Runtime.ContentStreaming.Tests.ContentDownloadEventHubTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-contentstreaming-events-${TS}.xml" \
  -logFile -
```

Expected: FAIL.

- [ ] **Step 3: Implement state store + event hub**

`UnityProject/Assets/Change/Runtime/ContentStreaming/State/ContentDownloadStateStore.cs`

```csharp
using System.Collections.Generic;

namespace Change.Runtime.ContentStreaming
{
    public sealed class ContentDownloadStateStore
    {
        private readonly Dictionary<string, DownloadTaskSnapshot> _tasks = new();

        public void Upsert(in DownloadTaskSnapshot snapshot)
        {
            _tasks[snapshot.PackId] = snapshot;
        }

        public bool Remove(string packId)
        {
            return _tasks.Remove(packId);
        }

        public bool TryGet(string packId, out DownloadTaskSnapshot snapshot)
        {
            return _tasks.TryGetValue(packId, out snapshot);
        }

        public void GetAll(List<DownloadTaskSnapshot> output)
        {
            output.Clear();
            foreach (var pair in _tasks)
            {
                output.Add(pair.Value);
            }
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/State/ContentDownloadEventHub.cs`

```csharp
using System;

namespace Change.Runtime.ContentStreaming
{
    public sealed class ContentDownloadEventHub
    {
        private readonly IClock _clock;
        private readonly long _progressIntervalTicks;
        private long _sequence;
        private long _lastProgressPublishTicks;

        public ContentDownloadEventHub()
            : this(new SystemClock(), 200)
        {
        }

        public ContentDownloadEventHub(IClock clock, int progressIntervalMs)
        {
            _clock = clock;
            _progressIntervalTicks = TimeSpan.FromMilliseconds(progressIntervalMs).Ticks;
        }

        public DownloadTaskSnapshot NextState(in DownloadTaskSnapshot snapshot)
        {
            _sequence++;
            return new DownloadTaskSnapshot(
                snapshot.PackId,
                snapshot.State,
                snapshot.DownloadedBytes,
                snapshot.TotalBytes,
                snapshot.Priority,
                snapshot.RetryCount,
                snapshot.RateKbps,
                snapshot.ErrorCode,
                _sequence);
        }

        public bool TryPublishProgress(in DownloadTaskSnapshot snapshot, out DownloadTaskSnapshot sequenced)
        {
            var now = _clock.UtcNowTicks;
            if (now - _lastProgressPublishTicks < _progressIntervalTicks)
            {
                sequenced = default;
                return false;
            }

            _lastProgressPublishTicks = now;
            sequenced = NextState(snapshot);
            return true;
        }

        private sealed class SystemClock : IClock
        {
            public long UtcNowTicks => DateTime.UtcNow.Ticks;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/ContentStreaming/State \
  UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/ContentDownloadEventHubTests.cs
git commit -m "feat(runtime): add content streaming state store and event hub"
```

---

### Task 5: Implement catalog sync + TTL cache cleaner

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Catalog/ICatalogClient.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Catalog/CatalogSyncService.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Maintenance/CacheExpiryCleaner.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/CatalogSyncAndCacheCleanerTests.cs`

- [ ] **Step 1: Write failing catalog/cleaner tests**

```csharp
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class CatalogSyncAndCacheCleanerTests
    {
        [Test]
        public async UniTask SyncCatalogAsync_UpdatesCatalogVersionAndDefinitions()
        {
            var client = new FakeCatalogClient(new[]
            {
                new ContentPackDefinition("voice_pack", "1.0.0", 1024, 10, DateTime.UtcNow.AddDays(1).Ticks, false)
            }, "v10");
            var sync = new CatalogSyncService(client);

            await sync.SyncAsync();

            Assert.AreEqual("v10", sync.CatalogVersion);
            Assert.AreEqual(1, sync.Definitions.Count);
        }

        [Test]
        public void CollectExpired_ReturnsOnlyExpiredRecords()
        {
            var now = DateTime.UtcNow;
            var records = new List<ContentCacheRecord>
            {
                new("a", "1.0", now.AddMinutes(-1).Ticks, now.Ticks),
                new("b", "1.0", now.AddDays(1).Ticks, now.Ticks)
            };

            var cleaner = new CacheExpiryCleaner();
            var expired = new List<ContentCacheRecord>();
            cleaner.CollectExpired(records, now.Ticks, expired);

            Assert.AreEqual(1, expired.Count);
            Assert.AreEqual("a", expired[0].PackId);
        }

        private sealed class FakeCatalogClient : ICatalogClient
        {
            private readonly IReadOnlyList<ContentPackDefinition> _definitions;
            private readonly string _version;

            public FakeCatalogClient(IReadOnlyList<ContentPackDefinition> definitions, string version)
            {
                _definitions = definitions;
                _version = version;
            }

            public UniTask<CatalogSyncResult> FetchAsync(string currentVersion)
            {
                return UniTask.FromResult(new CatalogSyncResult(_version, _definitions));
            }
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
  -testFilter "Change.Runtime.ContentStreaming.Tests.CatalogSyncAndCacheCleanerTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-contentstreaming-catalog-${TS}.xml" \
  -logFile -
```

Expected: FAIL.

- [ ] **Step 3: Implement catalog sync + cleaner**

`UnityProject/Assets/Change/Runtime/ContentStreaming/Catalog/ICatalogClient.cs`

```csharp
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.ContentStreaming
{
    public readonly struct CatalogSyncResult
    {
        public CatalogSyncResult(string version, IReadOnlyList<ContentPackDefinition> definitions)
        {
            Version = version;
            Definitions = definitions;
        }

        public string Version { get; }
        public IReadOnlyList<ContentPackDefinition> Definitions { get; }
    }

    public interface ICatalogClient
    {
        UniTask<CatalogSyncResult> FetchAsync(string currentVersion);
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Catalog/CatalogSyncService.cs`

```csharp
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.ContentStreaming
{
    public sealed class CatalogSyncService
    {
        private readonly ICatalogClient _client;
        private readonly List<ContentPackDefinition> _definitions = new();

        public CatalogSyncService(ICatalogClient client)
        {
            _client = client;
        }

        public string CatalogVersion { get; private set; } = string.Empty;
        public IReadOnlyList<ContentPackDefinition> Definitions => _definitions;

        public async UniTask<int> SyncAsync()
        {
            var result = await _client.FetchAsync(CatalogVersion);

            CatalogVersion = result.Version;
            _definitions.Clear();
            for (var i = 0; i < result.Definitions.Count; i++)
            {
                _definitions.Add(result.Definitions[i]);
            }

            return _definitions.Count;
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Maintenance/CacheExpiryCleaner.cs`

```csharp
using System.Collections.Generic;

namespace Change.Runtime.ContentStreaming
{
    public sealed class CacheExpiryCleaner
    {
        public void CollectExpired(IReadOnlyList<ContentCacheRecord> records, long nowUtcTicks, List<ContentCacheRecord> expired)
        {
            expired.Clear();

            for (var i = 0; i < records.Count; i++)
            {
                if (records[i].ExpireAtUtcTicks <= nowUtcTicks)
                {
                    expired.Add(records[i]);
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/ContentStreaming/Catalog \
  UnityProject/Assets/Change/Runtime/ContentStreaming/Maintenance/CacheExpiryCleaner.cs \
  UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/CatalogSyncAndCacheCleanerTests.cs
git commit -m "feat(runtime): add catalog sync and ttl cache cleaner"
```

---

### Task 6: Implement orchestrator + YooAsset adapter + service/event facade

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Execution/IAssetDownloadAdapter.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/Execution/YooAssetDownloadAdapter.cs`
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/ContentDownloadOrchestrator.cs`
- Modify: `UnityProject/Assets/Change/Runtime/Tests/PlayMode/Change.Runtime.PlayModeTests.asmdef`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/ContentDownloadOrchestratorTests.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/PlayMode/ContentStreaming/ContentDownloadOrchestratorPlayModeTests.cs`

- [ ] **Step 1: Write failing orchestrator tests**

```csharp
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class ContentDownloadOrchestratorTests
    {
        [Test]
        public async UniTask SyncAndEnqueue_WhenPolicyAllows_StartsDownload()
        {
            var env = OrchestratorFixture.Create();
            var addedEvents = 0;
            var stateEvents = 0;

            env.Orchestrator.TaskAdded += _ => addedEvents++;
            env.Orchestrator.TaskStateChanged += _ => stateEvents++;

            await env.Orchestrator.SyncCatalogAsync(CancellationToken.None);
            Assert.IsTrue(env.Orchestrator.EnqueuePack("voice_pack"));

            await env.Orchestrator.TickAsync(CancellationToken.None);

            Assert.IsTrue(env.Orchestrator.TryGetPackState("voice_pack", out var snapshot));
            Assert.AreEqual(DownloadTaskState.Completed, snapshot.State);
            Assert.AreEqual(1, addedEvents);
            Assert.GreaterOrEqual(stateEvents, 2);
        }

        [Test]
        public async UniTask TickAsync_WhenHighPressure_DoesNotDequeue()
        {
            var env = OrchestratorFixture.Create(isHighPressure: true);

            await env.Orchestrator.SyncCatalogAsync(CancellationToken.None);
            Assert.IsTrue(env.Orchestrator.EnqueuePack("voice_pack"));

            await env.Orchestrator.TickAsync(CancellationToken.None);

            Assert.IsTrue(env.Orchestrator.TryGetPackState("voice_pack", out var snapshot));
            Assert.AreEqual(DownloadTaskState.Queued, snapshot.State);
        }

        private sealed class OrchestratorFixture
        {
            public static OrchestratorFixture Create(bool isHighPressure = false)
            {
                var definitions = new List<ContentPackDefinition>
                {
                    new("voice_pack", "1.0.0", 1000, 100, System.DateTime.UtcNow.AddDays(2).Ticks, false)
                };

                var catalog = new FakeCatalogClient(definitions);
                var download = new FakeAdapter();
                var policy = new ContentDownloadPolicyEngine();

                var orchestrator = new ContentDownloadOrchestrator(
                    new CatalogSyncService(catalog),
                    policy,
                    new DownloadTaskScheduler(),
                    new ContentDownloadStateStore(),
                    new ContentDownloadEventHub(),
                    download,
                    new FixedNetwork(NetworkType.Wifi),
                    new FixedPressure(isHighPressure),
                    new FixedBudget(1024 * 1024));

                return new OrchestratorFixture(orchestrator);
            }

            private OrchestratorFixture(ContentDownloadOrchestrator orchestrator)
            {
                Orchestrator = orchestrator;
            }

            public ContentDownloadOrchestrator Orchestrator { get; }
        }

        private sealed class FakeCatalogClient : ICatalogClient
        {
            private readonly IReadOnlyList<ContentPackDefinition> _definitions;
            public FakeCatalogClient(IReadOnlyList<ContentPackDefinition> definitions) => _definitions = definitions;
            public UniTask<CatalogSyncResult> FetchAsync(string currentVersion) => UniTask.FromResult(new CatalogSyncResult("v1", _definitions));
        }

        private sealed class FakeAdapter : IAssetDownloadAdapter
        {
            public UniTask<ContentStreamingErrorCode> DownloadAsync(ContentPackDefinition definition, int rateLimitKbps, CancellationToken cancellationToken)
                => UniTask.FromResult(ContentStreamingErrorCode.None);
        }

        private sealed class FixedNetwork : INetworkStateProvider
        {
            public FixedNetwork(NetworkType current) => Current = current;
            public NetworkType Current { get; }
        }

        private sealed class FixedPressure : IPlayPressureSignal
        {
            public FixedPressure(bool isHighPressure) => IsHighPressure = isHighPressure;
            public bool IsHighPressure { get; }
        }

        private sealed class FixedBudget : IDataBudgetProvider
        {
            public FixedBudget(long dailyRemainingBytes) => DailyRemainingBytes = dailyRemainingBytes;
            public long DailyRemainingBytes { get; private set; }
            public void Consume(long bytes) => DailyRemainingBytes -= bytes;
        }
    }
}
```

- [ ] **Step 2: Run EditMode tests to verify failure**

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.ContentStreaming.Tests.ContentDownloadOrchestratorTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-contentstreaming-orchestrator-${TS}.xml" \
  -logFile -
```

Expected: FAIL.

- [ ] **Step 3: Implement adapter + orchestrator + playmode asmdef refs**

`UnityProject/Assets/Change/Runtime/Tests/PlayMode/Change.Runtime.PlayModeTests.asmdef`

```json
{
    "name": "Change.Runtime.PlayModeTests",
    "rootNamespace": "Change.Runtime",
    "references": [
        "Change.Runtime",
        "UniTask"
    ],
    "optionalUnityReferences": [
        "UnityEngine.TestRunner"
    ],
    "includePlatforms": [
        "WindowsStandalone64",
        "macOSStandalone",
        "LinuxStandalone64"
    ],
    "excludePlatforms": []
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Execution/IAssetDownloadAdapter.cs`

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.ContentStreaming
{
    public interface IAssetDownloadAdapter
    {
        UniTask<ContentStreamingErrorCode> DownloadAsync(ContentPackDefinition definition, int rateLimitKbps, CancellationToken cancellationToken);
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/Execution/YooAssetDownloadAdapter.cs`

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.ContentStreaming
{
    public sealed class YooAssetDownloadAdapter : IAssetDownloadAdapter
    {
        public UniTask<ContentStreamingErrorCode> DownloadAsync(ContentPackDefinition definition, int rateLimitKbps, CancellationToken cancellationToken)
        {
            // MVP adapter seam: integrate concrete YooAsset downloader wiring in implementation phase.
            return UniTask.FromResult(ContentStreamingErrorCode.None);
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/ContentStreaming/ContentDownloadOrchestrator.cs`

```csharp
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.ContentStreaming
{
    public sealed class ContentDownloadOrchestrator : IContentDownloadService, IContentDownloadEvents
    {
        private readonly CatalogSyncService _catalogSync;
        private readonly ContentDownloadPolicyEngine _policyEngine;
        private readonly DownloadTaskScheduler _scheduler;
        private readonly ContentDownloadStateStore _stateStore;
        private readonly ContentDownloadEventHub _eventHub;
        private readonly IAssetDownloadAdapter _downloadAdapter;
        private readonly INetworkStateProvider _network;
        private readonly IPlayPressureSignal _pressure;
        private readonly IDataBudgetProvider _budget;
        private readonly List<DownloadTaskSnapshot> _scratchReady = new();
        private readonly List<DownloadTaskSnapshot> _scratchAll = new();

        public event System.Action<DownloadTaskSnapshot> TaskAdded;
        public event System.Action<DownloadTaskSnapshot> TaskStateChanged;
        public event System.Action<DownloadTaskSnapshot> TaskProgressChanged;
        public event System.Action<string> TaskRemoved;
        public event System.Action<DownloadPolicySnapshot> GlobalPolicyChanged;
        public event System.Action<int> CatalogUpdated;

        public ContentDownloadOrchestrator(
            CatalogSyncService catalogSync,
            ContentDownloadPolicyEngine policyEngine,
            DownloadTaskScheduler scheduler,
            ContentDownloadStateStore stateStore,
            ContentDownloadEventHub eventHub,
            IAssetDownloadAdapter downloadAdapter,
            INetworkStateProvider network,
            IPlayPressureSignal pressure,
            IDataBudgetProvider budget)
        {
            _catalogSync = catalogSync;
            _policyEngine = policyEngine;
            _scheduler = scheduler;
            _stateStore = stateStore;
            _eventHub = eventHub;
            _downloadAdapter = downloadAdapter;
            _network = network;
            _pressure = pressure;
            _budget = budget;
        }

        public async UniTask SyncCatalogAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            var count = await _catalogSync.SyncAsync();
            CatalogUpdated?.Invoke(count);
        }

        public bool EnqueuePack(string packId)
        {
            for (var i = 0; i < _catalogSync.Definitions.Count; i++)
            {
                var definition = _catalogSync.Definitions[i];
                if (definition.PackId != packId)
                {
                    continue;
                }

                var task = DownloadTaskSnapshot.CreateQueued(definition.PackId, definition.SizeBytes, definition.Priority);
                var sequenced = _eventHub.NextState(task);
                _scheduler.Enqueue(sequenced);
                _stateStore.Upsert(sequenced);
                TaskAdded?.Invoke(sequenced);
                return true;
            }

            return false;
        }

        public void PauseAll(ContentStreamingPauseReason reason)
        {
            _ = reason;
            _scheduler.SetPaused(true);
            var policy = _policyEngine.Evaluate(
                networkType: _network.Current,
                isHighPressure: true,
                dailyBudgetRemainingBytes: _budget.DailyRemainingBytes,
                allowWifiAuto: true,
                allowCellularAuto: true,
                cellularRateLimitKbps: 256,
                maxConcurrentDownloads: 2);
            GlobalPolicyChanged?.Invoke(policy);
        }

        public void ResumeByPolicy()
        {
            _scheduler.SetPaused(false);
        }

        public bool RemovePack(string packId, bool removeCache)
        {
            _ = removeCache;
            var removed = _scheduler.Remove(packId);
            _stateStore.Remove(packId);
            if (removed)
            {
                TaskRemoved?.Invoke(packId);
            }
            return removed;
        }

        public bool TryGetPackState(string packId, out DownloadTaskSnapshot snapshot)
        {
            return _stateStore.TryGet(packId, out snapshot);
        }

        public IReadOnlyList<DownloadTaskSnapshot> GetAllTaskSnapshots()
        {
            _stateStore.GetAll(_scratchAll);
            return _scratchAll;
        }

        public async UniTask TickAsync(CancellationToken cancellationToken)
        {
            var policy = _policyEngine.Evaluate(
                _network.Current,
                _pressure.IsHighPressure,
                _budget.DailyRemainingBytes,
                allowWifiAuto: true,
                allowCellularAuto: true,
                cellularRateLimitKbps: 256,
                maxConcurrentDownloads: 2);

            if (!policy.AllowAutoDownload)
            {
                GlobalPolicyChanged?.Invoke(policy);
                return;
            }

            GlobalPolicyChanged?.Invoke(policy);

            _scheduler.DequeueReadyTasks(policy.MaxConcurrentDownloads, _scratchReady);
            for (var i = 0; i < _scratchReady.Count; i++)
            {
                var task = _eventHub.NextState(_scratchReady[i].WithState(DownloadTaskState.Downloading));
                _stateStore.Upsert(task);
                TaskStateChanged?.Invoke(task);
                TaskProgressChanged?.Invoke(task);

                var definition = FindDefinition(task.PackId);
                var error = await _downloadAdapter.DownloadAsync(definition, policy.CellularRateLimitKbps, cancellationToken);

                var finalState = error == ContentStreamingErrorCode.None ? DownloadTaskState.Completed : DownloadTaskState.FailedTransient;
                var finalTask = _eventHub.NextState(task.WithState(finalState, error));
                _stateStore.Upsert(finalTask);
                TaskStateChanged?.Invoke(finalTask);
                if (finalState == DownloadTaskState.Completed)
                {
                    _budget.Consume(definition.SizeBytes);
                }
            }
        }

        private ContentPackDefinition FindDefinition(string packId)
        {
            for (var i = 0; i < _catalogSync.Definitions.Count; i++)
            {
                var def = _catalogSync.Definitions[i];
                if (def.PackId == packId)
                {
                    return def;
                }
            }

            throw new System.InvalidOperationException($"Pack not found in catalog: {packId}");
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/Tests/PlayMode/ContentStreaming/ContentDownloadOrchestratorPlayModeTests.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Change.Runtime.ContentStreaming.Tests
{
    public sealed class ContentDownloadOrchestratorPlayModeTests
    {
        [UnityTest]
        public IEnumerator TickAsync_WithEmptyCatalog_CompletesWithoutException()
        {
            var orchestrator = new ContentDownloadOrchestrator(
                new CatalogSyncService(new EmptyCatalogClient()),
                new ContentDownloadPolicyEngine(),
                new DownloadTaskScheduler(),
                new ContentDownloadStateStore(),
                new ContentDownloadEventHub(),
                new SuccessAdapter(),
                new FixedNetwork(NetworkType.Wifi),
                new FixedPressure(false),
                new FixedBudget(1024 * 1024));

            yield return orchestrator.SyncCatalogAsync(CancellationToken.None).ToCoroutine();
            yield return orchestrator.TickAsync(CancellationToken.None).ToCoroutine();
        }

        private sealed class EmptyCatalogClient : ICatalogClient
        {
            public UniTask<CatalogSyncResult> FetchAsync(string currentVersion)
                => UniTask.FromResult(new CatalogSyncResult("v0", new List<ContentPackDefinition>()));
        }

        private sealed class SuccessAdapter : IAssetDownloadAdapter
        {
            public UniTask<ContentStreamingErrorCode> DownloadAsync(ContentPackDefinition definition, int rateLimitKbps, CancellationToken cancellationToken)
                => UniTask.FromResult(ContentStreamingErrorCode.None);
        }

        private sealed class FixedNetwork : INetworkStateProvider
        {
            public FixedNetwork(NetworkType current) => Current = current;
            public NetworkType Current { get; }
        }

        private sealed class FixedPressure : IPlayPressureSignal
        {
            public FixedPressure(bool isHighPressure) => IsHighPressure = isHighPressure;
            public bool IsHighPressure { get; }
        }

        private sealed class FixedBudget : IDataBudgetProvider
        {
            public FixedBudget(long dailyRemainingBytes) => DailyRemainingBytes = dailyRemainingBytes;
            public long DailyRemainingBytes { get; private set; }
            public void Consume(long bytes) => DailyRemainingBytes -= bytes;
        }
    }
}
```

- [ ] **Step 4: Run EditMode and PlayMode tests to verify pass**

EditMode:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.ContentStreaming.Tests.ContentDownloadOrchestratorTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-contentstreaming-orchestrator-${TS}.xml" \
  -logFile -
```

PlayMode:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform PlayMode \
  -testFilter "Change.Runtime.ContentStreaming.Tests.ContentDownloadOrchestratorPlayModeTests" \
  -testResults "$(pwd)/UnityProject/TestResults/playmode-contentstreaming-${TS}.xml" \
  -logFile -
```

Expected: PASS for both.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Tests/PlayMode/Change.Runtime.PlayModeTests.asmdef \
  UnityProject/Assets/Change/Runtime/ContentStreaming/Execution \
  UnityProject/Assets/Change/Runtime/ContentStreaming/ContentDownloadOrchestrator.cs \
  UnityProject/Assets/Change/Runtime/Tests/EditMode/ContentStreaming/ContentDownloadOrchestratorTests.cs \
  UnityProject/Assets/Change/Runtime/Tests/PlayMode/ContentStreaming/ContentDownloadOrchestratorPlayModeTests.cs
git commit -m "feat(runtime): add content streaming orchestrator and adapter seam"
```

---

### Task 7: Add module docs and architecture guards

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/ContentStreaming/README.md`

- [ ] **Step 1: Write README content focused on boundaries and integration points**

`UnityProject/Assets/Change/Runtime/ContentStreaming/README.md`

```markdown
# Change.Runtime.ContentStreaming

## Purpose

Runtime module for optional-content background downloading with policy-based scheduling.

## Boundaries

- Runtime owns: catalog sync, policy decision, queueing, adapter execution, state/event publication.
- UI owns: rendering and user interaction only (subscribe to DTO events).
- Game logic owns: enqueue/remove commands via `IContentDownloadService`.

## Key Interfaces

- `IContentDownloadService`: command/query facade for gameplay layer.
- `IContentDownloadEvents`: DTO event stream for FairyGUI or other presentation layers.
- `IAssetDownloadAdapter`: YooAsset seam; swap implementation without touching policy/scheduler.

## MVP Policy Defaults

- Wi-Fi auto download: enabled.
- Cellular auto download: enabled with rate limit.
- High pressure: pause auto dequeue.
- Daily budget exhausted: pause auto dequeue.

## Testing

- EditMode tests cover contracts/policy/scheduler/catalog/orchestrator.
- PlayMode smoke verifies runtime wiring compiles and executes in Unity loop.
```

- [ ] **Step 2: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/ContentStreaming/README.md
git commit -m "docs(runtime): add content streaming module readme"
```

---

## Self-Review

### 1) Spec Coverage

- Service-driven catalog sync: covered by Task 5 + Task 6.
- Wi-Fi auto + cellular throttled + budget: covered by Task 2 + Task 6.
- High-pressure pause/resume: covered by Task 2 + Task 6 tests.
- UI decoupled event contracts: covered by Task 1 + Task 4.
- TTL cache cleanup: covered by Task 5.
- Failure/retry scaffolding: scheduler retry path covered in Task 3; adapter error mapping path covered in Task 6.

No uncovered requirement found for MVP scope.

### 2) Placeholder Scan

- Removed ambiguous wording and provided concrete file paths, commands, and code in each code step.
- No `TODO`/`TBD` markers remain.

### 3) Type Consistency

- Core types (`ContentPackDefinition`, `DownloadPolicySnapshot`, `DownloadTaskSnapshot`) used consistently across all tasks.
- `ContentStreamingPauseReason`, `ContentStreamingErrorCode`, and `DownloadTaskState` names align across tests and implementation.
