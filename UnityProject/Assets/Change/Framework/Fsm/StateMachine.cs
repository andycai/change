using System;
using System.Collections.Generic;

namespace Change.Framework.Fsm
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

        private readonly IEqualityComparer<TStateId> _stateIdComparer;
        private readonly Dictionary<TStateId, IFsmState<TStateId, TEvent>> _states;
        private readonly Queue<TEvent> _eventQueue;
        private readonly Queue<TransitionRequest> _transitionQueue;
        private IFsmState<TStateId, TEvent> _currentState;
        private bool _isDrainingEvents;
        private bool _isDrainingTransitions;
        private int _stateCallbackDepth;
        private long _sequence;

        public StateMachine(IEqualityComparer<TStateId> comparer = null)
        {
            _stateIdComparer = comparer ?? EqualityComparer<TStateId>.Default;
            _states = new Dictionary<TStateId, IFsmState<TStateId, TEvent>>(_stateIdComparer);
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
            var onEnterSucceeded = false;
            _stateCallbackDepth++;
            try
            {
                initialState.OnEnter(in change);
                onEnterSucceeded = true;
            }
            finally
            {
                _stateCallbackDepth--;
                if (onEnterSucceeded)
                {
                    DrainEventsIfPossible();
                }
            }
        }

        public void Fire(in TEvent evt)
        {
            EnsureStarted();

            _eventQueue.Enqueue(evt);
            DrainEventsIfPossible();
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
            var transitionDrainSucceeded = false;
            try
            {
                while (_transitionQueue.Count > 0)
                {
                    var request = _transitionQueue.Dequeue();
                    ExecuteTransition(request);
                }

                transitionDrainSucceeded = true;
            }
            finally
            {
                _isDrainingTransitions = false;
                if (transitionDrainSucceeded)
                {
                    DrainEventsIfPossible();
                }
            }
        }

        private void ExecuteTransition(TransitionRequest request)
        {
            if (!_states.TryGetValue(request.NextStateId, out var nextState))
            {
                throw new InvalidOperationException($"Target state '{request.NextStateId}' is not registered.");
            }

            if (_stateIdComparer.Equals(CurrentStateId, request.NextStateId))
            {
                return;
            }

            var fromState = _currentState;
            var fromId = CurrentStateId;
            var change = request.HasCauseEvent
                ? StateChange<TStateId, TEvent>.Create(fromId, request.NextStateId, request.CauseEvent, NextSequence())
                : StateChange<TStateId, TEvent>.CreateWithoutEvent(fromId, request.NextStateId, NextSequence());

            var onExitSucceeded = false;
            try
            {
                _stateCallbackDepth++;
                fromState.OnExit(in change);
                onExitSucceeded = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"OnExit failed during {FormatTransitionContext(in change)}.",
                    ex);
            }
            finally
            {
                _stateCallbackDepth--;
                if (onExitSucceeded)
                {
                    DrainEventsIfPossible();
                }
            }

            _currentState = nextState;
            CurrentStateId = request.NextStateId;

            var onEnterSucceeded = false;
            try
            {
                _stateCallbackDepth++;
                nextState.OnEnter(in change);
                onEnterSucceeded = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"OnEnter failed during {FormatTransitionContext(in change)}.",
                    ex);
            }
            finally
            {
                _stateCallbackDepth--;
                if (onEnterSucceeded)
                {
                    DrainEventsIfPossible();
                }
            }

            OnStateChanged?.Invoke(change);
        }

        private void DrainEventsIfPossible()
        {
            if (_isDrainingEvents || _isDrainingTransitions || _stateCallbackDepth > 0)
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

        private static string FormatTransitionContext(in StateChange<TStateId, TEvent> change)
        {
            return change.HasCauseEvent
                ? $"transition '{change.From}' -> '{change.To}' (sequence={change.Sequence}, cause='{change.CauseEvent}')"
                : $"transition '{change.From}' -> '{change.To}' (sequence={change.Sequence}, cause=<none>)";
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
