using System;
using System.Collections;
using System.Threading;
using Change.Framework.Application;
using Change.Framework.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Change.Runtime.UI.Tests
{
    public sealed class WindowManagerPresenterHostTests
    {
        private sealed class SequenceHost : IWindowPresenterHost
        {
            public int Opened;
            public int Closed;
            public DummyPresenter LastPresenter;

            public void OnOpened(in WindowRequest request, IWindowView view, out IDisposable windowScope, out IPresenter presenter)
            {
                Opened++;
                windowScope = null;
                presenter = new DummyPresenter();
                LastPresenter = (DummyPresenter)presenter;
            }

            public void OnClosing(in WindowRequest request, IWindowView view, IPresenter presenter, IDisposable windowScope)
            {
                Closed++;
            }
        }

        private sealed class DummyPresenter : IPresenter
        {
            public int OpenCount;
            public int CloseCount;

            public void OnOpen() => OpenCount++;

            public void OnClose() => CloseCount++;
        }

        [UnityTest]
        public IEnumerator OpenThenClose_InvokesHostAndPresenterLifecycle()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new FakeWindowFactory();
                var host = new SequenceHost();
                var manager = new WindowManager(factory, host);
                var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);

                var view = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);

                Assert.AreEqual(1, host.Opened);
                Assert.IsNotNull(host.LastPresenter);
                Assert.AreEqual(1, host.LastPresenter.OpenCount);
                Assert.AreEqual(0, host.LastPresenter.CloseCount);

                var closed = manager.Close(in request);

                Assert.IsTrue(closed);
                Assert.AreEqual(1, host.LastPresenter.CloseCount);
                Assert.AreEqual(1, host.Closed);
                Assert.AreEqual(0, view.DisposeCount);
            });
        }

        [UnityTest]
        public IEnumerator OpenThenCloseThenOpenViaCache_InvokesLifecycleAgain()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new FakeWindowFactory();
                var host = new SequenceHost();
                var manager = new WindowManager(factory, host);
                var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);

                var view = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);
                Assert.AreEqual(1, host.Opened);
                var firstPresenter = host.LastPresenter;
                Assert.AreEqual(1, firstPresenter.OpenCount);
                Assert.AreEqual(0, firstPresenter.CloseCount);

                manager.Close(in request);
                Assert.AreEqual(1, firstPresenter.CloseCount);
                Assert.AreEqual(1, host.Closed);

                // Cache-hit reopen: should fire OnOpened and presenter.OnOpen again
                var reopened = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);

                Assert.AreSame(view, reopened);
                Assert.AreEqual(2, host.Opened);
                var secondPresenter = host.LastPresenter;
                Assert.AreNotSame(firstPresenter, secondPresenter);
                Assert.AreEqual(1, secondPresenter.OpenCount);
                Assert.AreEqual(0, secondPresenter.CloseCount);
            });
        }

        [UnityTest]
        public IEnumerator OpenThenCloseThenOpenViaCache_ThenCloseAgain_InvokesLifecycle()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var factory = new FakeWindowFactory();
                var host = new SequenceHost();
                var manager = new WindowManager(factory, host);
                var request = new WindowRequest(new WindowId("Inventory"), WindowOpenOptions.Default);

                // First open
                var view = (FakeWindowView)await manager.OpenAsync(in request, CancellationToken.None);
                Assert.AreEqual(1, host.Opened);
                var p1 = host.LastPresenter;

                // Close → cache
                manager.Close(in request);
                Assert.AreEqual(1, p1.CloseCount);
                Assert.AreEqual(1, host.Closed);

                // Cache-hit open
                await manager.OpenAsync(in request, CancellationToken.None);
                Assert.AreEqual(2, host.Opened);
                var p2 = host.LastPresenter;
                Assert.AreEqual(1, p2.OpenCount);

                // Close again → cache again
                manager.Close(in request);
                Assert.AreEqual(1, p2.CloseCount);
                Assert.AreEqual(2, host.Closed);

                // Cache-hit open again
                await manager.OpenAsync(in request, CancellationToken.None);
                Assert.AreEqual(3, host.Opened);
                var p3 = host.LastPresenter;
                Assert.AreNotSame(p2, p3);
                Assert.AreEqual(1, p3.OpenCount);

                Assert.AreEqual(0, view.DisposeCount);
            });
        }
    }
}
