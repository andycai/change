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
        private readonly HashSet<string> _activePacks = new();
        private CancellationTokenSource _pauseCts = new();

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
            _pauseCts.Cancel();
            _pauseCts.Dispose();
            _pauseCts = new CancellationTokenSource();

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
            _activePacks.Remove(packId);
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

            var limit = policy.MaxConcurrentDownloads - _activePacks.Count;
            if (limit <= 0)
            {
                return;
            }

            _scheduler.DequeueReadyTasks(limit, _readyBuffer);
            for (var i = 0; i < _readyBuffer.Count; i++)
            {
                var queued = _readyBuffer[i];
                if (_activePacks.Contains(queued.PackId))
                {
                    continue;
                }

                _activePacks.Add(queued.PackId);
                _scheduler.MarkDownloading(queued.PackId);

                var downloading = _eventHub.NextState(queued.WithState(DownloadTaskState.Downloading));
                _stateStore.Upsert(downloading);
                TaskStateChanged?.Invoke(downloading);

                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _pauseCts.Token);
                ExecuteDownloadAsync(downloading, policy, cts).Forget();
            }
        }

        private async UniTaskVoid ExecuteDownloadAsync(
            DownloadTaskSnapshot snapshot,
            DownloadPolicySnapshot policy,
            CancellationTokenSource cts)
        {
            try
            {
                var definition = FindDefinition(snapshot.PackId);
                var error = await _downloadAdapter.DownloadAsync(
                    definition,
                    policy.CellularRateLimitKbps,
                    downloadedBytes =>
                    {
                        if (_stateStore.TryGet(snapshot.PackId, out var current) &&
                            _eventHub.TryPublishProgress(current.WithProgress(downloadedBytes, 0), out var progress))
                        {
                            _stateStore.Upsert(progress);
                            TaskProgressChanged?.Invoke(progress);
                        }
                    },
                    cts.Token);

                var finalState = error == ContentStreamingErrorCode.None
                    ? DownloadTaskState.Completed
                    : (IsTransientError(error) ? DownloadTaskState.FailedTransient : DownloadTaskState.FailedTerminal);

                if (finalState == DownloadTaskState.FailedTransient)
                {
                    _scheduler.MarkTransientFailure(snapshot.PackId);
                }

                if (_stateStore.TryGet(snapshot.PackId, out var latest))
                {
                    var finalSnapshot = _eventHub.NextState(latest.WithState(finalState, error));
                    _stateStore.Upsert(finalSnapshot);
                    TaskStateChanged?.Invoke(finalSnapshot);

                    if (finalState == DownloadTaskState.Completed)
                    {
                        _budget.Consume(definition.SizeBytes);
                    }
                }
            }
            finally
            {
                _activePacks.Remove(snapshot.PackId);
                cts.Dispose();
            }
        }

        private static bool IsTransientError(ContentStreamingErrorCode error)
        {
            return error switch
            {
                ContentStreamingErrorCode.NetworkTimeout => true,
                ContentStreamingErrorCode.NetworkUnavailable => true,
                ContentStreamingErrorCode.ServerTemporary => true,
                _ => false
            };
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
