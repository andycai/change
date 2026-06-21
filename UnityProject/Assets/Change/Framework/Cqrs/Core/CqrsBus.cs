using System;
using System.Collections.Generic;
using Change.Framework.Collections;
using Change.Framework.Logging;
using Cysharp.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// In-process CQRS bus implementing dispatch, registration, and runtime surfaces.
    /// Command/Query handlers are not needed — all commands and queries are
    /// self-handling structs with intrinsic <c>Execute()</c>/<c>Query()</c> methods.
    /// Only events require registration (<see cref="Subscribe{TEvent}"/>).
    /// </summary>
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry, ICqrsRuntime
    {
        private interface IEventHandlerList { }

        private sealed class EventHandlerList<TEvent> : IEventHandlerList
            where TEvent : struct, IEvent
        {
            public FastList<IEventHandler<TEvent>> Handlers { get; } = new();
            public FastList<Action<TEvent>> Delegates { get; } = new();
        }

        private readonly FastDictionary<Type, IEventHandlerList> _eventHandlers = new();
        private readonly ILogger _logger;

        public CqrsBus()
            : this(NullLogger.Instance)
        {
        }

        public CqrsBus(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        // ===== Command Dispatch (self-handling) =====

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            // Stack copy avoids boxing when calling interface method on in-param value type.
            // The JIT emits a constrained callvirt on the local copy — zero allocation.
            var cmd = command;
            cmd.Execute();
        }

        // ===== Query Dispatch (self-handling) =====

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult Ask<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            var q = query;
            return q.Query();
        }

        // ===== Event Dispatch =====

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish<TEvent>(in TEvent @event)
            where TEvent : struct, IEvent
        {
            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                return;
            }

            var typedList = (EventHandlerList<TEvent>)handlerList;
            var handlers = typedList.Handlers;
            var delegates = typedList.Delegates;

            var handlerCount = handlers.Count;
            var delegateCount = delegates.Count;
            if (handlerCount == 0 && delegateCount == 0)
            {
                return;
            }

            List<Exception> exceptions = null;

            for (var i = 0; i < handlerCount; i++)
            {
                try
                {
                    handlers[i].Handle(in @event);
                }
                catch (Exception ex)
                {
                    exceptions ??= new List<Exception>();
                    exceptions.Add(ex);
                }
            }

            for (var i = 0; i < delegateCount; i++)
            {
                try
                {
                    delegates[i](@event);
                }
                catch (Exception ex)
                {
                    exceptions ??= new List<Exception>();
                    exceptions.Add(ex);
                }
            }

            if (exceptions != null)
            {
                throw new AggregateException(exceptions);
            }
        }

        // ===== Async Dispatch (self-handling) =====

        public async UniTask SendAsync<TCommand>(TCommand command)
            where TCommand : struct, IAsyncCommand
        {
            await command.ExecuteAsync();
        }

        public async UniTask<TResult> AskAsync<TQuery, TResult>(TQuery query)
            where TQuery : struct, IAsyncQuery<TResult>
        {
            return await query.QueryAsync();
        }

        // ===== Event Subscribe / Unsubscribe =====

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            ThrowIfValueTypeHandler(handler, nameof(handler));

            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                handlerList = new EventHandlerList<TEvent>();
                _eventHandlers.TryAdd(eventType, handlerList);
            }

            var typedList = (EventHandlerList<TEvent>)handlerList;
            typedList.Handlers.Add(handler);

            SafeInfo("Subscribed event handler.");
        }

        public void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            // Closure detection: Target != null means the delegate captures variables,
            // which would allocate a closure object (breaking the 0-GC guarantee).
            if (handler.Target != null)
            {
                throw new ClosureCaptureException(
                    "Delegate captures variables; only static methods or non-capturing lambdas "
                    + "are allowed to maintain the 0-GC guarantee. "
                    + $"Delegate type: {handler.GetType().FullName}, Target: {handler.Target.GetType().FullName}.");
            }

            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                handlerList = new EventHandlerList<TEvent>();
                _eventHandlers.TryAdd(eventType, handlerList);
            }

            var typedList = (EventHandlerList<TEvent>)handlerList;
            typedList.Delegates.Add(handler);

            SafeInfo("Subscribed event delegate.");
        }

        public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                return;
            }

            var typedList = (EventHandlerList<TEvent>)handlerList;
            var index = typedList.Handlers.IndexOf(handler);
            if (index >= 0)
            {
                typedList.Handlers.RemoveAt(index);
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                return;
            }

            var typedList = (EventHandlerList<TEvent>)handlerList;
            var index = typedList.Delegates.IndexOf(handler);
            if (index >= 0)
            {
                typedList.Delegates.RemoveAt(index);
            }
        }

        // ===== Helpers =====

        private static void ThrowIfValueTypeHandler(object handler, string paramName)
        {
            if (handler.GetType().IsValueType)
            {
                throw new InvalidOperationException(
                    $"Value-type handlers are not supported: {paramName} must be implemented by a class.");
            }
        }

        private void SafeInfo(string message)
        {
            try
            {
                _logger.Info(message);
            }
            catch (Exception ex)
            {
#if UNITY_EDITOR || DEBUG
                System.Diagnostics.Debug.WriteLine($"[CqrsBus] Logger error: {ex.Message}");
#endif
            }
        }

        private void ResetIfPoolable(object handler, Type messageType)
        {
            if (handler is IPoolable poolable)
            {
                try
                {
                    poolable.Reset();
                }
                catch (Exception ex)
                {
                    try
                    {
                        _logger.Error($"Reset() failed for {messageType.FullName}: {ex.Message}");
                    }
                    catch (Exception)
                    {
                        // 日志本身失败时静默，不影响命令执行。
                    }
                }
            }
        }
    }
}
