using Change.Framework.Fsm;

namespace GameScript.GameFlow.States
{
    public sealed class LobbyState : IFsmState<GameFlowStateId, GameFlowEvent>
    {
        public GameFlowStateId Id => GameFlowStateId.Lobby;

        public void OnEnter(in StateChange<GameFlowStateId, GameFlowEvent> change) { }

        public void OnExit(in StateChange<GameFlowStateId, GameFlowEvent> change) { }

        public FsmResult<GameFlowStateId> OnEvent(in GameFlowEvent evt)
        {
            return evt == GameFlowEvent.MatchRequested
                ? FsmResult<GameFlowStateId>.TransitionTo(GameFlowStateId.Match)
                : FsmResult<GameFlowStateId>.Ignored();
        }
    }
}
