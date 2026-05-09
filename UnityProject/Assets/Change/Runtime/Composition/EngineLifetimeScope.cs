using Change.Runtime.App.Events;
using Change.Runtime.Net;
using VContainer;
using VContainer.Unity;

namespace Change.Runtime.Composition
{
    public sealed class EngineLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<INetworkGateway, NoOpNetworkGateway>(Lifetime.Singleton);
            builder.Register<IAppEventBus, InProcessAppEventBus>(Lifetime.Singleton);
        }
    }
}
