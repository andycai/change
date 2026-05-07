using Change.Framework.Fsm;

namespace GameScript.GameFlow.States
{
    public sealed class BootState : IFsmState<GameFlowStateId, GameFlowEvent>
    {
        public GameFlowStateId Id => GameFlowStateId.Boot;

        public void OnEnter(in StateChange<GameFlowStateId, GameFlowEvent> change) { }

        public void OnExit(in StateChange<GameFlowStateId, GameFlowEvent> change) { }

        public FsmResult<GameFlowStateId> OnEvent(in GameFlowEvent evt)
        {
            return evt == GameFlowEvent.BootstrapCompleted
                ? FsmResult<GameFlowStateId>.TransitionTo(GameFlowStateId.Login)
                : FsmResult<GameFlowStateId>.Ignored();
        }
    }
}
