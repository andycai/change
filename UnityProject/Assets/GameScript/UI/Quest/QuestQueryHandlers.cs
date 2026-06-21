using System.Collections.Generic;
using Change.Framework.Cqrs;
using Change.Framework.Pooling;

namespace GameScript.UI.Quest
{
    public sealed class GetQuestPanelQueryHandler : IQueryHandler<GetQuestPanelQuery, QuestPanelSnapshot>, IPoolable
    {
        private readonly QuestSessionState _state;
        private readonly QuestRewardWallet _wallet;

        public GetQuestPanelQueryHandler(QuestSessionState state, QuestRewardWallet wallet)
        {
            _state = state;
            _wallet = wallet;
        }

        public QuestPanelSnapshot Handle(in GetQuestPanelQuery query)
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

        // Query Handler 不复用 buffer（snapshot 被 Presenter 持有，下次 Reset 清空会失效），
        // 故 Reset() 为契约占位空实现。
        public void Reset() { }
    }
}
