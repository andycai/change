namespace Fun.Framework.Fsm
{
    public readonly struct StateChange<TStateId, TEvent>
    {
        private StateChange(
            bool hasFrom,
            TStateId from,
            TStateId to,
            bool hasCauseEvent,
            TEvent causeEvent,
            long sequence)
        {
            HasFrom = hasFrom;
            From = from;
            To = to;
            HasCauseEvent = hasCauseEvent;
            CauseEvent = causeEvent;
            Sequence = sequence;
        }

        public bool HasFrom { get; }

        public TStateId From { get; }

        public TStateId To { get; }

        public bool HasCauseEvent { get; }

        public TEvent CauseEvent { get; }

        public long Sequence { get; }

        public static StateChange<TStateId, TEvent> Initial(TStateId to, long sequence)
        {
            return new StateChange<TStateId, TEvent>(false, default(TStateId), to, false, default(TEvent), sequence);
        }

        public static StateChange<TStateId, TEvent> Create(TStateId from, TStateId to, TEvent causeEvent, long sequence)
        {
            return new StateChange<TStateId, TEvent>(true, from, to, true, causeEvent, sequence);
        }

        public static StateChange<TStateId, TEvent> CreateWithoutEvent(TStateId from, TStateId to, long sequence)
        {
            return new StateChange<TStateId, TEvent>(true, from, to, false, default(TEvent), sequence);
        }
    }
}
