using Fun.Framework.Cqrs;
using NUnit.Framework;

namespace Fun.Framework.Tests
{
    public class RegistrationGuardTests
    {
        private readonly struct TestCommand : ICommand
        {
        }

        private readonly struct TestQuery : IQuery<int>
        {
        }

        private readonly struct TestEvent : IEvent
        {
        }

        private sealed class TestCommandHandler : ICommandHandler<TestCommand>
        {
            public void Handle(in TestCommand command)
            {
            }
        }

        private sealed class TestQueryHandler : IQueryHandler<TestQuery, int>
        {
            public int Handle(in TestQuery query)
            {
                return 1;
            }
        }

        private sealed class TestEventHandler : IEventHandler<TestEvent>
        {
            public void Handle(in TestEvent @event)
            {
            }
        }

        [Test]
        public void Send_WithoutHandler_ThrowsHandlerNotRegisteredException()
        {
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.Throws<HandlerNotRegisteredException>(() => bus.Send(new TestCommand()));
        }

        [Test]
        public void Query_WithoutHandler_ThrowsHandlerNotRegisteredException()
        {
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.Throws<HandlerNotRegisteredException>(() => bus.Query<TestQuery, int>(new TestQuery()));
        }

        [Test]
        public void RegisterCommand_Duplicate_ThrowsDuplicateRegistrationException()
        {
            var bus = new CqrsBus();
            bus.RegisterCommand(new TestCommandHandler());

            Assert.Throws<DuplicateRegistrationException>(() => bus.RegisterCommand(new TestCommandHandler()));
        }

        [Test]
        public void RegisterQuery_Duplicate_ThrowsDuplicateRegistrationException()
        {
            var bus = new CqrsBus();
            bus.RegisterQuery(new TestQueryHandler());

            Assert.Throws<DuplicateRegistrationException>(() => bus.RegisterQuery(new TestQueryHandler()));
        }

        [Test]
        public void RegisterOrSubscribe_AfterFreeze_ThrowsRegistryFrozenException()
        {
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.Throws<RegistryFrozenException>(() => bus.RegisterCommand(new TestCommandHandler()));
            Assert.Throws<RegistryFrozenException>(() => bus.RegisterQuery(new TestQueryHandler()));
            Assert.Throws<RegistryFrozenException>(() => bus.Subscribe(new TestEventHandler()));
        }
    }
}
