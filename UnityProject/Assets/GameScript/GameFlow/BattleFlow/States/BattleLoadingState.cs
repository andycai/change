using Change.Framework.Fsm;

namespace GameScript.GameFlow.BattleFlow.States
{
    public sealed class BattleLoadingState : IFsmState<BattleFlowStateId, BattleFlowEvent>
    {
        public BattleFlowStateId Id => BattleFlowStateId.Loading;

        public void OnEnter(in StateChange<BattleFlowStateId, BattleFlowEvent> change) { }

        public void OnExit(in StateChange<BattleFlowStateId, BattleFlowEvent> change) { }

        public FsmResult<BattleFlowStateId> OnEvent(in BattleFlowEvent evt)
        {
            return evt == BattleFlowEvent.SceneLoaded
                ? FsmResult<BattleFlowStateId>.TransitionTo(BattleFlowStateId.Ready)
                : FsmResult<BattleFlowStateId>.Ignored();
        }
    }
}
