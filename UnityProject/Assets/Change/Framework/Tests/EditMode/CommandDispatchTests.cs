using System;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class CommandDispatchTests
    {
        private readonly struct IncrementCounterCommand : ICommand
        {
            public IncrementCounterCommand(int amount)
            {
                Amount = amount;
            }

            public int Amount { get; }
        }

        private sealed class CounterState
        {
            public int Value;
        }

        private sealed class IncrementCounterHandler : ICommandHandler<IncrementCounterCommand>
        {
            private readonly CounterState _state;

            public IncrementCounterHandler(CounterState state)
            {
                _state = state;
            }

            public void Handle(in IncrementCounterCommand command)
            {
                _state.Value += command.Amount;
            }
        }

        [Test]
        public void Send_DispatchesRegisteredHandler()
        {
            var state = new CounterState();
            var bus = new CqrsBus();

            bus.RegisterCommand(new IncrementCounterHandler(state));
            bus.Freeze();
            bus.Send(new IncrementCounterCommand(3));

            Assert.AreEqual(3, state.Value);
        }

        [Test]
        public void Send_WithoutRegistration_ThrowsHandlerNotRegisteredException()
        {
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.Throws<HandlerNotRegisteredException>(() => bus.Send(new IncrementCounterCommand(1)));
        }

        [Test]
        public void RegisterCommand_DuplicateRegistration_ThrowsDuplicateRegistrationException()
        {
            var state = new CounterState();
            var bus = new CqrsBus();

            bus.RegisterCommand(new IncrementCounterHandler(state));

            Assert.Throws<DuplicateRegistrationException>(() => bus.RegisterCommand(new IncrementCounterHandler(state)));
        }

        [Test]
        public void RegisterCommand_AfterFreeze_ThrowsRegistryFrozenException()
        {
            var state = new CounterState();
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.Throws<RegistryFrozenException>(() => bus.RegisterCommand(new IncrementCounterHandler(state)));
        }

        [Test]
        public void RegisterCommand_AfterFreeze_WithNullHandler_ThrowsRegistryFrozenException()
        {
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.Throws<RegistryFrozenException>(() => bus.RegisterCommand<IncrementCounterCommand>(null));
        }

        [Test]
        public void Send_BeforeFreeze_ThrowsInvalidOperationException()
        {
            var state = new CounterState();
            var bus = new CqrsBus();
            bus.RegisterCommand(new IncrementCounterHandler(state));

            Assert.Throws<InvalidOperationException>(() => bus.Send(new IncrementCounterCommand(1)));
        }

        [Test]
        public void RegisterCommand_NullHandler_ThrowsArgumentNullException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ArgumentNullException>(() => bus.RegisterCommand<IncrementCounterCommand>(null));
        }
    }
}
