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

            builder.RegisterBuildCallback(c =>
            {
                var bus = c.Resolve<CqrsBus>();
                var bootstrap = new CqrsBootstrap(bus);
                var state = c.Resolve<QuestSessionState>();
                var wallet = c.Resolve<QuestRewardWallet>();

                bootstrap.RegisterQuery(new GetQuestPanelQueryHandler(state, wallet));
                bootstrap.RegisterCommand(new BumpMainQuestProgressHandler(state));
                bootstrap.RegisterCommand(new AdvanceMainQuestStepHandler(state, wallet));
                bootstrap.RegisterCommand(new BumpSideQuestProgressHandler(state));
                bootstrap.RegisterCommand(new ClaimSideQuestRewardHandler(state, wallet));
                bootstrap.RegisterCommand(new BumpDailyQuestProgressHandler(state));
                bootstrap.RegisterCommand(new ClaimDailyQuestRewardHandler(state, wallet));

                bootstrap.Build();
            });
        }
    }
}
