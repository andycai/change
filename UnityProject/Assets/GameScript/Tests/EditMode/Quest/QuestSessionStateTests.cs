using System;
using Change.Framework.Cqrs;
using GameScript.UI.Quest;
using NUnit.Framework;

namespace GameScript.Tests.Quest
{
    public sealed class QuestSessionStateTests
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

        [Test]
        public void MainQuest_Linear_AcrossSteps()
        {
            var bus = CreateBus(out var state, out _);
            bus.Send(new BumpMainQuestProgressCommand(2));
            bus.Send(new AdvanceMainQuestStepCommand());
            Assert.AreEqual(1, state.MainIndex);
            bus.Send(new BumpMainQuestProgressCommand(2));
            bus.Send(new AdvanceMainQuestStepCommand());
            Assert.AreEqual(2, state.MainIndex);
        }

        [Test]
        public void SideQuests_ClaimIndependent()
        {
            var bus = CreateBus(out _, out var wallet);
            bus.Send(new BumpSideQuestProgressCommand(1, 3));
            bus.Send(new ClaimSideQuestRewardCommand(1));
            bus.Send(new BumpSideQuestProgressCommand(2, 3));
            bus.Send(new ClaimSideQuestRewardCommand(2));
            Assert.AreEqual(25, wallet.Gold);
        }

        [Test]
        public void Daily_CannotClaimTwice()
        {
            var bus = CreateBus(out _, out _);
            bus.Send(new BumpDailyQuestProgressCommand(1, 1));
            bus.Send(new ClaimDailyQuestRewardCommand(1));
            Assert.Throws<InvalidOperationException>(() => bus.Send(new ClaimDailyQuestRewardCommand(1)));
        }
    }
}
