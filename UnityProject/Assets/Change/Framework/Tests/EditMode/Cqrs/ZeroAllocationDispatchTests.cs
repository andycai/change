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
            private readonly TickState _state;
            private readonly int _delta;

            public TickCommand(TickState state, int delta)
            {
                _state = state;
                _delta = delta;
            }

            public void Execute() { _state.Value += _delta; }
        }

        private readonly struct GetTickQuery : IQuery<int>
        {
            private readonly TickState _state;

            public GetTickQuery(TickState state) { _state = state; }

            public int Query() => _state.Value;
        }

        private readonly struct TickDomainEvent : IEvent
        {
            public int Delta { get; }
            public TickDomainEvent(int delta) { Delta = delta; }
        }

        private sealed class TickState { public int Value; }

        private sealed class TickDomainEventHandler : IEventHandler<TickDomainEvent>
        {
            public int Count;
            public void Handle(in TickDomainEvent e) { Count += e.Delta; }
        }

        private static int _sStaticTickCount;
        private static void StaticTickHandler(TickDomainEvent e) { _sStaticTickCount += e.Delta; }

        [Test]
        public void Send_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var state = new TickState();
            var bus = new CqrsBus();
            var command = new TickCommand(state, 1);

            for (var i = 0; i < WarmupIterations; i++) bus.Send(command);
            ForceFullGc();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < MeasuredIterations; i++) bus.Send(command);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
            Assert.AreEqual(WarmupIterations + MeasuredIterations, state.Value);
        }

        [Test]
        public void Query_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var state = new TickState { Value = 7 };
            var bus = new CqrsBus();
            var query = new GetTickQuery(state);

            for (var i = 0; i < WarmupIterations; i++) bus.Ask<GetTickQuery, int>(query);
            ForceFullGc();

            var before = GC.GetAllocatedBytesForCurrentThread();
            var sum = 0;
            for (var i = 0; i < MeasuredIterations; i++) sum += bus.Ask<GetTickQuery, int>(query);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after);
            Assert.AreEqual(7 * MeasuredIterations, sum);
        }

        [Test]
        public void Publish_HotPath_AllocatesZeroBytesAfterWarmup()
        {
            var handler = new TickDomainEventHandler();
            var bus = new CqrsBus();
            bus.Subscribe(handler);

            var domainEvent = new TickDomainEvent(1);
            for (var i = 0; i < WarmupIterations; i++) bus.Publish(domainEvent);
            ForceFullGc();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < MeasuredIterations; i++) bus.Publish(domainEvent);
            var after = GC.GetAllocatedBytesForCurrentThread();

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
            for (var i = 0; i < WarmupIterations; i++) bus.Publish(domainEvent);
            ForceFullGc();

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < MeasuredIterations; i++) bus.Publish(domainEvent);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after, "delegate publish hot path must not allocate");
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
