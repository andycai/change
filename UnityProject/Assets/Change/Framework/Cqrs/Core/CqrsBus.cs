using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Change.Framework.Logging;

namespace Change.Framework.Cqrs
{
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry
    {
        private const string CommandRegisteredMessage = "Registered command handler.";
        private const string QueryRegisteredMessage = "Registered query handler.";
        private const string EventSubscribedMessage = "Subscribed event handler.";

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

        private readonly Dictionary<Type, object> _commandHandlers = new();
        private readonly Dictionary<QueryKey, object> _queryHandlers = new();
        private readonly Dictionary<Type, object> _eventHandlers = new();
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
            if (_isFrozen)
            {
                throw new RegistryFrozenException("Registry is frozen.");
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var commandType = typeof(TCommand);
            if (_commandHandlers.ContainsKey(commandType))
            {
                throw new DuplicateRegistrationException($"Command handler already registered: {commandType.FullName}");
            }

            _commandHandlers[commandType] = handler;
            SafeInfo(CommandRegisteredMessage);
        }

        public void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            if (_isFrozen)
            {
                throw new RegistryFrozenException("Registry is frozen.");
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var queryType = typeof(TQuery);
            var resultType = typeof(TResult);
            var queryKey = new QueryKey(queryType, resultType);
            if (_queryHandlers.ContainsKey(queryKey))
            {
                throw new DuplicateRegistrationException(
                    $"Query handler already registered: {queryType.FullName} -> {resultType.FullName}");
            }

            _queryHandlers[queryKey] = handler;
            SafeInfo(QueryRegisteredMessage);
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
            where TEvent : struct, IEvent
        {
            if (_isFrozen)
            {
                throw new RegistryFrozenException("Registry is frozen.");
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var boxedHandlers))
            {
                boxedHandlers = new List<IEventHandler<TEvent>>();
                _eventHandlers[eventType] = boxedHandlers;
            }

            var handlers = (List<IEventHandler<TEvent>>)boxedHandlers;
            handlers.Add(handler);
            SafeInfo(EventSubscribedMessage);
        }

        public void Freeze()
        {
            _isFrozen = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            ThrowIfNotFrozen();

            var commandType = typeof(TCommand);
            if (!_commandHandlers.TryGetValue(commandType, out var boxedHandler))
            {
                throw new HandlerNotRegisteredException($"Command handler not registered: {commandType.FullName}");
            }

            var handler = (ICommandHandler<TCommand>)boxedHandler;
            handler.Handle(in command);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult Query<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            ThrowIfNotFrozen();

            var queryType = typeof(TQuery);
            var resultType = typeof(TResult);
            var queryKey = new QueryKey(queryType, resultType);
            if (!_queryHandlers.TryGetValue(queryKey, out var boxedHandler))
            {
                throw new HandlerNotRegisteredException(
                    $"Query handler not registered: {queryType.FullName} -> {resultType.FullName}");
            }

            var handler = (IQueryHandler<TQuery, TResult>)boxedHandler;
            return handler.Handle(in query);
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
            if (!_eventHandlers.TryGetValue(eventType, out var boxedHandlers))
            {
                return;
            }

            var handlers = (List<IEventHandler<TEvent>>)boxedHandlers;
            var count = handlers.Count;
            if (count == 0) return;

            List<Exception> exceptions = null;
            for (var i = 0; i < count; i++)
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

            if (exceptions != null)
            {
                throw new AggregateException(exceptions);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfNotFrozen()
        {
            if (!_isFrozen) throw new InvalidOperationException("Registry must be frozen before dispatch.");
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
