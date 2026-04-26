using System;

namespace Change.Framework.Fsm
{
    public readonly struct FsmResult<TStateId>
    {
        private readonly TStateId _nextStateId;

        private FsmResult(bool hasTransition, TStateId nextStateId)
        {
            HasTransition = hasTransition;
            _nextStateId = nextStateId;
        }

        public bool HasTransition { get; }

        public TStateId NextStateId
        {
            get
            {
                if (!HasTransition)
                {
                    throw new InvalidOperationException("Cannot access NextStateId when HasTransition is false.");
                }
                return _nextStateId;
            }
        }

        public static FsmResult<TStateId> Handled()
        {
            return new FsmResult<TStateId>(false, default(TStateId));
        }

        public static FsmResult<TStateId> Ignored()
        {
            return new FsmResult<TStateId>(false, default(TStateId));
        }

        public static FsmResult<TStateId> TransitionTo(TStateId next)
        {
            return new FsmResult<TStateId>(true, next);
        }
    }
}
