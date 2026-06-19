using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Change.Framework.Collections;
using Change.Framework.Logging;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// In-process CQRS bus implementing registration, dispatch, and runtime surfaces.
    /// Supports runtime registration and unregistration.
    /// </summary>
    /// <remarks>
    /// <para><b>Thread safety:</b> Registration and unregistration methods
    /// (<see cref="RegisterCommand{TCommand}"/>, <see cref="UnregisterCommand{TCommand}"/>,
    /// etc.) must be called from the main thread only. The caller is responsible for
    /// ensuring thread safety — no internal locking is performed.</para>
    /// <para>Dispatch methods (<see cref="Send{TCommand}"/>, <see cref="Ask{TQuery, TResult}"/>,
    /// <see cref="Publish{TEvent}"/>) may be called from any thread after registration
    /// is complete, as the internal dictionaries are read-only during dispatch.</para>
    /// <para>Registration during an active dispatch on the same thread is safe
    /// (synchronous execution guarantees no interleaving).</para>
    /// </remarks>
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry, ICqrsRuntime
    {
        private const string CommandRegisteredMessage = "Registered command handler.";
        private const string QueryRegisteredMessage = "Registered query handler.";
        private const string EventSubscribedMessage = "Subscribed event handler.";
        private const string EventDelegateSubscribedMessage = "Subscribed event delegate.";

        private interface ICommandHandlerRegistration
        {
        }

        private interface IQueryHandlerRegistration
        {
        }

        private interface IEventHandlerList
        {
        }

        private sealed class CommandHandlerRegistration<TCommand> : ICommandHandlerRegistration
            where TCommand : struct, ICommand
        {
            public CommandHandlerRegistration(ICommandHandler<TCommand> handler)
            {
                Handler = handler;
            }

            public ICommandHandler<TCommand> Handler { get; }
        }

        private sealed class QueryHandlerRegistration<TQuery, TResult> : IQueryHandlerRegistration
            where TQuery : struct, IQuery<TResult>
        {
            public QueryHandlerRegistration(IQueryHandler<TQuery, TResult> handler)
            {
                Handler = handler;
            }

            public IQueryHandler<TQuery, TResult> Handler { get; }
        }

        private sealed class EventHandlerList<TEvent> : IEventHandlerList
            where TEvent : struct, IEvent
        {
            public FastList<IEventHandler<TEvent>> Handlers { get; } = new();
            public FastList<Action<TEvent>> Delegates { get; } = new();
        }

        private readonly struct QueryKey : IEquatable<QueryKey>
        {
            public QueryKey(Type queryType, Type resultType)
            {
                QueryType = queryType;
                ResultType = resultType;
            }

            public Type QueryType { get; }
            public Type ResultType { get; }

            public bool Equals(QueryKey other)
            {
                return QueryType == other.QueryType && ResultType == other.ResultType;
            }

            public override bool Equals(object obj)
            {
                return obj is QueryKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (QueryType.GetHashCode() * 397) ^ ResultType.GetHashCode();
                }
            }
        }

        private readonly FastDictionary<Type, ICommandHandlerRegistration> _commandHandlers = new();
        private readonly FastDictionary<QueryKey, IQueryHandlerRegistration> _queryHandlers = new();
        private readonly FastDictionary<Type, Type> _queryResultByQueryType = new();
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

        public void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            ThrowIfValueTypeHandler(handler, nameof(handler));

            var commandType = typeof(TCommand);
            if (!_commandHandlers.TryAdd(commandType, new CommandHandlerRegistration<TCommand>(handler)))
            {
                throw new DuplicateRegistrationException($"Command handler already registered: {commandType.FullName}");
            }

            SafeInfo(CommandRegisteredMessage);
        }

        public void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            ThrowIfValueTypeHandler(handler, nameof(handler));

            var queryType = typeof(TQuery);
            var resultType = typeof(TResult);

            if (_queryResultByQueryType.TryGetValue(queryType, out var existingResultType))
            {
                if (existingResultType == resultType)
                {
                    throw new DuplicateRegistrationException(
                        $"Query handler already registered: {queryType.FullName} -> {resultType.FullName}");
                }

                throw new DuplicateRegistrationException(
                    $"Query type already registered with a different result: {queryType.FullName} -> {existingResultType.FullName}; " +
                    $"each query supports exactly one handler.");
            }

            var queryKey = new QueryKey(queryType, resultType);
            _queryHandlers.TryAdd(queryKey, new QueryHandlerRegistration<TQuery, TResult>(handler));
            _queryResultByQueryType.TryAdd(queryType, resultType);

            SafeInfo(QueryRegisteredMessage);
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            ThrowIfValueTypeHandler(handler, nameof(handler));

            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                handlerList = new EventHandlerList<TEvent>();
                _eventHandlers.TryAdd(eventType, handlerList);
            }

            var typedList = (EventHandlerList<TEvent>)handlerList;
            typedList.Handlers.Add(handler);

            SafeInfo(EventSubscribedMessage);
        }

        public void Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            // 闭包检测：Target != null 表示委托捕获了实例或局部变量，会触发堆分配，破坏 0GC。
            if (handler.Target != null)
            {
                throw new ClosureCaptureException(
                    "Delegate captures variables; only static methods or non-capturing lambdas " +
                    "are allowed to maintain the 0-GC guarantee. " +
                    $"Delegate type: {handler.GetType().FullName}, Target: {handler.Target.GetType().FullName}.");
            }

            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                handlerList = new EventHandlerList<TEvent>();
                _eventHandlers.TryAdd(eventType, handlerList);
            }

            var typedList = (EventHandlerList<TEvent>)handlerList;
            typedList.Delegates.Add(handler);

            SafeInfo(EventDelegateSubscribedMessage);
        }

        public void UnregisterCommand<TCommand>()
            where TCommand : struct, ICommand
        {
            var commandType = typeof(TCommand);
            if (!_commandHandlers.Remove(commandType))
            {
                throw new HandlerNotRegisteredException($"Command handler not registered: {commandType.FullName}");
            }
        }

        public void UnregisterQuery<TQuery, TResult>()
            where TQuery : struct, IQuery<TResult>
        {
            var queryType = typeof(TQuery);
            var resultType = typeof(TResult);
            var queryKey = new QueryKey(queryType, resultType);

            if (!_queryHandlers.Remove(queryKey))
            {
                throw new HandlerNotRegisteredException(
                    $"Query handler not registered: {queryType.FullName} -> {resultType.FullName}");
            }

            _queryResultByQueryType.Remove(queryType);
        }

        public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

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
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

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

        private static class SelfHandlingCommandCache<TCommand>
            where TCommand : struct, ICommand
        {
            public static readonly Action<TCommand> Invoke = BuildInvoke();

            private static Action<TCommand> BuildInvoke()
            {
                if (!typeof(ISelfHandlingCommand).IsAssignableFrom(typeof(TCommand)))
                {
                    return null;
                }

                // 通过 DynamicMethod 发出 constrained.callvirt：对 struct this 的约束调用，
                // 零装箱、零 GC 分配。委托签名为 Action<TCommand>（按值传入 struct 副本，仅栈拷贝）；
                // IL 取参数地址后以 constrained. 前缀调用接口方法，等价于 box-free 虚分派。
                // （注：直接的 Delegate.CreateDelegate 在 Mono 上无法把 by-value Action<TCommand>
                // 绑定到值类型实例方法 —— 值类型实例方法的隐式 this 为 ref T，签名不兼容。）
                var executeMethod = typeof(ISelfHandlingCommand).GetMethod(
                    "Execute", BindingFlags.Public | BindingFlags.Instance);

                var dm = new DynamicMethod(
                    "SelfHandlingCommandInvoke_" + typeof(TCommand).Name,
                    returnType: null,
                    parameterTypes: new[] { typeof(TCommand) },
                    restrictedSkipVisibility: true);
                var il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarga_S, (byte)0);
                il.Emit(OpCodes.Constrained, typeof(TCommand));
                il.EmitCall(OpCodes.Callvirt, executeMethod, null);
                il.Emit(OpCodes.Ret);

                return (Action<TCommand>)dm.CreateDelegate(typeof(Action<TCommand>));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            var invoke = SelfHandlingCommandCache<TCommand>.Invoke;
            if (invoke != null)
            {
                invoke(command);
                return;
            }

            var commandType = typeof(TCommand);
            if (!_commandHandlers.TryGetValue(commandType, out var registration))
            {
                throw new HandlerNotRegisteredException($"Command handler not registered: {commandType.FullName}");
            }

            var typedRegistration = (CommandHandlerRegistration<TCommand>)registration;
            typedRegistration.Handler.Handle(in command);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult Query<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            var queryType = typeof(TQuery);
            var resultType = typeof(TResult);
            var queryKey = new QueryKey(queryType, resultType);
            if (!_queryHandlers.TryGetValue(queryKey, out var registration))
            {
                throw new HandlerNotRegisteredException(
                    $"Query handler not registered: {queryType.FullName} -> {resultType.FullName}");
            }

            var typedRegistration = (QueryHandlerRegistration<TQuery, TResult>)registration;
            return typedRegistration.Handler.Handle(in query);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult Ask<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            return Query<TQuery, TResult>(in query);
        }

        /// <summary>
        /// Publishes an event to all subscribed class handlers then to all subscribed
        /// delegates, each in registration order. Subscribers are matched strictly by the
        /// closed generic type <typeparamref name="TEvent"/>; no base-interface fan-out
        /// is performed. Class handlers are invoked before delegates. All subscribers
        /// are invoked even if one throws. If any throws, exceptions are collected and
        /// re-thrown as an <see cref="AggregateException"/>.
        /// </summary>
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
                    // Action<TEvent> 按值传递 struct（无 in），拷贝在栈上，无堆分配
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

        private static void ThrowIfValueTypeHandler(object handler, string paramName)
        {
            if (handler.GetType().IsValueType)
            {
                throw new InvalidOperationException(
                    $"Value-type handlers are not supported: {paramName} must be implemented by a class to avoid boxing.");
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
    }
}
