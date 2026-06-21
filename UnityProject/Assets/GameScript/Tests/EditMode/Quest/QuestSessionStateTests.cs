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
            // BumpMainQuestProgressCommand 已是 ISelfHandlingCommand，无需注册 Class handler。
            bootstrap.RegisterQuery(new GetQuestPanelQueryHandler(state, wallet));
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
            bus.Send(new BumpMainQuestProgressCommand(state, 2));
            bus.Send(new AdvanceMainQuestStepCommand());
            Assert.AreEqual(1, state.MainIndex);
            bus.Send(new BumpMainQuestProgressCommand(state, 2));
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

        [Test]
        public void MainQuest_FinalStep_CompletesWithoutNextIndex()
        {
            var bus = CreateBus(out var state, out var wallet);
            for (var step = 0; step < 3; step++)
            {
                bus.Send(new BumpMainQuestProgressCommand(state, 2));
                bus.Send(new AdvanceMainQuestStepCommand());
            }

            Assert.IsTrue(state.MainCompleted);
            Assert.AreEqual(60, wallet.Gold);
        }

        [Test]
        public void AdvanceMain_WithoutProgress_Throws()
        {
            var bus = CreateBus(out _, out _);
            Assert.Throws<InvalidOperationException>(() => bus.Send(new AdvanceMainQuestStepCommand()));
        }

        [Test]
        public void BumpSide_AfterClaim_Throws()
        {
            var bus = CreateBus(out _, out _);
            bus.Send(new BumpSideQuestProgressCommand(1, 3));
            bus.Send(new ClaimSideQuestRewardCommand(1));
            Assert.Throws<InvalidOperationException>(() => bus.Send(new BumpSideQuestProgressCommand(1, 1)));
        }

        [Test]
        public void GetQuestPanelQuery_ReturnsWalletAndRows()
        {
            var bus = CreateBus(out _, out var wallet);
            wallet.AddGold(7);
            var snap = bus.Ask<GetQuestPanelQuery, QuestPanelSnapshot>(new GetQuestPanelQuery());
            Assert.AreEqual(7, snap.WalletGold);
            Assert.AreEqual(2, snap.Sides.Count);
            Assert.AreEqual(2, snap.Dailies.Count);
        }
    }
}
