using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class CqrsBootstrapLifecycleTests
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
            private readonly Counter _counter;

            public TestCommandHandler(Counter counter)
            {
                _counter = counter;
            }

            public void Handle(in TestCommand command)
            {
                _counter.Value++;
            }
        }

        private sealed class TestQueryHandler : IQueryHandler<TestQuery, int>
        {
            public int Handle(in TestQuery query)
            {
                return 42;
            }
        }

        private sealed class TestEventHandler : IEventHandler<TestEvent>
        {
            private readonly Counter _counter;

            public TestEventHandler(Counter counter)
            {
                _counter = counter;
            }

            public void Handle(in TestEvent @event)
            {
                _counter.Value++;
            }
        }

        private sealed class Counter
        {
            public int Value;
        }

        [Test]
        public void Build_ReturnsRuntime_ThatCanDispatchCommand()
        {
            var bootstrap = new CqrsBootstrap();
            var counter = new Counter();
            bootstrap.RegisterCommand(new TestCommandHandler(counter));

            var runtime = bootstrap.Build();
            runtime.Send(new TestCommand());

            Assert.AreEqual(1, counter.Value);
        }

        [Test]
        public void RegisterCommand_AfterBuild_ThrowsRegistryFrozenException()
        {
            var bootstrap = new CqrsBootstrap();
            bootstrap.Build();

            Assert.Throws<RegistryFrozenException>(() => bootstrap.RegisterCommand(new TestCommandHandler(new Counter())));
        }

        [Test]
        public void Runtime_SupportsQueryAndPublish_HappyPath()
        {
            var bootstrap = new CqrsBootstrap();
            var counter = new Counter();
            bootstrap.RegisterQuery(new TestQueryHandler());
            bootstrap.Subscribe(new TestEventHandler(counter));

            var runtime = bootstrap.Build();
            var result = runtime.Query<TestQuery, int>(new TestQuery());
            runtime.Publish(new TestEvent());

            Assert.AreEqual(42, result);
            Assert.AreEqual(1, counter.Value);
        }

        [Test]
        public void Build_CalledTwice_ReturnsSameRuntimeInstance()
        {
            var bootstrap = new CqrsBootstrap();

            var first = bootstrap.Build();
            var second = bootstrap.Build();

            Assert.AreSame(first, second);
        }
    }
}
