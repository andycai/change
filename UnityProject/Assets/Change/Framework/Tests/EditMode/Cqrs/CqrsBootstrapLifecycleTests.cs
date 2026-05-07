using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class CqrsBootstrapLifecycleTests
    {
        private readonly struct TestCommand : ICommand
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

        private sealed class Counter
        {
            public int Value;
        }

        private sealed class DefaultCqrsRuntime : ICqrsRuntime
        {
            private readonly ICqrsBus _bus;

            public DefaultCqrsRuntime(ICqrsBus bus)
            {
                _bus = bus;
            }

            public void Send<TCommand>(in TCommand command)
                where TCommand : struct, ICommand
            {
                _bus.Send(in command);
            }

            public TResult Query<TQuery, TResult>(in TQuery query)
                where TQuery : struct, IQuery<TResult>
            {
                return _bus.Query<TQuery, TResult>(in query);
            }

            public void Publish<TEvent>(in TEvent @event)
                where TEvent : struct, IEvent
            {
                _bus.Publish(in @event);
            }
        }

        private sealed class DefaultCqrsBootstrap : ICqrsBootstrap
        {
            private readonly CqrsBus _bus = new();

            public void RegisterCommand<TCommand>(ICommandHandler<TCommand> handler)
                where TCommand : struct, ICommand
            {
                _bus.RegisterCommand(handler);
            }

            public void RegisterQuery<TQuery, TResult>(IQueryHandler<TQuery, TResult> handler)
                where TQuery : struct, IQuery<TResult>
            {
                _bus.RegisterQuery(handler);
            }

            public void Subscribe<TEvent>(IEventHandler<TEvent> handler)
                where TEvent : struct, IEvent
            {
                _bus.Subscribe(handler);
            }

            public void Freeze()
            {
                _bus.Freeze();
            }

            public ICqrsRuntime Build()
            {
                _bus.Freeze();
                return new DefaultCqrsRuntime(_bus);
            }
        }

        [Test]
        public void Build_ReturnsRuntime_ThatCanDispatchCommand()
        {
            var bootstrap = new DefaultCqrsBootstrap();
            var counter = new Counter();
            bootstrap.RegisterCommand(new TestCommandHandler(counter));

            var runtime = bootstrap.Build();
            runtime.Send(new TestCommand());

            Assert.AreEqual(1, counter.Value);
        }
    }
}
