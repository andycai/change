using Change.Framework.Fsm;
using GameScript.GameFlow.BattleFlow;

namespace GameScript.GameFlow.Orchestration
{
    public sealed class GameFlowContext
    {
        public int PlayerId { get; set; }

        public int MatchId { get; set; }

        public bool IsBattleActive { get; set; }

        public StateMachine<BattleFlowStateId, BattleFlowEvent> BattleMachine { get; set; }
    }
}
