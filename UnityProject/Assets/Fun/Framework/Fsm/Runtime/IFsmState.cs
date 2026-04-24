namespace Fun.Framework.Fsm
{
    public interface IFsmState<TStateId, TEvent>
    {
        TStateId Id { get; }

        void OnEnter(in StateChange<TStateId, TEvent> change);

        void OnExit(in StateChange<TStateId, TEvent> change);

        FsmResult<TStateId> OnEvent(in TEvent evt);
    }
}
