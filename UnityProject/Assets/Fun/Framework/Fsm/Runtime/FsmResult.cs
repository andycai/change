namespace Fun.Framework.Fsm
{
    public readonly struct FsmResult<TStateId>
    {
        private readonly TStateId _nextStateId;

        private FsmResult(bool isHandled, bool hasTransition, TStateId nextStateId)
        {
            IsHandled = isHandled;
            HasTransition = hasTransition;
            _nextStateId = nextStateId;
        }

        public bool IsHandled { get; }

        public bool HasTransition { get; }

        public TStateId NextStateId
        {
            get
            {
                return _nextStateId;
            }
        }

        public static FsmResult<TStateId> Handled()
        {
            return new FsmResult<TStateId>(true, false, default(TStateId));
        }

        public static FsmResult<TStateId> Ignored()
        {
            return new FsmResult<TStateId>(false, false, default(TStateId));
        }

        public static FsmResult<TStateId> TransitionTo(TStateId next)
        {
            return new FsmResult<TStateId>(true, true, next);
        }
    }
}
