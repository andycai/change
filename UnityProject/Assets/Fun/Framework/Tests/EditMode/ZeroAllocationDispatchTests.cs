using System;
using Fun.Framework.Cqrs;
using NUnit.Framework;

namespace Fun.Framework.Tests
{
    public class ZeroAllocationDispatchTests
    {
        private readonly struct TickCommand : ICommand
        {
            public TickCommand(int delta)
            {
                Delta = delta;
            }

            public int Delta { get; }
        }

        private readonly struct GetTickQuery : IQuery<int>
        {
        }

        private readonly struct TickEvent : IEvent
        {
            public TickEvent(int delta)
            {
                Delta = delta;
            }

            public int Delta { get; }
        }

        private sealed class TickState
        {
            public int Value;
        }

        private sealed class TickCommandHandler : ICommandHandler<TickCommand>
        {
            private readonly TickState _state;

            public TickCommandHandler(TickState state)
            {
                _state = state;
            }

            public void Handle(in TickCommand command)
            {
                _state.Value += command.Delta;
            }
        }

        private sealed class TickQueryHandler : IQueryHandler<GetTickQuery, int>
        {
            private readonly TickState _state;

            public TickQueryHandler(TickState state)
            {
                _state = state;
            }

            public int Handle(in GetTickQuery query)
            {
                return _state.Value;
            }
        }

        private sealed class TickEventHandler : IEventHandler<TickEvent>
        {
            public int Count;

            public void Handle(in TickEvent @event)
            {
                Count += @event.Delta;
            }
        }

        [Test]
        public void Send_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var state = new TickState();
            var bus = new CqrsBus();
            bus.RegisterCommand(new TickCommandHandler(state));
            bus.Freeze();

            var command = new TickCommand(1);
            for (var i = 0; i < 1000; i++)
            {
                bus.Send(in command);
            }

            ForceFullGc();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100000; i++)
            {
                bus.Send(in command);
            }
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
        }

        [Test]
        public void Query_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var state = new TickState { Value = 7 };
            var bus = new CqrsBus();
            bus.RegisterQuery(new TickQueryHandler(state));
            bus.Freeze();

            var query = new GetTickQuery();
            for (var i = 0; i < 1000; i++)
            {
                bus.Query<GetTickQuery, int>(in query);
            }

            ForceFullGc();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100000; i++)
            {
                bus.Query<GetTickQuery, int>(in query);
            }
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
        }

        [Test]
        public void Publish_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var handler = new TickEventHandler();
            var bus = new CqrsBus();
            bus.Subscribe(handler);
            bus.Freeze();

            var @event = new TickEvent(1);
            for (var i = 0; i < 1000; i++)
            {
                bus.Publish(in @event);
            }

            ForceFullGc();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100000; i++)
            {
                bus.Publish(in @event);
            }
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
        }

        private static void ForceFullGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
