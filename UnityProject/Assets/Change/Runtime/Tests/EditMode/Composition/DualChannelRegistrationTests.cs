using Change.Runtime.Net;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Change.Runtime.Tests.Composition
{
    public sealed class DualChannelRegistrationTests
    {
        private sealed class TestEngineScope : LifetimeScope
        {
            protected override void Configure(IContainerBuilder builder)
            {
                builder.Register<INetworkGateway, NoOpNetworkGateway>(Lifetime.Singleton);
            }
        }

        [Test]
        public void EngineScope_Resolves_INetworkGateway()
        {
            var go = new GameObject("TestEngineScope");
            var scope = go.AddComponent<TestEngineScope>();
            scope.Build();

            var gateway = scope.Container.Resolve<INetworkGateway>();
            Assert.IsInstanceOf<NoOpNetworkGateway>(gateway);
        }
    }
}
