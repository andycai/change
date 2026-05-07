using Change.Framework.Fsm;

namespace GameScript.GameFlow.States
{
    public sealed class MatchState : IFsmState<GameFlowStateId, GameFlowEvent>
    {
        public GameFlowStateId Id => GameFlowStateId.Match;

        public void OnEnter(in StateChange<GameFlowStateId, GameFlowEvent> change) { }

        public void OnExit(in StateChange<GameFlowStateId, GameFlowEvent> change) { }

        public FsmResult<GameFlowStateId> OnEvent(in GameFlowEvent evt)
        {
            return evt == GameFlowEvent.MatchFound
                ? FsmResult<GameFlowStateId>.TransitionTo(GameFlowStateId.Battle)
                : FsmResult<GameFlowStateId>.Ignored();
        }
    }
}
