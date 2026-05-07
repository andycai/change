using Change.Framework.Fsm;

namespace GameScript.GameFlow.BattleFlow.States
{
    public sealed class BattleReadyState : IFsmState<BattleFlowStateId, BattleFlowEvent>
    {
        public BattleFlowStateId Id => BattleFlowStateId.Ready;

        public void OnEnter(in StateChange<BattleFlowStateId, BattleFlowEvent> change) { }

        public void OnExit(in StateChange<BattleFlowStateId, BattleFlowEvent> change) { }

        public FsmResult<BattleFlowStateId> OnEvent(in BattleFlowEvent evt)
        {
            return evt == BattleFlowEvent.CountdownFinished
                ? FsmResult<BattleFlowStateId>.TransitionTo(BattleFlowStateId.Playing)
                : FsmResult<BattleFlowStateId>.Ignored();
        }
    }
}
