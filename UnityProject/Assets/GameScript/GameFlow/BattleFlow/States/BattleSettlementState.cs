using Change.Framework.Fsm;

namespace GameScript.GameFlow.BattleFlow.States
{
    public sealed class BattleSettlementState : IFsmState<BattleFlowStateId, BattleFlowEvent>
    {
        public BattleFlowStateId Id => BattleFlowStateId.Settlement;

        public void OnEnter(in StateChange<BattleFlowStateId, BattleFlowEvent> change) { }

        public void OnExit(in StateChange<BattleFlowStateId, BattleFlowEvent> change) { }

        public FsmResult<BattleFlowStateId> OnEvent(in BattleFlowEvent evt)
        {
            return evt == BattleFlowEvent.SettlementConfirmed
                ? FsmResult<BattleFlowStateId>.TransitionTo(BattleFlowStateId.Exit)
                : FsmResult<BattleFlowStateId>.Ignored();
        }
    }
}
