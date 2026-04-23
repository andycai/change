using System;
using System.Collections.Generic;

namespace Fun.Framework.Cqrs
{
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry
    {
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
                    return ((QueryType != null ? QueryType.GetHashCode() : 0) * 397) ^
                           (ResultType != null ? ResultType.GetHashCode() : 0);
                }
            }
        }

        private readonly Dictionary<Type, object> _commandHandlers = new();
        private readonly Dictionary<QueryKey, object> _queryHandlers = new();
        private bool _isFrozen;

        public void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_isFrozen)
            {
                throw new InvalidOperationException("Registry is frozen.");
            }

            var commandType = typeof(TCommand);
            if (_commandHandlers.ContainsKey(commandType))
            {
                throw new InvalidOperationException($"Command handler already registered: {commandType.FullName}");
            }

            _commandHandlers[commandType] = handler;
        }

        public void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
            where TQuery : struct, IQuery<TResult>
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_isFrozen)
            {
                throw new InvalidOperationException("Registry is frozen.");
            }

            var queryType = typeof(TQuery);
            var resultType = typeof(TResult);
            var queryKey = new QueryKey(queryType, resultType);
            if (_queryHandlers.ContainsKey(queryKey))
            {
                throw new InvalidOperationException(
                    $"Query handler already registered: {queryType.FullName} -> {resultType.FullName}");
            }

            _queryHandlers[queryKey] = handler;
        }

        public void Freeze()
        {
            _isFrozen = true;
        }

        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            if (!_isFrozen)
            {
                throw new InvalidOperationException("Registry must be frozen before dispatch.");
            }

            var commandType = typeof(TCommand);
            if (!_commandHandlers.TryGetValue(commandType, out var boxedHandler))
            {
                throw new InvalidOperationException($"Command handler not registered: {commandType.FullName}");
            }

            var handler = (ICommandHandler<TCommand>)boxedHandler;
            handler.Handle(in command);
        }

        public TResult Query<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>
        {
            if (!_isFrozen)
            {
                throw new InvalidOperationException("Registry must be frozen before dispatch.");
            }

            var queryType = typeof(TQuery);
            var resultType = typeof(TResult);
            var queryKey = new QueryKey(queryType, resultType);
            if (!_queryHandlers.TryGetValue(queryKey, out var boxedHandler))
            {
                throw new InvalidOperationException(
                    $"Query handler not registered: {queryType.FullName} -> {resultType.FullName}");
            }

            var handler = (IQueryHandler<TQuery, TResult>)boxedHandler;
            return handler.Handle(in query);
        }
    }
}
