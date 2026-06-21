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
            state = new QuestSessionState();
            wallet = new QuestRewardWallet();
            return new CqrsBus();
        }

        [Test]
        public void MainQuest_Linear_AcrossSteps()
        {
            var bus = CreateBus(out var state, out var wallet);
            bus.Send(new BumpMainQuestProgressCommand(state, 2));
            bus.Send(new AdvanceMainQuestStepCommand(state, wallet));
            Assert.AreEqual(1, state.MainIndex);
            bus.Send(new BumpMainQuestProgressCommand(state, 2));
            bus.Send(new AdvanceMainQuestStepCommand(state, wallet));
            Assert.AreEqual(2, state.MainIndex);
        }

        [Test]
        public void SideQuests_ClaimIndependent()
        {
            var bus = CreateBus(out var state, out var wallet);
            bus.Send(new BumpSideQuestProgressCommand(state, 1, 3));
            bus.Send(new ClaimSideQuestRewardCommand(state, wallet, 1));
            bus.Send(new BumpSideQuestProgressCommand(state, 2, 3));
            bus.Send(new ClaimSideQuestRewardCommand(state, wallet, 2));
            Assert.AreEqual(25, wallet.Gold);
        }

        [Test]
        public void Daily_CannotClaimTwice()
        {
            var bus = CreateBus(out var state, out var wallet);
            bus.Send(new BumpDailyQuestProgressCommand(state, 1, 1));
            bus.Send(new ClaimDailyQuestRewardCommand(state, wallet, 1));
            Assert.Throws<InvalidOperationException>(() => bus.Send(new ClaimDailyQuestRewardCommand(state, wallet, 1)));
        }

        [Test]
        public void MainQuest_FinalStep_CompletesWithoutNextIndex()
        {
            var bus = CreateBus(out var state, out var wallet);
            for (var step = 0; step < 3; step++)
            {
                bus.Send(new BumpMainQuestProgressCommand(state, 2));
                bus.Send(new AdvanceMainQuestStepCommand(state, wallet));
            }

            Assert.IsTrue(state.MainCompleted);
            Assert.AreEqual(60, wallet.Gold);
        }

        [Test]
        public void AdvanceMain_WithoutProgress_Throws()
        {
            var bus = CreateBus(out var state, out var wallet);
            Assert.Throws<InvalidOperationException>(() => bus.Send(new AdvanceMainQuestStepCommand(state, wallet)));
        }

        [Test]
        public void BumpSide_AfterClaim_Throws()
        {
            var bus = CreateBus(out var state, out var wallet);
            bus.Send(new BumpSideQuestProgressCommand(state, 1, 3));
            bus.Send(new ClaimSideQuestRewardCommand(state, wallet, 1));
            Assert.Throws<InvalidOperationException>(() => bus.Send(new BumpSideQuestProgressCommand(state, 1, 1)));
        }

        [Test]
        public void GetQuestPanelQuery_ReturnsWalletAndRows()
        {
            var bus = CreateBus(out var state, out var wallet);
            wallet.AddGold(7);
            var snap = bus.Ask<GetQuestPanelQuery, QuestPanelSnapshot>(
                new GetQuestPanelQuery(state, wallet));
            Assert.AreEqual(7, snap.WalletGold);
            Assert.AreEqual(2, snap.Sides.Count);
            Assert.AreEqual(2, snap.Dailies.Count);
        }
    }
}
