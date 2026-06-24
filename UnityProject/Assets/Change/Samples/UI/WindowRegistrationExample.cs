using VContainer;
using VContainer.Unity;
using Change.Runtime.UI.Core;
using Change.Runtime.UI.Abstractions;
using Change.Framework.UI;

namespace Change.Samples.UI
{
    public class WindowRegistrationExample : IStartable
    {
        private readonly IWindowRegistry _registry;

        public WindowRegistrationExample(IWindowRegistry registry)
        {
            _registry = registry;
        }

        void IStartable.Start()
        {
            // Register shop window (Normal group)
            _registry.Register(
                new WindowId("ShopWindow"),
                "UI_Shop",
                "ShopMain",
                "Shop",
                WindowLayer.Normal
            );

            // Register confirmation dialog (Overlay group)
            _registry.Register(
                new WindowId("ConfirmDialog"),
                "UI_Common",
                "ConfirmDialog",
                null,  // Overlay group
                WindowLayer.Popup
            );

            // Register guild window (Normal group)
            _registry.Register(
                new WindowId("GuildWindow"),
                "UI_Guild",
                "GuildMain",
                "Guild",
                WindowLayer.Normal
            );
        }
    }

    public class UIRegistrationScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IWindowRegistry, WindowRegistry>(Lifetime.Singleton);
            builder.RegisterEntryPoint<WindowRegistrationExample>();
        }
    }
}
