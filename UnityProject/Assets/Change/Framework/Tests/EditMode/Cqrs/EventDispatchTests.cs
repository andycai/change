using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class EventDispatchTests
    {
        private readonly struct ScoreChangedEvent : IEvent
        {
            public ScoreChangedEvent(int delta)
            {
                Delta = delta;
            }

            public int Delta { get; }
        }

        private sealed class OrderedEventHandler : IEventHandler<ScoreChangedEvent>
        {
            private readonly List<int> _order;
            private readonly int _id;

            public OrderedEventHandler(List<int> order, int id)
            {
                _order = order;
                _id = id;
            }

            public void Handle(in ScoreChangedEvent @event)
            {
                _order.Add(_id);
            }
        }

        private readonly struct StructScoreChangedHandler : IEventHandler<ScoreChangedEvent>
        {
            public void Handle(in ScoreChangedEvent @event)
            {
            }
        }

        [Test]
        public void Publish_InvokesSubscribersInRegistrationOrder()
        {
            var order = new List<int>();
            var bus = new CqrsBus();

            bus.Subscribe(new OrderedEventHandler(order, 1));
            bus.Subscribe(new OrderedEventHandler(order, 2));
            bus.Freeze();

            bus.Publish(new ScoreChangedEvent(10));

            CollectionAssert.AreEqual(new[] { 1, 2 }, order);
        }

        [Test]
        public void Publish_WithoutSubscribers_DoesNothing()
        {
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.DoesNotThrow(() => bus.Publish(new ScoreChangedEvent(1)));
        }

        [Test]
        public void Publish_BeforeFreeze_ThrowsInvalidOperationException()
        {
            var bus = new CqrsBus();

            Assert.Throws<InvalidOperationException>(() => bus.Publish(new ScoreChangedEvent(1)));
        }

        [Test]
        public void Subscribe_AfterFreeze_ThrowsRegistryFrozenException()
        {
            var order = new List<int>();
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.Throws<RegistryFrozenException>(() => bus.Subscribe(new OrderedEventHandler(order, 1)));
        }

        [Test]
        public void Subscribe_AfterFreeze_WithNullHandler_ThrowsRegistryFrozenException()
        {
            var bus = new CqrsBus();
            bus.Freeze();

            Assert.Throws<RegistryFrozenException>(() => bus.Subscribe<ScoreChangedEvent>(null));
        }

        [Test]
        public void Subscribe_NullHandler_ThrowsArgumentNullException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ArgumentNullException>(() => bus.Subscribe<ScoreChangedEvent>(null));
        }

        [Test]
        public void Subscribe_StructHandler_ThrowsInvalidOperationException()
        {
            var bus = new CqrsBus();

            Assert.Throws<InvalidOperationException>(() => bus.Subscribe(new StructScoreChangedHandler()));
        }

        [Test]
        public void Publish_WhenHandlerThrows_InvokesAllHandlersAndThrowsAggregateException()
        {
            var order = new List<int>();
            var bus = new CqrsBus();

            bus.Subscribe(new OrderedEventHandler(order, 1));
            bus.Subscribe(new ThrowingEventHandler());
            bus.Subscribe(new OrderedEventHandler(order, 2));
            bus.Freeze();

            var exception = Assert.Throws<AggregateException>(() => bus.Publish(new ScoreChangedEvent(10)));

            Assert.AreEqual(1, exception.InnerExceptions.Count);
            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerExceptions[0]);
            Assert.AreEqual("Handler failed", exception.InnerExceptions[0].Message);
            // All handlers are invoked despite the exception
            CollectionAssert.AreEqual(new[] { 1, 2 }, order);
        }

        [Test]
        public void Subscribe_FromMultipleThreads_RegistersAllHandlers()
        {
            const int handlerCount = 64;
            var barrier = new Barrier(handlerCount);
            var countHandler = new CountingEventHandler();
            var bus = new CqrsBus();
            var tasks = new Task[handlerCount];

            for (var i = 0; i < handlerCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    bus.Subscribe(countHandler);
                });
            }

            Task.WaitAll(tasks);
            bus.Freeze();
            bus.Publish(new ScoreChangedEvent(1));

            Assert.AreEqual(handlerCount, countHandler.Count);
        }

        private sealed class ThrowingEventHandler : IEventHandler<ScoreChangedEvent>
        {
            public void Handle(in ScoreChangedEvent @event)
            {
                throw new InvalidOperationException("Handler failed");
            }
        }

        private sealed class CountingEventHandler : IEventHandler<ScoreChangedEvent>
        {
            public int Count;

            public void Handle(in ScoreChangedEvent @event)
            {
                Count += @event.Delta;
            }
        }
    }
}
