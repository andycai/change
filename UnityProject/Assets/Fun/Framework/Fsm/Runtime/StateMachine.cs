using System;
using System.Collections.Generic;

namespace Fun.Framework.Fsm
{
    public sealed class StateMachine<TStateId, TEvent>
    {
        private readonly Dictionary<TStateId, IFsmState<TStateId, TEvent>> _states;
        private readonly Queue<TEvent> _eventQueue;
        private IFsmState<TStateId, TEvent> _currentState;
        private bool _isDrainingEvents;
        private long _sequence;

        public StateMachine(IEqualityComparer<TStateId> comparer = null)
        {
            _states = new Dictionary<TStateId, IFsmState<TStateId, TEvent>>(comparer ?? EqualityComparer<TStateId>.Default);
            _eventQueue = new Queue<TEvent>();
        }

        public event Action<StateChange<TStateId, TEvent>> OnStateChanged;

        public bool IsStarted { get; private set; }

        public TStateId CurrentStateId { get; private set; }

        public void Register(IFsmState<TStateId, TEvent> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (_states.ContainsKey(state.Id))
            {
                throw new InvalidOperationException($"State '{state.Id}' is already registered.");
            }

            _states.Add(state.Id, state);
        }

        public void Start(TStateId initial)
        {
            if (IsStarted)
            {
                throw new InvalidOperationException("State machine is already started.");
            }

            if (!_states.TryGetValue(initial, out var initialState))
            {
                throw new InvalidOperationException($"Initial state '{initial}' is not registered.");
            }

            IsStarted = true;
            _currentState = initialState;
            CurrentStateId = initial;

            var change = StateChange<TStateId, TEvent>.Initial(initial, NextSequence());
            initialState.OnEnter(in change);
        }

        public void Fire(in TEvent evt)
        {
            EnsureStarted();

            _eventQueue.Enqueue(evt);
            if (_isDrainingEvents)
            {
                return;
            }

            _isDrainingEvents = true;
            try
            {
                while (_eventQueue.Count > 0)
                {
                    var currentEvent = _eventQueue.Dequeue();
                    var result = _currentState.OnEvent(in currentEvent);
                    if (result.HasTransition)
                    {
                        ChangeStateInternal(result.NextStateId, currentEvent, true);
                    }
                }
            }
            finally
            {
                _isDrainingEvents = false;
            }
        }

        public void ChangeState(TStateId next)
        {
            EnsureStarted();
            ChangeStateInternal(next, default(TEvent), false);
        }

        private void ChangeStateInternal(TStateId next, TEvent causeEvent, bool hasCauseEvent)
        {
            if (!_states.TryGetValue(next, out var nextState))
            {
                throw new InvalidOperationException($"Target state '{next}' is not registered.");
            }

            if (EqualityComparer<TStateId>.Default.Equals(CurrentStateId, next))
            {
                return;
            }

            var fromState = _currentState;
            var fromId = CurrentStateId;
            var change = hasCauseEvent
                ? StateChange<TStateId, TEvent>.Create(fromId, next, causeEvent, NextSequence())
                : StateChange<TStateId, TEvent>.CreateWithoutEvent(fromId, next, NextSequence());

            fromState.OnExit(in change);
            _currentState = nextState;
            CurrentStateId = next;
            nextState.OnEnter(in change);
            OnStateChanged?.Invoke(change);
        }

        private long NextSequence()
        {
            _sequence++;
            return _sequence;
        }

        private void EnsureStarted()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("State machine is not started.");
            }
        }
    }
}
