using System;
using Change.Runtime.Composition;
using Change.Runtime.Net;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Change.Runtime.Tests.Composition
{
    public sealed class GameRootScopeTests
    {
        private sealed class Track : IDisposable
        {
            public bool Disposed;

            public void Dispose() => Disposed = true;
        }

        private sealed class Engine : LifetimeScope
        {
            protected override void Configure(IContainerBuilder builder)
            {
                builder.Register<INetworkGateway, NoOpNetworkGateway>(Lifetime.Singleton);
            }
        }

        private sealed class HotfixInstaller : IHotfixGameInstaller
        {
            public void Install(IContainerBuilder builder)
            {
                builder.Register<Track>(Lifetime.Scoped);
            }
        }

        [Test]
        public void DisposeGameRoot_DisposesTransientResolvedFromChild()
        {
            var go = new GameObject("Engine");
            var engine = go.AddComponent<Engine>();
            engine.Build();

            var host = new GameCompositionHost(engine);
            var child = host.CreateGameRoot(new HotfixInstaller());

            var t = child.Container.Resolve<Track>();
            Assert.IsFalse(t.Disposed);

            child.DisposeCore();
            Assert.IsTrue(t.Disposed);

            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }
}
