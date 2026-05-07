using Change.Framework.Fsm;

namespace GameScript.GameFlow.BattleFlow.States
{
    public sealed class BattlePlayingState : IFsmState<BattleFlowStateId, BattleFlowEvent>
    {
        public BattleFlowStateId Id => BattleFlowStateId.Playing;

        public void OnEnter(in StateChange<BattleFlowStateId, BattleFlowEvent> change) { }

        public void OnExit(in StateChange<BattleFlowStateId, BattleFlowEvent> change) { }

        public FsmResult<BattleFlowStateId> OnEvent(in BattleFlowEvent evt)
        {
            if (evt == BattleFlowEvent.PauseRequested)
            {
                return FsmResult<BattleFlowStateId>.TransitionTo(BattleFlowStateId.Paused);
            }

            if (evt == BattleFlowEvent.BattleTimeUp || evt == BattleFlowEvent.WinLoseResolved)
            {
                return FsmResult<BattleFlowStateId>.TransitionTo(BattleFlowStateId.Settlement);
            }

            return FsmResult<BattleFlowStateId>.Ignored();
        }
    }
}
