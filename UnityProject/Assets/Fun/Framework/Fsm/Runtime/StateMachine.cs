using System;
using System.Collections.Generic;

namespace Fun.Framework.Fsm
{
    public sealed class StateMachine<TStateId, TEvent>
    {
        private readonly Dictionary<TStateId, IFsmState<TStateId, TEvent>> _states;
        private IFsmState<TStateId, TEvent> _currentState;
        private long _sequence;

        public StateMachine(IEqualityComparer<TStateId> comparer = null)
        {
            _states = new Dictionary<TStateId, IFsmState<TStateId, TEvent>>(comparer ?? EqualityComparer<TStateId>.Default);
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
            _currentState.OnEvent(in evt);
        }

        public void ChangeState(TStateId next)
        {
            EnsureStarted();
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
