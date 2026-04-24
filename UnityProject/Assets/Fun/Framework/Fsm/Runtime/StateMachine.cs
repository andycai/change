using System;
using System.Collections.Generic;

namespace Fun.Framework.Fsm
{
    public sealed class StateMachine<TStateId, TEvent>
    {
        private readonly struct TransitionRequest
        {
            public TransitionRequest(TStateId nextStateId, TEvent causeEvent, bool hasCauseEvent)
            {
                NextStateId = nextStateId;
                CauseEvent = causeEvent;
                HasCauseEvent = hasCauseEvent;
            }

            public TStateId NextStateId { get; }

            public TEvent CauseEvent { get; }

            public bool HasCauseEvent { get; }
        }

        private readonly Dictionary<TStateId, IFsmState<TStateId, TEvent>> _states;
        private readonly Queue<TEvent> _eventQueue;
        private readonly Queue<TransitionRequest> _transitionQueue;
        private IFsmState<TStateId, TEvent> _currentState;
        private bool _isDrainingEvents;
        private bool _isDrainingTransitions;
        private long _sequence;

        public StateMachine(IEqualityComparer<TStateId> comparer = null)
        {
            _states = new Dictionary<TStateId, IFsmState<TStateId, TEvent>>(comparer ?? EqualityComparer<TStateId>.Default);
            _eventQueue = new Queue<TEvent>();
            _transitionQueue = new Queue<TransitionRequest>();
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
                        EnqueueTransition(result.NextStateId, currentEvent, true);
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
            EnqueueTransition(next, default(TEvent), false);
        }

        private void EnqueueTransition(TStateId next, TEvent causeEvent, bool hasCauseEvent)
        {
            _transitionQueue.Enqueue(new TransitionRequest(next, causeEvent, hasCauseEvent));
            if (_isDrainingTransitions)
            {
                return;
            }

            _isDrainingTransitions = true;
            try
            {
                while (_transitionQueue.Count > 0)
                {
                    var request = _transitionQueue.Dequeue();
                    ExecuteTransition(request);
                }
            }
            finally
            {
                _isDrainingTransitions = false;
            }
        }

        private void ExecuteTransition(TransitionRequest request)
        {
            if (!_states.TryGetValue(request.NextStateId, out var nextState))
            {
                throw new InvalidOperationException($"Target state '{request.NextStateId}' is not registered.");
            }

            if (EqualityComparer<TStateId>.Default.Equals(CurrentStateId, request.NextStateId))
            {
                return;
            }

            var fromState = _currentState;
            var fromId = CurrentStateId;
            var change = request.HasCauseEvent
                ? StateChange<TStateId, TEvent>.Create(fromId, request.NextStateId, request.CauseEvent, NextSequence())
                : StateChange<TStateId, TEvent>.CreateWithoutEvent(fromId, request.NextStateId, NextSequence());

            try
            {
                fromState.OnExit(in change);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"OnExit failed during transition '{fromId}' -> '{request.NextStateId}'.",
                    ex);
            }

            _currentState = nextState;
            CurrentStateId = request.NextStateId;

            try
            {
                nextState.OnEnter(in change);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"OnEnter failed during transition '{fromId}' -> '{request.NextStateId}'.",
                    ex);
            }

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
