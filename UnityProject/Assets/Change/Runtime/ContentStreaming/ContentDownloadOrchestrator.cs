using System;
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
        private readonly List<DownloadTaskSnapshot> _readyBuffer = new();
        private readonly List<DownloadTaskSnapshot> _snapshotBuffer = new();

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
            _catalogSync = catalogSync ?? throw new ArgumentNullException(nameof(catalogSync));
            _policyEngine = policyEngine ?? throw new ArgumentNullException(nameof(policyEngine));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
            _downloadAdapter = downloadAdapter ?? throw new ArgumentNullException(nameof(downloadAdapter));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _pressure = pressure ?? throw new ArgumentNullException(nameof(pressure));
            _budget = budget ?? throw new ArgumentNullException(nameof(budget));
        }

        public event Action<DownloadTaskSnapshot> TaskAdded;
        public event Action<DownloadTaskSnapshot> TaskStateChanged;
        public event Action<DownloadTaskSnapshot> TaskProgressChanged;
        public event Action<string> TaskRemoved;
        public event Action<DownloadPolicySnapshot> GlobalPolicyChanged;
        public event Action<int> CatalogUpdated;

        public async UniTask SyncCatalogAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = await _catalogSync.SyncAsync();
            CatalogUpdated?.Invoke(count);
        }

        public bool EnqueuePack(string packId)
        {
            for (var i = 0; i < _catalogSync.Definitions.Count; i++)
            {
                var definition = _catalogSync.Definitions[i];
                if (!string.Equals(definition.PackId, packId, StringComparison.Ordinal))
                {
                    continue;
                }

                var task = DownloadTaskSnapshot.CreateQueued(
                    definition.PackId,
                    definition.SizeBytes,
                    definition.Priority);

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
            _scheduler.SetPaused(true);
            var pausedPolicy = _policyEngine.Evaluate(
                _network.Current,
                isHighPressure: true,
                dailyBudgetRemainingBytes: _budget.DailyRemainingBytes,
                allowWifiAuto: true,
                allowCellularAuto: true,
                cellularRateLimitKbps: 256,
                maxConcurrentDownloads: 2);
            GlobalPolicyChanged?.Invoke(pausedPolicy);
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
            _stateStore.GetAll(_snapshotBuffer);
            return _snapshotBuffer;
        }

        public async UniTask TickAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var policy = _policyEngine.Evaluate(
                _network.Current,
                _pressure.IsHighPressure,
                _budget.DailyRemainingBytes,
                allowWifiAuto: true,
                allowCellularAuto: true,
                cellularRateLimitKbps: 256,
                maxConcurrentDownloads: 2);

            GlobalPolicyChanged?.Invoke(policy);
            if (!policy.AllowAutoDownload)
            {
                return;
            }

            _scheduler.DequeueReadyTasks(policy.MaxConcurrentDownloads, _readyBuffer);
            for (var i = 0; i < _readyBuffer.Count; i++)
            {
                var queued = _readyBuffer[i];
                var downloading = _eventHub.NextState(queued.WithState(DownloadTaskState.Downloading));
                _stateStore.Upsert(downloading);
                TaskStateChanged?.Invoke(downloading);
                TaskProgressChanged?.Invoke(downloading);

                var definition = FindDefinition(downloading.PackId);
                var error = await _downloadAdapter.DownloadAsync(definition, policy.CellularRateLimitKbps, cancellationToken);
                var finalState = error == ContentStreamingErrorCode.None
                    ? DownloadTaskState.Completed
                    : DownloadTaskState.FailedTransient;

                var finalSnapshot = _eventHub.NextState(downloading.WithState(finalState, error));
                _stateStore.Upsert(finalSnapshot);
                TaskStateChanged?.Invoke(finalSnapshot);

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
                var definition = _catalogSync.Definitions[i];
                if (string.Equals(definition.PackId, packId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            throw new InvalidOperationException($"Pack not found in catalog: {packId}");
        }
    }
}
