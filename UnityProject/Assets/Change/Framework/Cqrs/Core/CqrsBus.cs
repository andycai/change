using System;
using System.Runtime.CompilerServices;
using Change.Framework.Collections;
using Change.Framework.Logging;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// In-process CQRS bus with explicit registration and freeze lifecycle.
    /// Handlers may be registered from multiple threads before <see cref="Freeze"/>.
    /// Dispatch is allowed only after <see cref="Freeze"/>.
    /// </summary>
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry
    {
        private const string CommandRegisteredMessage = "Registered command handler.";
        private const string QueryRegisteredMessage = "Registered query handler.";
        private const string EventSubscribedMessage = "Subscribed event handler.";

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
        private readonly FastDictionary<Type, IEventHandlerList> _eventHandlers = new();
        private readonly object _registrationGate = new();
        private readonly ILogger _logger;
        private volatile bool _isFrozen;

        /// <summary>
        /// Gets whether the registry is frozen and ready for dispatch.
        /// </summary>
        public bool IsFrozen => _isFrozen;

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
            lock (_registrationGate)
            {
                if (_isFrozen)
                {
                    throw new RegistryFrozenException("Registry is frozen.");
                }
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            ThrowIfValueTypeHandler(handler, nameof(handler));

            lock (_registrationGate)
            {
                if (_isFrozen)
                {
                    throw new RegistryFrozenException("Registry is frozen.");
                }

                var commandType = typeof(TCommand);
                if (!_commandHandlers.TryAdd(commandType, new CommandHandlerRegistration<TCommand>(handler)))
                {
                    throw new DuplicateRegistrationException($"Command handler already registered: {commandType.FullName}");
                }
            }

            SafeInfo(CommandRegisteredMessage);
        }

        public void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            lock (_registrationGate)
            {
                if (_isFrozen)
                {
                    throw new RegistryFrozenException("Registry is frozen.");
                }
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            ThrowIfValueTypeHandler(handler, nameof(handler));

            lock (_registrationGate)
            {
                if (_isFrozen)
                {
                    throw new RegistryFrozenException("Registry is frozen.");
                }

                var queryType = typeof(TQuery);
                var resultType = typeof(TResult);
                var queryKey = new QueryKey(queryType, resultType);
                if (!_queryHandlers.TryAdd(queryKey, new QueryHandlerRegistration<TQuery, TResult>(handler)))
                {
                    throw new DuplicateRegistrationException(
                        $"Query handler already registered: {queryType.FullName} -> {resultType.FullName}");
                }
            }

            SafeInfo(QueryRegisteredMessage);
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            lock (_registrationGate)
            {
                if (_isFrozen)
                {
                    throw new RegistryFrozenException("Registry is frozen.");
                }
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            ThrowIfValueTypeHandler(handler, nameof(handler));

            lock (_registrationGate)
            {
                if (_isFrozen)
                {
                    throw new RegistryFrozenException("Registry is frozen.");
                }

                var eventType = typeof(TEvent);
                if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
                {
                    var created = new EventHandlerList<TEvent>();
                    if (!_eventHandlers.TryAdd(eventType, created))
                    {
                        _eventHandlers.TryGetValue(eventType, out handlerList);
                    }
                    else
                    {
                        handlerList = created;
                    }
                }

                var typedList = (EventHandlerList<TEvent>)handlerList;
                typedList.Handlers.Add(handler);
            }
            SafeInfo(EventSubscribedMessage);
        }

        public void Freeze()
        {
            lock (_registrationGate)
            {
                _isFrozen = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            ThrowIfNotFrozen();

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
            ThrowIfNotFrozen();

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
        /// Publishes an event to all subscribed handlers in registration order.
        /// All handlers are invoked even if one throws. If any handler throws,
        /// exceptions are collected and re-thrown as an <see cref="AggregateException"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Publish<TEvent>(in TEvent @event)
            where TEvent : struct, IEvent
        {
            ThrowIfNotFrozen();

            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlerList))
            {
                return;
            }

            var typedList = (EventHandlerList<TEvent>)handlerList;
            var handlers = typedList.Handlers;
            var count = handlers.Count;
            if (count == 0) return;

            FastList<Exception> exceptions = null;
            for (var i = 0; i < count; i++)
            {
                try
                {
                    handlers[i].Handle(in @event);
                }
                catch (Exception ex)
                {
                    exceptions ??= new FastList<Exception>();
                    exceptions.Add(ex);
                }
            }

            if (exceptions != null)
            {
                var innerExceptions = new Exception[exceptions.Count];
                for (var i = 0; i < exceptions.Count; i++)
                {
                    innerExceptions[i] = exceptions[i];
                }

                throw new AggregateException(innerExceptions);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfNotFrozen()
        {
            if (!_isFrozen) throw new InvalidOperationException("Registry must be frozen before dispatch.");
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
