using Change.Framework.Cqrs;
using Change.Runtime.Composition;
using GameScript.UI.Quest;
using VContainer;

namespace GameScript.Composition
{
    public sealed class GameHotfixInstaller : IHotfixGameInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<QuestSessionState>(Lifetime.Singleton);
            builder.Register<QuestRewardWallet>(Lifetime.Singleton);
            builder.Register<CqrsBus>(Lifetime.Singleton);
            builder.Register<ICqrsBus>(c => c.Resolve<CqrsBus>(), Lifetime.Singleton);
            builder.Register<IOpenQuestPanelUseCase, OpenQuestPanelUseCase>(Lifetime.Transient);

            // Command/Query are self-handling structs — no registration needed.
            // Dependencies are injected via struct constructor fields at call sites.
        }
    }
}
