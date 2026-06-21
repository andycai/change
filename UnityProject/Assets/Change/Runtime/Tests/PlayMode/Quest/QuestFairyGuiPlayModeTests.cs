using System.Collections;
using Change.Framework.Cqrs;
using Cysharp.Threading.Tasks;
using GameScript.UI.Quest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Change.Runtime.Tests.PlayMode.Quest
{
    public sealed class QuestFairyGuiPlayModeTests
    {
        private static ICqrsBus CreateBus(out QuestSessionState state, out QuestRewardWallet wallet)
        {
            state = new QuestSessionState();
            wallet = new QuestRewardWallet();
            return new CqrsBus();
        }

        [UnityTest]
        public IEnumerator QuestView_OnOpen_ShowsWalletFromQuery()
        {
            return UniTask.ToCoroutine(async () =>
            {
                await UniTask.Yield(PlayerLoopTiming.Update);

                var bus = CreateBus(out var state, out var wallet);
                var useCase = new OpenQuestPanelUseCase(bus, state, wallet);
                var root = QuestUiRootBuilder.BuildRoot();
                var view = new QuestFairyGuiView(root);
                var presenter = new QuestWindowPresenter(view, useCase);
                presenter.SetActiveTab(0);
                presenter.OnOpen();

                Assert.AreEqual("0", root.GetChild("txtWallet").asTextField.text);

                bus.Send(new BumpSideQuestProgressCommand(state, 1, 3));
                bus.Send(new ClaimSideQuestRewardCommand(state, wallet, 1));
                presenter.OnOpen();

                Assert.AreEqual("10", root.GetChild("txtWallet").asTextField.text);
            });
        }
    }
}
