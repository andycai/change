namespace Change.Framework.Fsm
{
    public interface IFsmState<TStateId, TEvent>
        where TEvent : struct
    {
        TStateId Id { get; }

        void OnEnter(in StateChange<TStateId, TEvent> change);

        void OnExit(in StateChange<TStateId, TEvent> change);

        FsmResult<TStateId> OnEvent(in TEvent evt);
    }
}
