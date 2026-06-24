using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Change.Framework.Application;
using Change.Framework.UI;
using Change.Runtime.UI.Abstractions;
using Change.Runtime.UI.Core;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.UI
{
    public sealed class WindowManager
    {
        private sealed class InflightEntry
        {
            public InflightEntry()
            {
                Cancellation = new CancellationTokenSource();
                Completion = new TaskCompletionSource<IWindowView>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public CancellationTokenSource Cancellation { get; }
            public TaskCompletionSource<IWindowView> Completion { get; }
            public Task<IWindowView> Task => Completion.Task;
            public int WaiterCount { get; set; }
        }

        private readonly IWindowFactory _factory;
        private readonly IWindowPresenterHost _host;
        private readonly IWindowRegistry _registry;
        private readonly object _gate = new();
        private readonly Dictionary<WindowRequest, OpenedWindowEntry> _opened = new();
        private readonly Dictionary<WindowRequest, InflightEntry> _inflight = new();
        private readonly Dictionary<WindowRequest, CachedWindowEntry> _cache = new();
        private readonly LinkedList<WindowRequest> _cacheAccessOrder = new();
        private const int MaxCacheSize = 10;
        private string _currentActiveGroup;

        public WindowManager(IWindowFactory factory)
            : this(factory, NullWindowPresenterHost.Instance)
        {
        }

        public WindowManager(IWindowFactory factory, IWindowPresenterHost host)
            : this(factory, host, null)
        {
        }

        public WindowManager(IWindowFactory factory, IWindowPresenterHost host, IWindowRegistry registry)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _registry = registry; // null is allowed for backward compatibility
        }

        public bool TryGet(in WindowRequest request, out IWindowView view)
        {
            lock (_gate)
            {
                if (_opened.TryGetValue(request, out var entry))
                {
                    view = entry.View;
                    return true;
                }
            }

            view = null;
            return false;
        }

        public UniTask<IWindowView> OpenAsync(in WindowRequest request, CancellationToken cancellationToken)
        {
            var requestCopy = request;
            return OpenAsyncInternal(requestCopy, cancellationToken);
        }

        private async UniTask<IWindowView> OpenAsyncInternal(WindowRequest request, CancellationToken cancellationToken)
        {
            InflightEntry entry = null;
            IWindowView existing = null;
            OpenedWindowEntry toDisposeEntry = null;
            var shouldStartCreate = false;

            lock (_gate)
            {
                if (request.Options.ReuseIfLoaded && _opened.TryGetValue(request, out var reuseEntry))
                {
                    existing = reuseEntry.View;
                }
                else if (_inflight.TryGetValue(request, out entry))
                {
                    if (entry.Cancellation.IsCancellationRequested || entry.Task.IsCompleted)
                    {
                        // A canceled/stale inflight request should not absorb new callers.
                        _inflight.Remove(request);
                        entry = null;
                    }
                    else
                    {
                        entry.WaiterCount++;
                    }
                }

                if (entry == null && existing == null)
                {
                    if (!request.Options.ReuseIfLoaded && _opened.TryGetValue(request, out toDisposeEntry))
                    {
                        _opened.Remove(request);
                    }

                    entry = new InflightEntry
                    {
                        WaiterCount = 1,
                    };
                    _inflight[request] = entry;
                    shouldStartCreate = true;
                }
            }

            if (existing != null)
            {
                existing.BringToFront();
                existing.SetVisible(true);
                return existing;
            }

            if (toDisposeEntry != null)
            {
                DisposeOpenedEntryWithHost(in request, toDisposeEntry);
            }

            if (shouldStartCreate)
            {
                // Group mutual exclusion check (outside lock since CloseWindowsInGroup acquires lock internally)
                if (request.Group != null)
                {
                    if (_registry != null && _registry.TryGetMetadata(request.Id, out var metadata))
                    {
                        if (!IsOverlayGroup(metadata.Layer))
                        {
                            string groupToClose = null;
                            lock (_gate)
                            {
                                if (_currentActiveGroup != null && _currentActiveGroup != request.Group)
                                {
                                    groupToClose = _currentActiveGroup;
                                }

                                _currentActiveGroup = request.Group;
                            }

                            if (groupToClose != null)
                            {
                                CloseWindowsInGroup(groupToClose);
                            }
                        }
                    }
                }

                CreateAndCacheAsync(request, entry).Forget();
            }

            return await AwaitInflightAsync(entry, cancellationToken);
        }

        public bool Close(in WindowRequest request)
        {
            OpenedWindowEntry entry;
            lock (_gate)
            {
                if (!_opened.TryGetValue(request, out entry))
                {
                    return false;
                }

                _opened.Remove(request);
            }

            entry.Presenter?.OnClose();
            entry.WindowScope?.Dispose();
            _host.OnClosing(in request, entry.View, entry.Presenter, entry.WindowScope);
            entry.View.SetState(WindowState.Closing);
            entry.View.Dispose();
            return true;
        }

        private void DisposeOpenedEntryWithHost(in WindowRequest request, OpenedWindowEntry entry)
        {
            entry.Presenter?.OnClose();
            entry.WindowScope?.Dispose();
            _host.OnClosing(in request, entry.View, entry.Presenter, entry.WindowScope);
            entry.View.SetState(WindowState.Closing);
            entry.View.Dispose();
        }

        private static bool IsOverlayGroup(WindowLayer layer)
        {
            return layer == WindowLayer.Popup || layer == WindowLayer.Top;
        }

        private void CloseWindowsInGroup(string group)
        {
            // Collect entries to close under the lock
            var toClose = new List<(WindowRequest request, OpenedWindowEntry entry)>();
            lock (_gate)
            {
                foreach (var kvp in _opened)
                {
                    if (kvp.Key.Group == group)
                    {
                        toClose.Add((kvp.Key, kvp.Value));
                    }
                }

                foreach (var item in toClose)
                {
                    _opened.Remove(item.request);
                }
            }

            // Dispose outside the lock
            foreach (var (request, entry) in toClose)
            {
                var req = request;
                DisposeOpenedEntryWithHost(in req, entry);
            }
        }

        private async UniTaskVoid CreateAndCacheAsync(WindowRequest request, InflightEntry entry)
        {
            try
            {
                var created = await _factory.CreateAsync(in request, entry.Cancellation.Token);
                OpenedWindowEntry replacedEntry = null;
                var shouldSetVisible = false;
                lock (_gate)
                {
                    var isCurrentEntry = _inflight.TryGetValue(request, out var currentEntry) && ReferenceEquals(currentEntry, entry);
                    if (isCurrentEntry && !entry.Cancellation.IsCancellationRequested)
                    {
                        if (_opened.TryGetValue(request, out replacedEntry))
                        {
                            _opened.Remove(request);
                        }

                        shouldSetVisible = true;
                    }

                    if (isCurrentEntry)
                    {
                        _inflight.Remove(request);
                    }
                }

                if (!shouldSetVisible)
                {
                    created.Dispose();
                    entry.Cancellation.Cancel();
                    entry.Completion.TrySetCanceled(entry.Cancellation.Token);
                    return;
                }

                created.SetVisible(true);
                if (replacedEntry != null)
                {
                    DisposeOpenedEntryWithHost(in request, replacedEntry);
                }

                _host.OnOpened(in request, created, out var windowScope, out var presenter);
                presenter?.OnOpen();

                lock (_gate)
                {
                    _opened[request] = new OpenedWindowEntry(created, presenter, windowScope);
                }

                entry.Completion.TrySetResult(created);
            }
            catch (OperationCanceledException) when (entry.Cancellation.IsCancellationRequested)
            {
                RemoveInflightIfCurrent(request, entry);
                entry.Completion.TrySetCanceled(entry.Cancellation.Token);
            }
            catch (Exception exception)
            {
                RemoveInflightIfCurrent(request, entry);
                entry.Completion.TrySetException(exception);
            }
        }

        private async UniTask<IWindowView> AwaitInflightAsync(InflightEntry entry, CancellationToken cancellationToken)
        {
            try
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    return await entry.Task;
                }

                if (!entry.Task.IsCompleted)
                {
                    var cancellationSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    using (cancellationToken.Register(() => cancellationSignal.TrySetResult(true)))
                    {
                        var completed = await Task.WhenAny(entry.Task, cancellationSignal.Task);
                        if (!ReferenceEquals(completed, entry.Task))
                        {
                            throw new OperationCanceledException(cancellationToken);
                        }
                    }
                }

                return await entry.Task;
            }
            finally
            {
                ReleaseWaiter(entry);
            }
        }

        private void ReleaseWaiter(InflightEntry entry)
        {
            var shouldCancelCreate = false;
            lock (_gate)
            {
                entry.WaiterCount--;
                if (entry.WaiterCount == 0 && !entry.Task.IsCompleted)
                {
                    shouldCancelCreate = true;
                }
            }

            if (shouldCancelCreate)
            {
                entry.Cancellation.Cancel();
            }
        }

        private void RemoveInflightIfCurrent(WindowRequest request, InflightEntry entry)
        {
            lock (_gate)
            {
                if (_inflight.TryGetValue(request, out var currentEntry) && ReferenceEquals(currentEntry, entry))
                {
                    _inflight.Remove(request);
                }
            }
        }

        /// <summary>
        /// Called outside the lock. Acquires the lock internally for cache manipulation,
        /// then disposes evicted entries outside the lock.
        /// </summary>
        private void AddToCache(WindowRequest request, IWindowView view)
        {
            CachedWindowEntry evictedEntry = null;
            CachedWindowEntry newEntry;

            lock (_gate)
            {
                if (_cache.Count >= MaxCacheSize)
                {
                    evictedEntry = EvictLeastRecentlyUsed();
                }

                newEntry = new CachedWindowEntry(view, new CancellationTokenSource());
                _cache[request] = newEntry;
                _cacheAccessOrder.AddFirst(request);
            }

            ScheduleDelayedRelease(request, newEntry.ReleaseCts.Token).Forget();
            if (evictedEntry != null)
            {
                evictedEntry.View.Dispose();
                evictedEntry.Dispose();
            }
        }

        /// <summary>
        /// Must be called inside _gate lock.
        /// Returns the evicted entry so the caller can dispose it outside the lock.
        /// </summary>
        private CachedWindowEntry EvictLeastRecentlyUsed()
        {
            var lastNode = _cacheAccessOrder.Last;
            if (lastNode == null) return null;

            var lruRequest = lastNode.Value;
            _cacheAccessOrder.RemoveLast();
            _cache.Remove(lruRequest, out var evictedEntry);
            return evictedEntry;
        }

        private async UniTaskVoid ScheduleDelayedRelease(WindowRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay(30000, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // Delayed release failed; entry remains in cache until next eviction.
                return;
            }

            CachedWindowEntry entry;
            lock (_gate)
            {
                if (!_cache.TryGetValue(request, out entry))
                    return;
                _cache.Remove(request);

                var node = _cacheAccessOrder.First;
                while (node != null)
                {
                    if (node.Value.Equals(request))
                    {
                        _cacheAccessOrder.Remove(node);
                        break;
                    }

                    node = node.Next;
                }
            }

            entry.View.Dispose();
            entry.Dispose();
        }
    }
}
