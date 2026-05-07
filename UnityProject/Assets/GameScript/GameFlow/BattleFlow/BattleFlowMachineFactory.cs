using Change.Framework.Fsm;
using GameScript.GameFlow.BattleFlow.States;

namespace GameScript.GameFlow.BattleFlow
{
    public static class BattleFlowMachineFactory
    {
        public static StateMachine<BattleFlowStateId, BattleFlowEvent> Create()
        {
            var machine = new StateMachine<BattleFlowStateId, BattleFlowEvent>();

            machine.Register(new BattleLoadingState());
            machine.Register(new BattleReadyState());
            machine.Register(new BattlePlayingState());
            machine.Register(new BattlePausedState());
            machine.Register(new BattleSettlementState());
            machine.Register(new BattleExitState());

            machine.Start(BattleFlowStateId.Loading);
            return machine;
        }
    }
}
