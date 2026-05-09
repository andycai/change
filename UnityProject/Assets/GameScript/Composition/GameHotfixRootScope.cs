using System.Collections.Generic;
using Change.Framework.UI;
using Change.Runtime.UI;
using GameScript.UI.Quest;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using YooAsset;

namespace GameScript.Composition
{
    public sealed class GameHotfixRootScope : LifetimeScope
    {
        [SerializeField] private LifetimeScope _engineScope;
        [SerializeField] private ResourcePackage _uiPackage;

        protected override void Awake()
        {
            if (_engineScope != null)
            {
                var p = parentReference;
                p.Object = _engineScope;
                parentReference = p;
            }
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            new GameHotfixInstaller().Install(builder);

            builder.RegisterInstance(_uiPackage);
            builder.Register<IUiAssetLoader>(c => YooUiAssetLoader.FromResourcePackage(c.Resolve<ResourcePackage>()), Lifetime.Singleton);
            builder.Register<IWindowLocationResolver>(_ =>
                new StaticWindowLocationResolver(new Dictionary<string, string>
                {
                    [WindowIds.Inventory.Value] = "ui/inventory.prefab",
                    [WindowIds.Quest.Value] = "ui/quest_panel.prefab"
                }), Lifetime.Singleton);
            builder.Register<IWindowFactory, FairyGuiWindowFactory>(Lifetime.Singleton);
            builder.Register<WindowManager>(c => new WindowManager(c.Resolve<IWindowFactory>(), c.Resolve<IWindowPresenterHost>()), Lifetime.Singleton);
            builder.Register<IQuestWindowPresenterFactory, QuestWindowPresenterFactory>(Lifetime.Singleton);
            builder.Register<IWindowPresenterHost, QuestGamePresenterHost>(Lifetime.Singleton);
        }
    }
}
