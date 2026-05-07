using Change.Framework.Fsm;

namespace GameScript.GameFlow.States
{
    public sealed class ResultState : IFsmState<GameFlowStateId, GameFlowEvent>
    {
        public GameFlowStateId Id => GameFlowStateId.Result;

        public void OnEnter(in StateChange<GameFlowStateId, GameFlowEvent> change) { }

        public void OnExit(in StateChange<GameFlowStateId, GameFlowEvent> change) { }

        public FsmResult<GameFlowStateId> OnEvent(in GameFlowEvent evt)
        {
            return evt == GameFlowEvent.ConfirmResult
                ? FsmResult<GameFlowStateId>.TransitionTo(GameFlowStateId.Lobby)
                : FsmResult<GameFlowStateId>.Ignored();
        }
    }
}
