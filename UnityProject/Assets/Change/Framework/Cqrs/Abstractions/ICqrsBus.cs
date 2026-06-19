using System.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    public interface ICqrsBus
    {
        void Send<TCommand>(in TCommand command)
            where TCommand : struct, ICommand;

        TResult Query<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>;

        TResult Ask<TQuery, TResult>(in TQuery query)
            where TQuery : struct, IQuery<TResult>;

        void Publish<TEvent>(in TEvent @event)
            where TEvent : struct, IEvent;

        Task SendAsync<TCommand>(TCommand command)
            where TCommand : struct, ICommand;

        Task<TResult> AskAsync<TQuery, TResult>(TQuery query)
            where TQuery : struct, IQuery<TResult>;
    }
}
