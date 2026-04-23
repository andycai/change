using Fun.Framework.Cqrs;
using NUnit.Framework;

namespace Fun.Framework.Tests
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
    }
}
