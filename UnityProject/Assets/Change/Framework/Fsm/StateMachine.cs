using System;
using System.Collections.Generic;
using Change.Framework.Collections;

namespace Change.Framework.Fsm
{
    /// <summary>
    /// 事件驱动的状态机。支持重入、FIFO 事件处理和状态切换。
    /// 该类非线程安全，应在单线程（如 Unity 主线程）中使用。
    /// </summary>
    /// <typeparam name="TStateId">状态 ID 类型</typeparam>
    /// <typeparam name="TEvent">事件类型</typeparam>
    public sealed class StateMachine<TStateId, TEvent>
        where TEvent : struct
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
        private readonly FastDictionary<TStateId, IFsmState<TStateId, TEvent>> _states;
        private readonly RingBuffer<TEvent> _eventQueue;
        private readonly RingBuffer<TransitionRequest> _transitionQueue;
        private readonly bool _allowSelfTransition;
        
        private IFsmState<TStateId, TEvent> _currentState;
        private bool _isDrainingEvents;
        private bool _isDrainingTransitions;
        private bool _isFaulted;
        private int _stateCallbackDepth;
        private long _sequence;

        /// <summary>
        /// 创建状态机实例。
        /// </summary>
        /// <param name="comparer">状态 ID 比较器</param>
        /// <param name="initialCapacity">内部环形缓冲区初始容量。若超出此容量将抛出异常，需根据业务预估过程中的并发事件/切换数量。</param>
        /// <param name="allowSelfTransition">是否允许切换到当前相同状态（触发 OnExit -> OnEnter）</param>
        public StateMachine(IEqualityComparer<TStateId> comparer = null, int initialCapacity = 8, bool allowSelfTransition = false)
        {
            _stateIdComparer = comparer ?? EqualityComparer<TStateId>.Default;
            _states = new FastDictionary<TStateId, IFsmState<TStateId, TEvent>>(initialCapacity, _stateIdComparer);
            _eventQueue = new RingBuffer<TEvent>(initialCapacity);
            _transitionQueue = new RingBuffer<TransitionRequest>(initialCapacity);
            _allowSelfTransition = allowSelfTransition;
        }

        public event Action<StateChange<TStateId, TEvent>> OnStateChanged;

        public bool IsStarted { get; private set; }

        public bool IsFaulted => _isFaulted;

        public TStateId CurrentStateId { get; private set; }

        /// <summary>
        /// 注册状态。必须在 Start 之前调用。
        /// </summary>
        public void Register(IFsmState<TStateId, TEvent> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException("Cannot register states after the state machine has started.");
            }

