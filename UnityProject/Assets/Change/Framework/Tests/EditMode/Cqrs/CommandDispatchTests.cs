using System;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class CommandDispatchTests
    {
        private readonly struct IncrementCounterCommand : ICommand
        {
            private readonly CounterState _state;
            private readonly int _amount;

            public IncrementCounterCommand(CounterState state, int amount)
            {
                _state = state;
                _amount = amount;
            }

            public void Execute()
            {
                _state.Value += _amount;
            }
        }

        private sealed class CounterState
        {
            public int Value;
        }

        [Test]
        public void Send_ExecutesSelfHandlingCommand()
        {
            var state = new CounterState();
            var bus = new CqrsBus();

            bus.Send(new IncrementCounterCommand(state, 3));

            Assert.AreEqual(3, state.Value);
        }

        [Test]
        public void SelfHandling_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var counter = new CounterState();
            var bus = new CqrsBus();
            var command = new IncrementCounterCommand(counter, 1);

            for (var i = 0; i < 1000; i++) bus.Send(command);

            ForceFullGc();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100000; i++) bus.Send(command);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
            Assert.AreEqual(101000, counter.Value);
        }

        private static void ForceFullGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
