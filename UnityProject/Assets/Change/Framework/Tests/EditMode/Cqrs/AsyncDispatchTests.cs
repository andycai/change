using System.Threading.Tasks;
using Change.Framework.Cqrs;
using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class AsyncDispatchTests
    {
        private readonly struct AsyncCommand : ICommand
        {
            public int Value { get; }
            public AsyncCommand(int value) { Value = value; }
        }

        private sealed class AsyncCommandHandler
            : IAsyncCommandHandler<AsyncCommand>, IPoolable
        {
            public int ResetCount;
            public int Captured;

            public Task ExecuteAsync(AsyncCommand command)
            {
                Captured = command.Value;
                return Task.CompletedTask;
            }

            public void Reset() { ResetCount++; }
        }

        [Test]
        public void SendAsync_DispatchesHandler_AndResetsAfter()
        {
            var bus = new CqrsBus();
            var handler = new AsyncCommandHandler();
            bus.RegisterAsyncCommand(handler);

            bus.SendAsync(new AsyncCommand(11)).GetAwaiter().GetResult();

            Assert.AreEqual(11, handler.Captured);
            Assert.AreEqual(1, handler.ResetCount);
        }

        private readonly struct SelfHandlingAsyncCommand : ISelfHandlingCommand
        {
            public void Execute() { }
        }

        [Test]
        public void SendAsync_SelfHandlingCommand_ThrowsNotSupportedException()
        {
            var bus = new CqrsBus();

            Assert.Throws<System.NotSupportedException>(() =>
            {
                bus.SendAsync(new SelfHandlingAsyncCommand()).GetAwaiter().GetResult();
            });
        }
    }
}
