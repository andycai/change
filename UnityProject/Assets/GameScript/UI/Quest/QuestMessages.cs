using System;
using System.Collections.Generic;
using Change.Framework.Cqrs;

namespace GameScript.UI.Quest
{
    public readonly struct GetQuestPanelQuery : IQuery<QuestPanelSnapshot>
    {
        private readonly QuestSessionState _state;
        private readonly QuestRewardWallet _wallet;

        public GetQuestPanelQuery(QuestSessionState state, QuestRewardWallet wallet)
        {
            _state = state;
            _wallet = wallet;
        }

        public QuestPanelSnapshot Query()
        {
            var sides = new List<SideQuestVm>(_state.Sides.Count);
            foreach (var row in _state.Sides)
            {
                sides.Add(new SideQuestVm(row.Id, row.Progress, row.Target, row.RewardGold, row.CanClaim, row.RewardClaimed));
            }

            var dailies = new List<DailyQuestVm>(_state.Dailies.Count);
            foreach (var row in _state.Dailies)
            {
                dailies.Add(new DailyQuestVm(row.Id, row.Progress, row.Target, row.RewardGold, row.CanClaim, row.RewardClaimed));
            }

            return new QuestPanelSnapshot
            {
                Main = new MainQuestVm(_state.MainIndex, _state.MainProgress, _state.MainTarget, _state.MainCompleted),
                Sides = sides,
                Dailies = dailies,
                WalletGold = _wallet.Gold
            };
        }
    }

    public sealed class QuestPanelSnapshot
    {
        public MainQuestVm Main;
        public IReadOnlyList<SideQuestVm> Sides;
        public IReadOnlyList<DailyQuestVm> Dailies;
        public int WalletGold;
    }

    public readonly struct MainQuestVm
    {
        public MainQuestVm(int index, int progress, int target, bool completed)
        {
            Index = index;
            Progress = progress;
            Target = target;
            Completed = completed;
        }

        public int Index { get; }
        public int Progress { get; }
        public int Target { get; }
        public bool Completed { get; }
    }

    public readonly struct SideQuestVm
    {
        public SideQuestVm(int id, int progress, int target, int rewardGold, bool canClaim, bool claimed)
        {
            Id = id;
            Progress = progress;
            Target = target;
            RewardGold = rewardGold;
            CanClaim = canClaim;
            Claimed = claimed;
        }

        public int Id { get; }
        public int Progress { get; }
        public int Target { get; }
        public int RewardGold { get; }
        public bool CanClaim { get; }
        public bool Claimed { get; }
    }

    public readonly struct DailyQuestVm
    {
        public DailyQuestVm(int id, int progress, int target, int rewardGold, bool canClaim, bool claimed)
        {
            Id = id;
            Progress = progress;
            Target = target;
            RewardGold = rewardGold;
            CanClaim = canClaim;
            Claimed = claimed;
        }

        public int Id { get; }
        public int Progress { get; }
        public int Target { get; }
        public int RewardGold { get; }
        public bool CanClaim { get; }
        public bool Claimed { get; }
    }

    public readonly struct BumpMainQuestProgressCommand : ICommand
    {
        private readonly QuestSessionState _state;
        public int Delta { get; }

        public BumpMainQuestProgressCommand(QuestSessionState state, int delta)
        {
            _state = state;
            Delta = delta;
        }

        public void Execute() => _state.BumpMainProgress(Delta);
    }

    public readonly struct AdvanceMainQuestStepCommand : ICommand
    {
        private readonly QuestSessionState _state;
        private readonly QuestRewardWallet _wallet;

        public AdvanceMainQuestStepCommand(QuestSessionState state, QuestRewardWallet wallet)
        {
            _state = state;
            _wallet = wallet;
        }

        public void Execute()
        {
            _state.CompleteMainIfReady();
            _wallet.AddGold(20);
        }
    }

    public readonly struct ClaimSideQuestRewardCommand : ICommand
    {
        private readonly QuestSessionState _state;
        private readonly QuestRewardWallet _wallet;
        public int SideId { get; }

        public ClaimSideQuestRewardCommand(QuestSessionState state, QuestRewardWallet wallet, int sideId)
        {
            _state = state;
            _wallet = wallet;
            SideId = sideId;
        }

        public void Execute()
        {
            var row = _state.GetSideOrThrow(SideId);
            if (!row.CanClaim) throw new InvalidOperationException("Side quest not claimable.");
            row.RewardClaimed = true;
            _wallet.AddGold(row.RewardGold);
        }
    }

    public readonly struct ClaimDailyQuestRewardCommand : ICommand
    {
        private readonly QuestSessionState _state;
        private readonly QuestRewardWallet _wallet;
        public int DailyId { get; }

        public ClaimDailyQuestRewardCommand(QuestSessionState state, QuestRewardWallet wallet, int dailyId)
        {
            _state = state;
            _wallet = wallet;
            DailyId = dailyId;
        }

        public void Execute()
        {
            var row = _state.GetDailyOrThrow(DailyId);
            if (!row.CanClaim) throw new InvalidOperationException("Daily quest not claimable.");
            row.RewardClaimed = true;
            _wallet.AddGold(row.RewardGold);
        }
    }

    public readonly struct BumpSideQuestProgressCommand : ICommand
    {
        private readonly QuestSessionState _state;
        public int SideId { get; }
        public int Delta { get; }

        public BumpSideQuestProgressCommand(QuestSessionState state, int sideId, int delta)
        {
            _state = state;
            SideId = sideId;
            Delta = delta;
        }

        public void Execute()
        {
            var row = _state.GetSideOrThrow(SideId);
            if (row.RewardClaimed) throw new InvalidOperationException("Side quest already claimed.");
            row.Progress = Math.Min(row.Progress + Delta, row.Target);
        }
    }

    public readonly struct BumpDailyQuestProgressCommand : ICommand
    {
        private readonly QuestSessionState _state;
        public int DailyId { get; }
        public int Delta { get; }

        public BumpDailyQuestProgressCommand(QuestSessionState state, int dailyId, int delta)
        {
            _state = state;
            DailyId = dailyId;
            Delta = delta;
        }

        public void Execute()
        {
            var row = _state.GetDailyOrThrow(DailyId);
            if (row.RewardClaimed) throw new InvalidOperationException("Daily quest reward already claimed.");
            row.Progress = Math.Min(row.Progress + Delta, row.Target);
        }
    }
}
