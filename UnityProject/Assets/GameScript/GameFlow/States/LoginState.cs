using Change.Framework.Fsm;

namespace GameScript.GameFlow.States
{
    public sealed class LoginState : IFsmState<GameFlowStateId, GameFlowEvent>
    {
        public GameFlowStateId Id => GameFlowStateId.Login;

        public void OnEnter(in StateChange<GameFlowStateId, GameFlowEvent> change) { }

        public void OnExit(in StateChange<GameFlowStateId, GameFlowEvent> change) { }

        public FsmResult<GameFlowStateId> OnEvent(in GameFlowEvent evt)
        {
            return evt == GameFlowEvent.LoginSucceeded
                ? FsmResult<GameFlowStateId>.TransitionTo(GameFlowStateId.Lobby)
                : FsmResult<GameFlowStateId>.Ignored();
        }
    }
}
