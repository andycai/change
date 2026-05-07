using Change.Framework.Fsm;

namespace GameScript.GameFlow.BattleFlow.States
{
    public sealed class BattleExitState : IFsmState<BattleFlowStateId, BattleFlowEvent>
    {
        public BattleFlowStateId Id => BattleFlowStateId.Exit;

        public void OnEnter(in StateChange<BattleFlowStateId, BattleFlowEvent> change) { }

        public void OnExit(in StateChange<BattleFlowStateId, BattleFlowEvent> change) { }

        public FsmResult<BattleFlowStateId> OnEvent(in BattleFlowEvent evt)
        {
            return FsmResult<BattleFlowStateId>.Ignored();
        }
    }
}
