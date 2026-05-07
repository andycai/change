using Change.Framework.Fsm;
using GameScript.GameFlow.BattleFlow;
using GameScript.GameFlow.Orchestration;
using System;

namespace GameScript.GameFlow.States
{
    public sealed class BattleHostState : IFsmState<GameFlowStateId, GameFlowEvent>
    {
        private readonly GameFlowContext _context;

        public BattleHostState(GameFlowContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public GameFlowStateId Id => GameFlowStateId.Battle;

        public void OnEnter(in StateChange<GameFlowStateId, GameFlowEvent> change)
        {
            var battleMachine = BattleFlowMachineFactory.Create();
            battleMachine.Start(BattleFlowStateId.Loading);

            _context.BattleMachine = battleMachine;
            _context.IsBattleActive = true;
        }

        public void OnExit(in StateChange<GameFlowStateId, GameFlowEvent> change)
        {
            _context.IsBattleActive = false;
            _context.BattleMachine = null;
        }

        public FsmResult<GameFlowStateId> OnEvent(in GameFlowEvent evt)
        {
            return evt == GameFlowEvent.BattleFinished
                ? FsmResult<GameFlowStateId>.TransitionTo(GameFlowStateId.Result)
                : FsmResult<GameFlowStateId>.Ignored();
        }
    }
}
