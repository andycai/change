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
                Assert.AreEqual(1, view.DisposeCount);
            });
        }
    }
}
