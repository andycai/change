using System;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class RuntimeRegistrationTests
    {
        private readonly struct RegTestCommand : ICommand
        {
            public RegTestCommand(int value) { Value = value; }
            public int Value { get; }
        }

        private readonly struct RegTestQuery : IQuery<int>
        {
            public RegTestQuery(int multiplier) { Multiplier = multiplier; }
            public int Multiplier { get; }
        }

        private readonly struct RegTestEvent : IEvent
        {
            public RegTestEvent(int delta) { Delta = delta; }
            public int Delta { get; }
        }

        private sealed class Counter { public int Value; }

        private sealed class RegTestCommandHandler : ICommandHandler<RegTestCommand>
        {
            private readonly Counter _c;
            public RegTestCommandHandler(Counter c) { _c = c; }
            public void Handle(in RegTestCommand cmd) { _c.Value += cmd.Value; }
        }

        private sealed class RegTestQueryHandler : IQueryHandler<RegTestQuery, int>
        {
            private readonly Counter _c;
            public RegTestQueryHandler(Counter c) { _c = c; }
            public int Handle(in RegTestQuery q) { return _c.Value * q.Multiplier; }
        }

        private sealed class RegTestEventHandler : IEventHandler<RegTestEvent>
        {
            private readonly Counter _c;
            public RegTestEventHandler(Counter c) { _c = c; }
            public void Handle(in RegTestEvent e) { _c.Value += e.Delta; }
        }

        // === Duplicate Registration Tests ===

        [Test]
        public void RegisterCommand_DuplicateRegistration_Throws()
        {
            var bus = new CqrsBus();
            bus.RegisterCommand(new RegTestCommandHandler(new Counter()));
            Assert.Throws<DuplicateRegistrationException>(
                () => bus.RegisterCommand(new RegTestCommandHandler(new Counter())));
        }

        [Test]
        public void RegisterQuery_DuplicateRegistration_Throws()
        {
            var bus = new CqrsBus();
            bus.RegisterQuery(new RegTestQueryHandler(new Counter()));
            Assert.Throws<DuplicateRegistrationException>(
                () => bus.RegisterQuery(new RegTestQueryHandler(new Counter())));
        }

        [Test]
        public void Subscribe_DuplicateSubscription_AppendsSuccessfully()
        {
            var bus = new CqrsBus();
            var handler = new RegTestEventHandler(new Counter());
            bus.Subscribe(handler);
            Assert.DoesNotThrow(() => bus.Subscribe(handler));
        }

        // === Unregister Command Tests ===

        [Test]
        public void UnregisterCommand_WhenRegistered_RemovesHandler()
        {
            var bus = new CqrsBus();
            var counter = new Counter();
            bus.RegisterCommand(new RegTestCommandHandler(counter));
            bus.UnregisterCommand<RegTestCommand>();
            Assert.Throws<HandlerNotRegisteredException>(
                () => bus.Send(new RegTestCommand(1)));
        }

        [Test]
        public void UnregisterCommand_WhenNotRegistered_Throws()
        {
            var bus = new CqrsBus();
            Assert.Throws<HandlerNotRegisteredException>(
                () => bus.UnregisterCommand<RegTestCommand>());
        }

        [Test]
        public void UnregisterCommand_ThenReregister_Works()
        {
            var bus = new CqrsBus();
            var counter = new Counter();
            bus.RegisterCommand(new RegTestCommandHandler(counter));
            bus.UnregisterCommand<RegTestCommand>();
            bus.RegisterCommand(new RegTestCommandHandler(counter));
            bus.Send(new RegTestCommand(5));
            Assert.AreEqual(5, counter.Value);
        }

        // === Unregister Query Tests ===

        [Test]
        public void UnregisterQuery_WhenRegistered_RemovesHandler()
        {
            var bus = new CqrsBus();
            var counter = new Counter { Value = 10 };
            bus.RegisterQuery(new RegTestQueryHandler(counter));
            bus.UnregisterQuery<RegTestQuery, int>();
            Assert.Throws<HandlerNotRegisteredException>(
                () => bus.Ask<RegTestQuery, int>(new RegTestQuery(2)));
        }

        [Test]
        public void UnregisterQuery_WhenNotRegistered_Throws()
        {
            var bus = new CqrsBus();
            Assert.Throws<HandlerNotRegisteredException>(
                () => bus.UnregisterQuery<RegTestQuery, int>());
        }

        // === Unsubscribe Tests ===

        [Test]
        public void Unsubscribe_RemovesSpecificHandler()
        {
            var bus = new CqrsBus();
            var counter1 = new Counter();
            var counter2 = new Counter();
            var h1 = new RegTestEventHandler(counter1);
            var h2 = new RegTestEventHandler(counter2);
            bus.Subscribe(h1);
            bus.Subscribe(h2);
            bus.Unsubscribe(h1);
            bus.Publish(new RegTestEvent(3));
            Assert.AreEqual(0, counter1.Value, "Unsubscribed handler should not receive event");
            Assert.AreEqual(3, counter2.Value);
        }

        [Test]
        public void Unsubscribe_WhenNotSubscribed_IsIdempotent()
        {
            var bus = new CqrsBus();
            var handler = new RegTestEventHandler(new Counter());
            Assert.DoesNotThrow(() => bus.Unsubscribe(handler));
        }

        [Test]
        public void Unsubscribe_WithNullHandler_ThrowsArgumentNullException()
        {
            var bus = new CqrsBus();
            Assert.Throws<ArgumentNullException>(() => bus.Unsubscribe<RegTestEvent>((IEventHandler<RegTestEvent>)null));
        }

        // === Full Lifecycle Tests ===

        [Test]
        public void RegisterSendUnregisterSend_ThrowsHandlerNotRegistered()
        {
            var bus = new CqrsBus();
            var counter = new Counter();
            bus.RegisterCommand(new RegTestCommandHandler(counter));
            bus.Send(new RegTestCommand(10));
            Assert.AreEqual(10, counter.Value);
            bus.UnregisterCommand<RegTestCommand>();
            Assert.Throws<HandlerNotRegisteredException>(
                () => bus.Send(new RegTestCommand(1)));
        }
    }
}
