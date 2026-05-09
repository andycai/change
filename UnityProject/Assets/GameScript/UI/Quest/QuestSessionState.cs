using System;
using System.Collections.Generic;

namespace GameScript.UI.Quest
{
    public sealed class QuestSessionState
    {
        public int MainIndex { get; private set; }
        public int MainProgress { get; private set; }
        public int MainTarget { get; private set; }
        public bool MainCompleted { get; private set; }

        public readonly List<SideQuestRow> Sides = new();
        public readonly List<DailyQuestRow> Dailies = new();

        private static readonly int[] MainTargets = { 2, 2, 2 };

        public QuestSessionState()
        {
            Reset();
        }

        public void Reset()
        {
            MainIndex = 0;
            MainProgress = 0;
            MainTarget = MainTargets[0];
            MainCompleted = false;
            Sides.Clear();
            Sides.Add(new SideQuestRow(1, 3, 10));
            Sides.Add(new SideQuestRow(2, 3, 15));
            Dailies.Clear();
            Dailies.Add(new DailyQuestRow(1, 1, 5));
            Dailies.Add(new DailyQuestRow(2, 1, 5));
        }

        public void BumpMainProgress(int delta)
        {
            if (MainCompleted) throw new InvalidOperationException("Main quest already finished.");
            if (delta <= 0) throw new ArgumentOutOfRangeException(nameof(delta));
            MainProgress = Math.Min(MainProgress + delta, MainTarget);
        }

        public void CompleteMainIfReady()
        {
            if (MainCompleted) throw new InvalidOperationException("Main quest already finished.");
            if (MainProgress < MainTarget) throw new InvalidOperationException("Main quest progress insufficient.");
            if (MainIndex >= MainTargets.Length - 1)
            {
                MainCompleted = true;
                return;
            }
            MainIndex++;
            MainProgress = 0;
            MainTarget = MainTargets[MainIndex];
        }

        public SideQuestRow GetSideOrThrow(int id)
        {
            var row = Sides.Find(x => x.Id == id);
            if (row == null) throw new InvalidOperationException($"Unknown side quest {id}.");
            return row;
        }

        public DailyQuestRow GetDailyOrThrow(int id)
        {
            var row = Dailies.Find(x => x.Id == id);
            if (row == null) throw new InvalidOperationException($"Unknown daily quest {id}.");
            return row;
        }
    }

    public sealed class SideQuestRow
    {
        public SideQuestRow(int id, int target, int rewardGold)
        {
            Id = id;
            Target = target;
            RewardGold = rewardGold;
        }

        public int Id { get; }
        public int Target { get; }
        public int RewardGold { get; }
        public int Progress { get; set; }
        public bool RewardClaimed { get; set; }

        public bool CanClaim => !RewardClaimed && Progress >= Target;
    }

    public sealed class DailyQuestRow
    {
        public DailyQuestRow(int id, int target, int rewardGold)
        {
            Id = id;
            Target = target;
            RewardGold = rewardGold;
        }

        public int Id { get; }
        public int Target { get; }
        public int RewardGold { get; }
        public int Progress { get; set; }
        public bool RewardClaimed { get; set; }

        public bool CanClaim => !RewardClaimed && Progress >= Target;
    }
}
