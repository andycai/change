using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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

        private readonly struct TestDomainEvent : IEvent
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

        private sealed class TestDomainEventHandler : IEventHandler<TestDomainEvent>
        {
            private readonly Counter _counter;

            public TestDomainEventHandler(Counter counter)
            {
                _counter = counter;
            }

            public void Handle(in TestDomainEvent domainEvent)
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
        public void RegisterCommand_AfterBuild_Succeeds()
        {
            var bootstrap = new CqrsBootstrap();
            bootstrap.Build();

            Assert.DoesNotThrow(() => bootstrap.RegisterCommand(new TestCommandHandler(new Counter())));
        }

        [Test]
        public void Runtime_SupportsQueryAndPublish_HappyPath()
        {
            var bootstrap = new CqrsBootstrap();
            var counter = new Counter();
            bootstrap.RegisterQuery(new TestQueryHandler());
            bootstrap.Subscribe(new TestDomainEventHandler(counter));

            var runtime = bootstrap.Build();
            var result = runtime.Ask<TestQuery, int>(new TestQuery());
            runtime.Publish(new TestDomainEvent());

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

        [Test]
        public void Bootstrap_DoesNotExposePublicRuntimeProperty()
        {
            var runtimeProperty = typeof(CqrsBootstrap).GetProperty(
                "Runtime",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            Assert.IsNull(runtimeProperty);
        }

        [Test]
        public void RegisterQueryAndSubscribe_AfterBuild_Succeed()
        {
            var bootstrap = new CqrsBootstrap();
            bootstrap.Build();

            Assert.DoesNotThrow(() => bootstrap.RegisterQuery(new TestQueryHandler()));
            Assert.DoesNotThrow(() => bootstrap.Subscribe(new TestDomainEventHandler(new Counter())));
        }

        [Test]
        public void Bootstrap_DoesNotExposePublicFreezeMethod()
        {
            var freezeMethod = typeof(CqrsBootstrap).GetMethod(
                "Freeze",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            Assert.IsNull(freezeMethod);
        }

        [Test]
        public void Build_FromMultipleThreadsConcurrently_ReturnsSameRuntimeInstance()
        {
            const int callerCount = 32;
            var bootstrap = new CqrsBootstrap();
            var barrier = new Barrier(callerCount);
            var results = new ICqrsRuntime[callerCount];
            var tasks = new Task[callerCount];

            for (var i = 0; i < callerCount; i++)
            {
                var index = i;
                tasks[index] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    results[index] = bootstrap.Build();
                });
            }

            Task.WaitAll(tasks);

            var first = results[0];
            Assert.IsNotNull(first);
            for (var i = 1; i < callerCount; i++)
            {
                Assert.AreSame(first, results[i],
                    "Concurrent Build() must return the same runtime instance to all callers.");
            }
        }
    }
}
