using Change.Framework.Cqrs;
using Change.Framework.Pooling;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class PoolingResetTests
    {
        private readonly struct PoolableCommand : ICommand
        {
            public int Value { get; }
            public PoolableCommand(int value) { Value = value; }
        }

        private sealed class PoolableCommandHandler
            : ICommandHandler<PoolableCommand>, IPoolable
        {
            public int HandleCount;
            public int ResetCount;

            public void Handle(in PoolableCommand command) { HandleCount++; }

            public void Reset() { ResetCount++; }
        }

        private readonly struct PlainCommand : ICommand
        {
            public int Value { get; }
            public PlainCommand(int value) { Value = value; }
        }

        private sealed class NonPoolableHandler : ICommandHandler<PlainCommand>
        {
            public int HandleCount;
            public void Handle(in PlainCommand command) { HandleCount++; }
        }

        [Test]
        public void Send_PoolableHandler_CallsResetAfterEachHandle()
        {
            var bus = new CqrsBus();
            var handler = new PoolableCommandHandler();
            bus.RegisterCommand(handler);

            bus.Send(new PoolableCommand(1));
            bus.Send(new PoolableCommand(2));

            Assert.AreEqual(2, handler.HandleCount);
            Assert.AreEqual(2, handler.ResetCount);
        }

        [Test]
        public void Send_NonPoolableHandler_DoesNotCallReset()
        {
            var bus = new CqrsBus();
            var handler = new NonPoolableHandler();
            bus.RegisterCommand(handler);

            bus.Send(new PlainCommand(1));

            Assert.AreEqual(1, handler.HandleCount);
        }
    }
}
