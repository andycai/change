using System;
using System.Collections.Generic;
using Fun.Framework.Cqrs;
using NUnit.Framework;

namespace Fun.Framework.Tests
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
        public void Subscribe_NullHandler_ThrowsArgumentNullException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ArgumentNullException>(() => bus.Subscribe<ScoreChangedEvent>(null));
        }
    }
}
