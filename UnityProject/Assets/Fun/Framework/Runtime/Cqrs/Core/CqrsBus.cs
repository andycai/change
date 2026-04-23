using System;
using System.Collections.Generic;

namespace Fun.Framework.Cqrs
{
    public sealed class CqrsBus : ICqrsBus, ICqrsRegistry
    {
        private readonly Dictionary<Type, object> _commandHandlers = new();
        private bool _isFrozen;

        public void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
            where TCommand : struct, ICommand
        {
            if (handler == null)
            {
                throw new InvalidOperationException("Command handler cannot be null.");
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

        public void Freeze()
        {
            _isFrozen = true;
        }

        public void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand
        {
            var commandType = typeof(TCommand);
            if (!_commandHandlers.TryGetValue(commandType, out var boxedHandler))
            {
                throw new InvalidOperationException($"Command handler not registered: {commandType.FullName}");
            }

            var handler = (ICommandHandler<TCommand>)boxedHandler;
            handler.Handle(in command);
        }
    }
}
