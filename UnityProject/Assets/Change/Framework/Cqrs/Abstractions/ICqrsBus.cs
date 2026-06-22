using System;
using Cysharp.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    public interface ICqrsBus
    {
        void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand;

        void Send<TCommand>(Action<TCommand> configure)
            where TCommand : class, IPooledCommand, new();

        TResult Ask<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>;

        void Publish<TEvent>(in TEvent @event)
            where TEvent : struct, IEvent;

        UniTask SendAsync<TCommand>(TCommand command)
            where TCommand : struct, IAsyncCommand;

        UniTask<TResult> AskAsync<TQuery, TResult>(TQuery query)
            where TQuery : struct, IAsyncQuery<TResult>;
    }
}
