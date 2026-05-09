using System.Collections;
using Change.Framework.Cqrs;
using Cysharp.Threading.Tasks;
using GameScript.UI.Quest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Change.Runtime.Tests.PlayMode.Quest
{
    /// <summary>
    /// PlayMode: FairyGUI display objects expect a live Stage (EditMode 不稳定).
    /// </summary>
    public sealed class QuestFairyGuiPlayModeTests
    {
        private static ICqrsBus CreateBus(out QuestSessionState state, out QuestRewardWallet wallet)
        {
            var bus = new CqrsBus();
            var bootstrap = new CqrsBootstrap(bus);
            state = new QuestSessionState();
            wallet = new QuestRewardWallet();
            bootstrap.RegisterQuery(new GetQuestPanelQueryHandler(state, wallet));
            bootstrap.RegisterCommand(new BumpMainQuestProgressHandler(state));
            bootstrap.RegisterCommand(new AdvanceMainQuestStepHandler(state, wallet));
            bootstrap.RegisterCommand(new BumpSideQuestProgressHandler(state));
            bootstrap.RegisterCommand(new ClaimSideQuestRewardHandler(state, wallet));
            bootstrap.RegisterCommand(new BumpDailyQuestProgressHandler(state));
            bootstrap.RegisterCommand(new ClaimDailyQuestRewardHandler(state, wallet));
            bootstrap.Build();
            return bus;
        }

        [UnityTest]
        public IEnumerator QuestView_OnOpen_ShowsWalletFromQuery()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await UniTask.Yield(PlayerLoopTiming.Update);

                var bus = CreateBus(out _, out _);
                var useCase = new OpenQuestPanelUseCase(bus);
                var root = QuestUiRootBuilder.BuildRoot();
                var view = new QuestFairyGuiView(root);
                var presenter = new QuestWindowPresenter(view, useCase);
                presenter.SetActiveTab(0);
                presenter.OnOpen();

                Assert.AreEqual("0", root.GetChild("txtWallet").asTextField.text);

                bus.Send(new BumpSideQuestProgressCommand(1, 3));
                bus.Send(new ClaimSideQuestRewardCommand(1));
                presenter.OnOpen();

                Assert.AreEqual("10", root.GetChild("txtWallet").asTextField.text);
            });
        }
    }
}
