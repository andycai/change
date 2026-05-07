using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class CqrsContextRuntimeProviderTests
    {
        private readonly struct TestCommand : ICommand
        {
        }

        private sealed class Counter
        {
            public int Value;
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

        [Test]
        public void Get_WhenContextNotRegistered_ThrowsHandlerNotRegisteredException()
        {
            var provider = new CqrsContextRuntimeProvider();

            Assert.Throws<HandlerNotRegisteredException>(() => provider.Get("battle"));
        }

        [Test]
        public void RegisterThenGet_ReturnsRuntimeForContext()
        {
            var provider = new CqrsContextRuntimeProvider();
            var counter = new Counter();
            var bootstrap = new CqrsBootstrap();
            bootstrap.RegisterCommand(new TestCommandHandler(counter));
            var runtime = bootstrap.Build();

            provider.Register("battle", runtime);
            var resolved = provider.Get("battle");
            resolved.Send(new TestCommand());

            Assert.AreSame(runtime, resolved);
            Assert.AreEqual(1, counter.Value);
        }
    }
}
