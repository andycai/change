using System.Threading.Tasks;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Handles a command asynchronously. Implement as a class. Only Class mode
    /// supports async dispatch; struct self-handling commands must use sync <c>Send</c>.
    /// </summary>
    public interface IAsyncCommandHandler<TCommand>
        where TCommand : struct, ICommand
    {
        Task ExecuteAsync(TCommand command);
    }
}
