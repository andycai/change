using System;
using Change.Framework.Cqrs;
using Change.Framework.Pooling;

namespace GameScript.UI.Quest
{
    public sealed class AdvanceMainQuestStepHandler : ICommandHandler<AdvanceMainQuestStepCommand>, IPoolable
    {
        private readonly QuestSessionState _state;
        private readonly QuestRewardWallet _wallet;

        public AdvanceMainQuestStepHandler(QuestSessionState state, QuestRewardWallet wallet)
        {
            _state = state;
            _wallet = wallet;
        }

        public void Handle(in AdvanceMainQuestStepCommand command)
        {
            _state.CompleteMainIfReady();
            _wallet.AddGold(20);
        }

        public void Reset() { }
    }

    public sealed class BumpSideQuestProgressHandler : ICommandHandler<BumpSideQuestProgressCommand>, IPoolable
    {
        private readonly QuestSessionState _state;

        public BumpSideQuestProgressHandler(QuestSessionState state) => _state = state;

        public void Handle(in BumpSideQuestProgressCommand command)
        {
            var row = _state.GetSideOrThrow(command.SideId);
            if (row.RewardClaimed) throw new InvalidOperationException("Side quest already claimed.");
            row.Progress = Math.Min(row.Progress + command.Delta, row.Target);
        }

        public void Reset() { }
    }

    public sealed class ClaimSideQuestRewardHandler : ICommandHandler<ClaimSideQuestRewardCommand>, IPoolable
    {
        private readonly QuestSessionState _state;
        private readonly QuestRewardWallet _wallet;

        public ClaimSideQuestRewardHandler(QuestSessionState state, QuestRewardWallet wallet)
        {
            _state = state;
            _wallet = wallet;
        }

        public void Handle(in ClaimSideQuestRewardCommand command)
        {
            var row = _state.GetSideOrThrow(command.SideId);
            if (!row.CanClaim) throw new InvalidOperationException("Side quest not claimable.");
            row.RewardClaimed = true;
            _wallet.AddGold(row.RewardGold);
        }

        public void Reset() { }
    }

    public sealed class BumpDailyQuestProgressHandler : ICommandHandler<BumpDailyQuestProgressCommand>, IPoolable
    {
        private readonly QuestSessionState _state;

        public BumpDailyQuestProgressHandler(QuestSessionState state) => _state = state;

        public void Handle(in BumpDailyQuestProgressCommand command)
        {
            var row = _state.GetDailyOrThrow(command.DailyId);
            if (row.RewardClaimed) throw new InvalidOperationException("Daily quest reward already claimed.");
            row.Progress = Math.Min(row.Progress + command.Delta, row.Target);
        }

        public void Reset() { }
    }

    public sealed class ClaimDailyQuestRewardHandler : ICommandHandler<ClaimDailyQuestRewardCommand>, IPoolable
    {
        private readonly QuestSessionState _state;
        private readonly QuestRewardWallet _wallet;

        public ClaimDailyQuestRewardHandler(QuestSessionState state, QuestRewardWallet wallet)
        {
            _state = state;
            _wallet = wallet;
        }

        public void Handle(in ClaimDailyQuestRewardCommand command)
        {
            var row = _state.GetDailyOrThrow(command.DailyId);
            if (!row.CanClaim) throw new InvalidOperationException("Daily quest not claimable.");
            row.RewardClaimed = true;
            _wallet.AddGold(row.RewardGold);
        }

        public void Reset() { }
    }
}
