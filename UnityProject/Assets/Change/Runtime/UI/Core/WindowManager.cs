using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly object _gate = new();
        private readonly Dictionary<WindowRequest, IWindowView> _opened = new();
        private readonly Dictionary<WindowRequest, InflightEntry> _inflight = new();

        public WindowManager(IWindowFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public bool TryGet(in WindowRequest request, out IWindowView view)
        {
            return _opened.TryGetValue(request, out view);
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
            IWindowView toDispose = null;
            var shouldStartCreate = false;

            lock (_gate)
            {
                if (request.Options.ReuseIfLoaded && _opened.TryGetValue(request, out existing))
                {
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
                    if (!request.Options.ReuseIfLoaded && _opened.TryGetValue(request, out toDispose))
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

            toDispose?.Dispose();

            if (shouldStartCreate)
            {
                CreateAndCacheAsync(request, entry).Forget();
            }

            return await AwaitInflightAsync(entry, cancellationToken);
        }

        public bool Close(in WindowRequest request)
        {
            IWindowView window;
            lock (_gate)
            {
                if (!_opened.TryGetValue(request, out window))
                {
                    return false;
                }

                _opened.Remove(request);
            }

            window.Dispose();
            return true;
        }

        private async UniTaskVoid CreateAndCacheAsync(WindowRequest request, InflightEntry entry)
        {
            try
            {
                var created = await _factory.CreateAsync(in request, entry.Cancellation.Token);
                IWindowView replaced = null;
                var shouldSetVisible = false;
                lock (_gate)
                {
                    var isCurrentEntry = _inflight.TryGetValue(request, out var currentEntry) && ReferenceEquals(currentEntry, entry);
                    if (isCurrentEntry && !entry.Cancellation.IsCancellationRequested)
                    {
                        if (_opened.TryGetValue(request, out replaced))
                        {
                            _opened.Remove(request);
                        }

                        _opened[request] = created;
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
                replaced?.Dispose();
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
    }
}