            if (!_states.TryAdd(state.Id, state))
            {
                throw new InvalidOperationException($"State '{state.Id}' is already registered.");
            }
        }

        /// <summary>
        /// 启动状态机。
        /// </summary>
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

            _isFaulted = false;
            IsStarted = true;
            _currentState = initialState;
            CurrentStateId = initial;

            var change = StateChange<TStateId, TEvent>.Initial(initial, NextSequence());

            try
            {
                InvokeCallback(() => initialState.OnEnter(in change),
                    $"Failed to enter initial state '{initial}'. State machine has been reset and marked as faulted.");

                DrainTransitionsIfPossible();
                DrainEventsIfPossible();
            }
            catch
            {
                ResetToNotStarted();
                throw;
            }
        }

        /// <summary>
        /// 触发一个事件。如果当前正在处理回调，事件将被加入队列延迟处理。
        /// </summary>
        public void Fire(in TEvent evt)
        {
            EnsureStarted();

            _eventQueue.Enqueue(evt);
            DrainEventsIfPossible();
        }

        /// <summary>
        /// 手动请求状态切换。
        /// </summary>
        public void ChangeState(TStateId next)
        {
            EnsureStarted();
            EnqueueTransition(next, default(TEvent), false);
        }

        private void EnqueueTransition(TStateId next, TEvent causeEvent, bool hasCauseEvent)
        {
            if (!_allowSelfTransition && _stateIdComparer.Equals(CurrentStateId, next))
            {
                return;
            }

            _transitionQueue.Enqueue(new TransitionRequest(next, causeEvent, hasCauseEvent));
            DrainTransitionsIfPossible();
        }

        private void DrainTransitionsIfPossible()
        {
            if (_isDrainingTransitions || _stateCallbackDepth > 0)
            {
                return;
            }

            _isDrainingTransitions = true;
            try
            {
                while (_transitionQueue.Count > 0 && _transitionQueue.TryDequeue(out var request))
                {
                    ExecuteTransition(request);
                }
            }
            finally
            {
                _isDrainingTransitions = false;
                DrainEventsIfPossible();
            }
        }

        private void ExecuteTransition(TransitionRequest request)
        {
            // 再次检查自循环，因为队列中可能存在失效的请求
            if (!_allowSelfTransition && _stateIdComparer.Equals(CurrentStateId, request.NextStateId))
            {
                return;
            }

            if (!_states.TryGetValue(request.NextStateId, out var nextState))
            {
                throw new InvalidOperationException($"Target state '{request.NextStateId}' is not registered.");
            }

            var fromState = _currentState;
            var fromId = CurrentStateId;
            var change = request.HasCauseEvent
                ? StateChange<TStateId, TEvent>.Create(fromId, request.NextStateId, request.CauseEvent, NextSequence())
                : StateChange<TStateId, TEvent>.CreateWithoutEvent(fromId, request.NextStateId, NextSequence());

            // 1. 执行 Exit 回调
            InvokeCallback(() => fromState.OnExit(in change),
                $"OnExit failed during {FormatTransitionContext(in change)}. The state machine is now marked as faulted. Current state: '{fromId}'.");

            // 2. 更新当前状态
            _currentState = nextState;
            CurrentStateId = request.NextStateId;

            // 3. 执行 Enter 回调
            InvokeCallback(() => nextState.OnEnter(in change),
                $"OnEnter failed during {FormatTransitionContext(in change)}. The state machine is now marked as faulted. Current state: '{request.NextStateId}' (failed to initialize).");

            // 4. 触发通知
            InvokeCallback(() => OnStateChanged?.Invoke(change),
                "An error occurred in OnStateChanged event handler. The state machine is now marked as faulted.");
        }

        private void InvokeCallback(Action action, string errorMessage)
        {
            _stateCallbackDepth++;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _isFaulted = true;
                throw new InvalidOperationException(errorMessage, ex);
            }
            finally
            {
                _stateCallbackDepth--;
            }
        }

        private T InvokeCallback<T>(Func<T> func, string errorMessage)
        {
            _stateCallbackDepth++;
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                _isFaulted = true;
                throw new InvalidOperationException(errorMessage, ex);
            }
            finally
            {
                _stateCallbackDepth--;
            }
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
                while (_eventQueue.Count > 0 && _eventQueue.TryDequeue(out var currentEvent))
                {
                    var result = InvokeCallback(() => _currentState.OnEvent(in currentEvent),
                        $"OnEvent failed in state '{CurrentStateId}'. The state machine is now marked as faulted.");

                    // Flush transitions requested directly inside OnEvent before
                    // applying the returned transition result.
                    DrainTransitionsIfPossible();

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
            var cause = change.HasCauseEvent ? $"'{change.CauseEvent}'" : "<none>";
            return $"transition '{change.From}' -> '{change.To}' (sequence={change.Sequence}, cause={cause})";
        }

        private long NextSequence()
        {
            _sequence++;
            return _sequence;
        }

        private void ResetToNotStarted()
        {
            IsStarted = false;
            _currentState = default;
            CurrentStateId = default;
            _eventQueue.Clear(ClearMode.ZeroMemory);
            _transitionQueue.Clear(ClearMode.ZeroMemory);
            _isDrainingEvents = false;
            _isDrainingTransitions = false;
            _sequence = 0;
            // Note: _isFaulted is not reset here; a machine stays faulted until a new Start (if we allow it).
        }

        private void EnsureStarted()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("State machine is not started.");
            }

            if (_isFaulted)
            {
                throw new InvalidOperationException("State machine is in a faulted state due to a previous unhandled exception.");
            }
        }
    }
}
