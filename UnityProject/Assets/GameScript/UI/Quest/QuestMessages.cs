using System.Collections.Generic;
using Change.Framework.Cqrs;

namespace GameScript.UI.Quest
{
    public readonly struct GetQuestPanelQuery : IQuery<QuestPanelSnapshot>
    {
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
        public BumpMainQuestProgressCommand(int delta)
        {
            Delta = delta;
        }

        public int Delta { get; }
    }

    public readonly struct AdvanceMainQuestStepCommand : ICommand
    {
    }

    public readonly struct ClaimSideQuestRewardCommand : ICommand
    {
        public ClaimSideQuestRewardCommand(int sideId)
        {
            SideId = sideId;
        }

        public int SideId { get; }
    }

    public readonly struct ClaimDailyQuestRewardCommand : ICommand
    {
        public ClaimDailyQuestRewardCommand(int dailyId)
        {
            DailyId = dailyId;
        }

        public int DailyId { get; }
    }

    public readonly struct BumpSideQuestProgressCommand : ICommand
    {
        public BumpSideQuestProgressCommand(int sideId, int delta)
        {
            SideId = sideId;
            Delta = delta;
        }

        public int SideId { get; }
        public int Delta { get; }
    }

    public readonly struct BumpDailyQuestProgressCommand : ICommand
    {
        public BumpDailyQuestProgressCommand(int dailyId, int delta)
        {
            DailyId = dailyId;
            Delta = delta;
        }

        public int DailyId { get; }
        public int Delta { get; }
    }
}
