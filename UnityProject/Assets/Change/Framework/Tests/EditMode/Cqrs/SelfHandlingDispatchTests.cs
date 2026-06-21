using System;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class SelfHandlingDispatchTests
    {
        private sealed class Counter
        {
            public int Value;
        }

        private readonly struct BumpSelfHandlingCommand : ISelfHandlingCommand
        {
            private readonly Counter _counter;
            private readonly int _delta;

            public BumpSelfHandlingCommand(Counter counter, int delta)
            {
                _counter = counter;
                _delta = delta;
            }

            public void Execute()
            {
                _counter.Value += _delta;
            }
        }

        [Test]
        public void Send_SelfHandlingCommand_DispatchesExecuteWithoutRegistration()
        {
            var counter = new Counter();
            var bus = new CqrsBus();

            bus.Send(new BumpSelfHandlingCommand(counter, 5));

            Assert.AreEqual(5, counter.Value);
        }

        private readonly struct PlainCommand : ICommand
        {
            public int Value { get; }
            public PlainCommand(int value) { Value = value; }
        }

        private sealed class PlainCommandHandler : ICommandHandler<PlainCommand>
        {
            public int Received;
            public void Handle(in PlainCommand command) { Received = command.Value; }
        }

        [Test]
        public void Send_NonSelfHandlingCommand_StillUsesHandlerLookup()
        {
            var bus = new CqrsBus();
            var handler = new PlainCommandHandler();
            bus.RegisterCommand(handler);

            bus.Send(new PlainCommand(42));

            Assert.AreEqual(42, handler.Received);
        }

        private sealed class Source
        {
            public int Value;
        }

        private readonly struct ReadSelfHandlingQuery : ISelfHandlingQuery<int>
        {
            private readonly Source _source;

            public ReadSelfHandlingQuery(Source source) { _source = source; }

            public int Execute() => _source.Value;
        }

        [Test]
        public void Ask_SelfHandlingQuery_ReturnsExecuteResultWithoutRegistration()
        {
            var source = new Source { Value = 7 };
            var bus = new CqrsBus();

            var result = bus.Ask<ReadSelfHandlingQuery, int>(new ReadSelfHandlingQuery(source));

            Assert.AreEqual(7, result);
        }

        [Test]
        public void Query_SelfHandlingQuery_ReturnsExecuteResultWithoutRegistration()
        {
            var source = new Source { Value = 9 };
            var bus = new CqrsBus();

            var result = bus.Query<ReadSelfHandlingQuery, int>(new ReadSelfHandlingQuery(source));

            Assert.AreEqual(9, result);
        }

        private const int WarmupIterations = 1000;
        private const int MeasuredIterations = 100000;

        private static void ForceFullGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        [Test]
        public void Send_SelfHandlingHotPath_AllocatesZeroBytesAfterWarmup()
        {
            var counter = new Counter();
            var bus = new CqrsBus();
            var command = new BumpSelfHandlingCommand(counter, 1);

            for (var i = 0; i < WarmupIterations; i++)
            {
                bus.Send(command);
            }

            ForceFullGc();

            var before = GC.GetTotalMemory(true);
            for (var i = 0; i < MeasuredIterations; i++)
            {
                bus.Send(command);
            }
            var after = GC.GetTotalMemory(true);

            Assert.AreEqual(before, after);
            Assert.AreEqual(WarmupIterations + MeasuredIterations, counter.Value);
        }

        [Test]
        public void Ask_SelfHandlingHotPath_AllocatesZeroBytesAfterWarmup()
        {
            var source = new Source { Value = 3 };
            var bus = new CqrsBus();
            var query = new ReadSelfHandlingQuery(source);

            for (var i = 0; i < WarmupIterations; i++)
            {
                bus.Ask<ReadSelfHandlingQuery, int>(query);
            }

            ForceFullGc();

            var sum = 0;
            var before = GC.GetTotalMemory(true);
            for (var i = 0; i < MeasuredIterations; i++)
            {
                sum += bus.Ask<ReadSelfHandlingQuery, int>(query);
            }
            var after = GC.GetTotalMemory(true);

            Assert.AreEqual(before, after);
            Assert.AreEqual(3 * MeasuredIterations, sum);
        }
    }
}
