using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Change.Framework.UI;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Change.Runtime.UI.Tests
{
    public class WindowManagerCoreTests
    {
        [UnityTest]
        public IEnumerator OpenAsync_ReusesExistingWindow_WhenReuseEnabled()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new FakeWindowFactory();
                var manager = new WindowManager(factory);
                var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);

                var first = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);
                var second = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);

                Assert.AreSame(first, second);
                Assert.AreEqual(1, factory.CreateCount);
                Assert.AreEqual(1, first.BringToFrontCount);
                Assert.AreEqual(2, first.SetVisibleCount);
            });
        }

        [UnityTest]
        public IEnumerator OpenAsync_DedupsConcurrentInflightRequests()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new ControlledWindowFactory();
                var manager = new WindowManager(factory);
                var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);

                var firstOpen = manager.OpenAsync(in request, CancellationToken.None).AsTask();
                var secondOpen = manager.OpenAsync(in request, CancellationToken.None).AsTask();

                Assert.AreEqual(1, factory.CreateCount);
                Assert.AreEqual(1, factory.PendingCount);
                Assert.IsFalse(firstOpen.IsCompleted);
                Assert.IsFalse(secondOpen.IsCompleted);

                var created = new FakeWindowView(request.Id);
                factory.CompleteNext(created);

                var first = (FakeWindowView)await firstOpen;
                var second = (FakeWindowView)await secondOpen;

                Assert.AreSame(first, second);
                Assert.AreSame(created, first);
                Assert.AreEqual(1, first.SetVisibleCount);
            });
        }

        [UnityTest]
        public IEnumerator OpenAsync_CancellationOnOneCaller_DoesNotCancelSharedInflight()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new ControlledWindowFactory();
                var manager = new WindowManager(factory);
                var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);
                var cancellation = new CancellationTokenSource();

                var cancelledOpen = manager.OpenAsync(in request, cancellation.Token).AsTask();
                var successfulOpen = manager.OpenAsync(in request, CancellationToken.None).AsTask();

                Assert.AreEqual(1, factory.CreateCount);
                cancellation.Cancel();

                Assert.IsFalse(successfulOpen.IsCompleted);

                var created = new FakeWindowView(request.Id);
                factory.CompleteNext(created);

                try
                {
                    await cancelledOpen;
                    Assert.Fail("Expected cancellation for the caller token.");
                }
                catch (OperationCanceledException)
                {
                }

                var opened = (FakeWindowView)await successfulOpen;

                Assert.AreSame(created, opened);
                Assert.IsTrue(manager.TryGet(in request, out var cached));
                Assert.AreSame(opened, cached);
            });
        }

        [UnityTest]
        public IEnumerator OpenAsync_AllCallersCanceledBeforeCompletion_DoesNotCacheOrShow()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new ControlledWindowFactory();
                var manager = new WindowManager(factory);
                var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);
                var firstCancellation = new CancellationTokenSource();
                var secondCancellation = new CancellationTokenSource();

                var firstOpen = manager.OpenAsync(in request, firstCancellation.Token).AsTask();
                var secondOpen = manager.OpenAsync(in request, secondCancellation.Token).AsTask();

                Assert.AreEqual(1, factory.CreateCount);
                firstCancellation.Cancel();
                secondCancellation.Cancel();

                try
                {
                    await firstOpen;
                    Assert.Fail("Expected first caller cancellation.");
                }
                catch (OperationCanceledException)
                {
                }

                try
                {
                    await secondOpen;
                    Assert.Fail("Expected second caller cancellation.");
                }
                catch (OperationCanceledException)
                {
                }

                Assert.AreEqual(1, factory.SharedCancellationCount);

                var created = new FakeWindowView(request.Id);
                factory.CompleteNext(created);
                await UniTask.Yield();

                Assert.IsFalse(manager.TryGet(in request, out _));
                Assert.AreEqual(0, created.SetVisibleCount);
            });
        }

        [UnityTest]
        public IEnumerator OpenAsync_LateCallerAfterSharedCancellation_StartsFreshInflight()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new ControlledWindowFactory();
                var manager = new WindowManager(factory);
                var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);
                var cancellation = new CancellationTokenSource();

                var canceledOpen = manager.OpenAsync(in request, cancellation.Token).AsTask();
                Assert.AreEqual(1, factory.CreateCount);

                cancellation.Cancel();
                try
                {
                    await canceledOpen;
                    Assert.Fail("Expected first caller cancellation.");
                }
                catch (OperationCanceledException)
                {
                }

                var lateOpen = manager.OpenAsync(in request, CancellationToken.None).AsTask();
                Assert.AreEqual(2, factory.CreateCount);

                var canceledCreated = new FakeWindowView(request.Id);
                factory.CompleteNext(canceledCreated);
                await UniTask.Yield();

                Assert.IsFalse(lateOpen.IsCompleted);
                Assert.IsFalse(manager.TryGet(in request, out _));
                Assert.AreEqual(0, canceledCreated.SetVisibleCount);

                var lateCreated = new FakeWindowView(request.Id);
                factory.CompleteNext(lateCreated);
                var opened = (FakeWindowView)await lateOpen;

                Assert.AreSame(lateCreated, opened);
                Assert.IsTrue(manager.TryGet(in request, out var cached));
                Assert.AreSame(opened, cached);
            });
        }

        [UnityTest]
        public IEnumerator OpenAsync_CreatesDistinctInstances_WhenMultipleInstancesEnabled()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new FakeWindowFactory();
                var manager = new WindowManager(factory);
                var optionsA = new WindowOpenOptions(WindowLayer.Normal, reuseIfLoaded: false, allowMultipleInstances: true, instanceId: 1);
                var optionsB = new WindowOpenOptions(WindowLayer.Normal, reuseIfLoaded: false, allowMultipleInstances: true, instanceId: 2);
                var requestA = new WindowRequest(new WindowId("Inventory"), optionsA);
                var requestB = new WindowRequest(new WindowId("Inventory"), optionsB);

                var first = (FakeWindowView)await manager.OpenAsync(in requestA, CancellationToken.None);
                var second = (FakeWindowView)await manager.OpenAsync(in requestB, CancellationToken.None);

                Assert.AreNotSame(first, second);
                Assert.AreEqual(2, factory.CreateCount);
            });
        }

        [UnityTest]
        public IEnumerator OpenAsync_DisposesPreviousWindow_WhenReuseDisabledForSameIdentity()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new FakeWindowFactory();
                var manager = new WindowManager(factory);
                var options = new WindowOpenOptions(WindowLayer.Normal, reuseIfLoaded: false, allowMultipleInstances: false, instanceId: 0);
                var request = new WindowRequest(new WindowId("Inventory"), options);

                var first = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);
                var second = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);

                Assert.AreNotSame(first, second);
                Assert.AreEqual(1, first.DisposeCount);
                Assert.AreEqual(WindowState.Closed, first.State);
                Assert.IsTrue(manager.TryGet(in request, out var cached));
                Assert.AreSame(second, cached);
            });
        }

        [UnityTest]
        public IEnumerator Close_RemovesCachedWindow_AndTryGetReturnsFalse()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new FakeWindowFactory();
                var manager = new WindowManager(factory);
                var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);

                var view = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);
                var closed = manager.Close(in request);

                Assert.IsTrue(closed);
                Assert.IsFalse(manager.TryGet(in request, out _));
                Assert.AreEqual(1, view.DisposeCount);
            });
        }
    }

    internal sealed class FakeWindowFactory : IWindowFactory
    {
        public int CreateCount { get; private set; }

        public UniTask<IWindowView> CreateAsync(in WindowRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CreateCount++;
            return UniTask.FromResult<IWindowView>(new FakeWindowView(request.Id));
        }
    }

    internal sealed class ControlledWindowFactory : IWindowFactory
    {
        private readonly Queue<TaskCompletionSource<IWindowView>> _pending = new();
        private int _sharedCancellationCount;

        public int CreateCount { get; private set; }
        public int PendingCount => _pending.Count;
        public int SharedCancellationCount => _sharedCancellationCount;

        public UniTask<IWindowView> CreateAsync(in WindowRequest request, CancellationToken cancellationToken)
        {
            CreateCount++;

            var source = new TaskCompletionSource<IWindowView>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => Interlocked.Increment(ref _sharedCancellationCount));
            _pending.Enqueue(source);
            return source.Task.AsUniTask();
        }

        public void CompleteNext(IWindowView view)
        {
            if (_pending.Count == 0)
            {
                throw new InvalidOperationException("No pending create operation to complete.");
            }

            _pending.Dequeue().SetResult(view);
        }
    }

    internal sealed class FakeWindowView : IWindowView
    {
        public FakeWindowView(WindowId id, WindowLayer layer = WindowLayer.Normal)
        {
            Id = id;
            Layer = layer;
            State = WindowState.Closed;
        }

        public WindowId Id { get; }
        public WindowLayer Layer { get; }
        public WindowState State { get; private set; }
        public int BringToFrontCount { get; private set; }
        public int SetVisibleCount { get; private set; }
        public int DisposeCount { get; private set; }

        public void SetState(WindowState state)
        {
            State = state;
        }

        public void BringToFront()
        {
            BringToFrontCount++;
        }

        public void SetVisible(bool visible)
        {
            SetVisibleCount++;
            State = visible ? WindowState.Open : WindowState.Hidden;
        }

        public void Dispose()
        {
            DisposeCount++;
            State = WindowState.Closed;
        }
    }
}
