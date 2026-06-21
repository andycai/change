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
            private readonly Counter _counter;
            public TestCommand(Counter counter) { _counter = counter; }
            public void Execute() { _counter.Value++; }
        }

        private readonly struct TestQuery : IQuery<int>
        {
            public int Query() => 42;
        }

        private readonly struct TestEvent : IEvent
        {
        }

        private sealed class TestEventHandler : IEventHandler<TestEvent>
        {
            private readonly Counter _counter;
            public TestEventHandler(Counter counter) { _counter = counter; }
            public void Handle(in TestEvent _) { _counter.Value++; }
        }

        private sealed class Counter
        {
            public int Value;
        }

        [Test]
        public void Build_ReturnsRuntime_ThatCanDispatchCommand()
        {
            var bus = new CqrsBus();
            var runtime = new CqrsBootstrap(bus).Build();

            var counter = new Counter();
            runtime.Send(new TestCommand(counter));

            Assert.AreEqual(1, counter.Value);
        }

        [Test]
        public void Runtime_SupportsQueryAndPublish_HappyPath()
        {
            var bus = new CqrsBus();
            var counter = new Counter();
            var bootstrap = new CqrsBootstrap(bus);
            bootstrap.Subscribe(new TestEventHandler(counter));

            var runtime = bootstrap.Build();
            var result = runtime.Ask<TestQuery, int>(new TestQuery());
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

        [Test]
        public void Bootstrap_DoesNotExposePublicRuntimeProperty()
        {
            var runtimeProperty = typeof(CqrsBootstrap).GetProperty(
                "Runtime",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            Assert.IsNull(runtimeProperty);
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
