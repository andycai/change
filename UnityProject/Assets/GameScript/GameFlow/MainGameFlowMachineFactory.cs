using Change.Framework.Fsm;
using GameScript.GameFlow.Orchestration;
using GameScript.GameFlow.States;
using System;

namespace GameScript.GameFlow
{
    public static class MainGameFlowMachineFactory
    {
        public static StateMachine<GameFlowStateId, GameFlowEvent> Create(GameFlowContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var machine = new StateMachine<GameFlowStateId, GameFlowEvent>();

            machine.Register(new BootState());
            machine.Register(new LoginState());
            machine.Register(new LobbyState());
            machine.Register(new MatchState());
            machine.Register(new BattleHostState(context));
            machine.Register(new ResultState());

            return machine;
        }
    }
}
