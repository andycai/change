using System;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class ZeroAllocationDispatchTests
    {
        private const int WarmupIterations = 1000;
        private const int MeasuredIterations = 100000;

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

        private readonly struct TickDomainEvent : IEvent
        {
            public TickDomainEvent(int delta)
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

        private sealed class TickDomainEventHandler : IEventHandler<TickDomainEvent>
        {
            public int Count;

            public void Handle(in TickDomainEvent domainEvent)
            {
                Count += domainEvent.Delta;
            }
        }

        private static int _sStaticTickCount;

        private static void StaticTickHandler(TickDomainEvent domainEvent)
        {
            _sStaticTickCount += domainEvent.Delta;
        }

        [Test]
        public void Send_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var state = new TickState();
            var bootstrap = new CqrsBootstrap();
            bootstrap.RegisterCommand(new TickCommandHandler(state));
            ICqrsRuntime runtime = bootstrap.Build();

            var command = new TickCommand(1);
            for (var i = 0; i < WarmupIterations; i++)
            {
                runtime.Send(in command);
            }

            ForceFullGc();

            var before = GC.GetTotalMemory(true);
            for (var i = 0; i < MeasuredIterations; i++)
            {
                runtime.Send(in command);
            }
            var after = GC.GetTotalMemory(true);

            Assert.AreEqual(before, after);
            Assert.AreEqual(WarmupIterations + MeasuredIterations, state.Value);
        }

        [Test]
        public void Query_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var state = new TickState { Value = 7 };
            var bootstrap = new CqrsBootstrap();
            bootstrap.RegisterQuery(new TickQueryHandler(state));
            ICqrsRuntime runtime = bootstrap.Build();

            var query = new GetTickQuery();
            for (var i = 0; i < WarmupIterations; i++)
            {
                runtime.Ask<GetTickQuery, int>(in query);
            }

            ForceFullGc();

            var before = GC.GetTotalMemory(true);
            var sum = 0;
            for (var i = 0; i < MeasuredIterations; i++)
            {
                sum += runtime.Ask<GetTickQuery, int>(in query);
            }
            var after = GC.GetTotalMemory(true);

            Assert.AreEqual(before, after);
            Assert.AreEqual(7 * MeasuredIterations, sum);
        }

        [Test]
        public void Publish_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var handler = new TickDomainEventHandler();
            var bootstrap = new CqrsBootstrap();
            bootstrap.Subscribe(handler);
            ICqrsRuntime runtime = bootstrap.Build();

            var domainEvent = new TickDomainEvent(1);
            for (var i = 0; i < WarmupIterations; i++)
            {
                runtime.Publish(in domainEvent);
            }

            ForceFullGc();

            var before = GC.GetTotalMemory(true);
            for (var i = 0; i < MeasuredIterations; i++)
            {
                runtime.Publish(in domainEvent);
            }
            var after = GC.GetTotalMemory(true);

            Assert.AreEqual(before, after);
            Assert.AreEqual(WarmupIterations + MeasuredIterations, handler.Count);
        }

        [Test]
        public void Publish_DelegateHotPath_AllocatesZeroBytesAfterWarmup()
        {
            _sStaticTickCount = 0;
            var bus = new CqrsBus();

            bus.Subscribe<TickDomainEvent>(StaticTickHandler);

            var domainEvent = new TickDomainEvent(1);
            for (var i = 0; i < WarmupIterations; i++)
            {
                bus.Publish(in domainEvent);
            }

            ForceFullGc();

            var before = GC.GetTotalMemory(true);
            for (var i = 0; i < MeasuredIterations; i++)
            {
                bus.Publish(in domainEvent);
            }
            var after = GC.GetTotalMemory(true);

            Assert.AreEqual(before, after,
                "delegate publish hot path must not allocate");
            Assert.AreEqual(WarmupIterations + MeasuredIterations, _sStaticTickCount);
        }

        private static void ForceFullGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
