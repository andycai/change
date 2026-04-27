using System;
using Change.Framework.Application;
using Change.Framework.UI;
using NUnit.Framework;

namespace Change.Framework.Tests.Application
{
    public class ApplicationContractsTests
    {
        private readonly struct DummyRequest
        {
            public DummyRequest(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private sealed class DummyUseCase : IUseCase<DummyRequest, int>
        {
            public int Execute(in DummyRequest request) => request.Value + 1;
        }

        private sealed class DummyPresenter : IPresenter
        {
            public int OpenCount { get; private set; }
            public int CloseCount { get; private set; }

            public void OnOpen() => OpenCount++;
            public void OnClose() => CloseCount++;
        }

        private sealed class DummyAppFacade : IAppFacade
        {
            private readonly IUseCase<DummyRequest, int> _useCase;

            public DummyAppFacade(IUseCase<DummyRequest, int> useCase)
            {
                _useCase = useCase;
            }

            public int Run(in DummyRequest request) => _useCase.Execute(in request);
        }

        [Test]
        public void WindowId_RejectsNullOrWhiteSpace()
        {
            Assert.Throws<ArgumentException>(() => _ = new WindowId(null));
            Assert.Throws<ArgumentException>(() => _ = new WindowId(string.Empty));
            Assert.Throws<ArgumentException>(() => _ = new WindowId("   "));
        }

        [Test]
        public void WindowId_Default_ThrowsInvalidOperationException()
        {
            var id = default(WindowId);

            Assert.Throws<InvalidOperationException>(() => _ = id.Value);
            Assert.Throws<InvalidOperationException>(() => _ = id.GetHashCode());
            Assert.Throws<InvalidOperationException>(() => _ = id.ToString());
            Assert.Throws<InvalidOperationException>(() => _ = id.Equals(new WindowId("Inventory")));
        }

        [Test]
        public void WindowId_Equality_UsesValueSemantics()
        {
            var a = new WindowId("Inventory");
            var b = new WindowId("Inventory");

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void WindowOpenOptions_DefaultsToNormalLayerAndReuse()
        {
            var options = WindowOpenOptions.Default;

            Assert.AreEqual(WindowLayer.Normal, options.Layer);
            Assert.IsTrue(options.ReuseIfLoaded);
            Assert.IsFalse(options.AllowMultipleInstances);
            Assert.AreEqual(0, options.InstanceId);
        }

        [Test]
        public void UseCase_ExecutesWithInParameter()
        {
            var useCase = new DummyUseCase();
            var request = new DummyRequest(41);

            var result = useCase.Execute(in request);

            Assert.AreEqual(42, result);
        }

        [Test]
        public void Presenter_OnOpenAndOnClose_CanDriveLifecycle()
        {
            var presenter = new DummyPresenter();

            presenter.OnOpen();
            presenter.OnClose();

            Assert.AreEqual(1, presenter.OpenCount);
            Assert.AreEqual(1, presenter.CloseCount);
        }

        [Test]
        public void AppFacade_CanWrapUseCaseExecution()
        {
            IAppFacade facade = new DummyAppFacade(new DummyUseCase());
            var appFacade = (DummyAppFacade)facade;
            var request = new DummyRequest(9);

            var result = appFacade.Run(in request);

            Assert.AreEqual(10, result);
        }
    }
}
