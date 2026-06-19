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

            bus.Publish(new ScoreChangedEvent(10));

            CollectionAssert.AreEqual(new[] { 1, 2 }, order);
        }

        [Test]
        public void Publish_WithoutSubscribers_DoesNothing()
        {
            var bus = new CqrsBus();

            Assert.DoesNotThrow(() => bus.Publish(new ScoreChangedEvent(1)));
        }

        [Test]
        public void Subscribe_NullHandler_ThrowsArgumentNullException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ArgumentNullException>(() => bus.Subscribe<ScoreChangedEvent>((IEventHandler<ScoreChangedEvent>)null));
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
            bus.Publish(new ScoreChangedEvent(1));

            Assert.AreEqual(handlerCount, countHandler.Count);
        }

        [Test]
        public void ClosureCaptureException_IsInvalidOperationException_WithMessage()
        {
            var ex = new ClosureCaptureException("captured!");

            Assert.IsInstanceOf<InvalidOperationException>(ex);
            Assert.AreEqual("captured!", ex.Message);
        }

        // ===== Delegate mode fixtures =====

        private static int _staticCounter;

        private static void StaticIncrementHandler(ScoreChangedEvent @event)
        {
            _staticCounter += @event.Delta;
        }

        private sealed class DelegateRecorder
        {
            // 实例方法引用示例，仅用于闭包检测反例测试（Target != null，应被拒绝）
            public void Record(ScoreChangedEvent @event) { }
        }

        private static System.Collections.Generic.List<int> _publishDelegateOrder;

        private static void RecordDelegateCallStatic(ScoreChangedEvent @event)
        {
            _publishDelegateOrder?.Add(1);
        }

        private static void StaticThrowingHandler(ScoreChangedEvent @event)
        {
            throw new InvalidOperationException("Delegate failed");
        }

        // ===== Delegate subscribe tests =====

        [Test]
        public void Subscribe_StaticDelegate_InvokesOnPublish()
        {
            _staticCounter = 0;
            var bus = new CqrsBus();

            bus.Subscribe<ScoreChangedEvent>(StaticIncrementHandler);

            bus.Publish(new ScoreChangedEvent(7));

            Assert.AreEqual(7, _staticCounter);
        }

        [Test]
        public void Subscribe_NullDelegate_ThrowsArgumentNullException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ArgumentNullException>(
                () => bus.Subscribe<ScoreChangedEvent>((Action<ScoreChangedEvent>)null));
        }

        [Test]
        public void Subscribe_CapturingLambda_ThrowsClosureCaptureException()
        {
            var bus = new CqrsBus();
            var captured = 0;

            // lambda 捕获局部变量 captured → Target != null
            Assert.Throws<ClosureCaptureException>(
                () => bus.Subscribe<ScoreChangedEvent>(e => captured += e.Delta));
        }

        [Test]
        public void Subscribe_InstanceMethod_ThrowsClosureCaptureException()
        {
            var bus = new CqrsBus();
            var recorder = new DelegateRecorder();

            // 实例方法引用 → Target == recorder != null
            Assert.Throws<ClosureCaptureException>(
                () => bus.Subscribe<ScoreChangedEvent>(recorder.Record));
        }

        [Test]
        public void Publish_InvokesHandlersThenDelegates()
        {
            var handlerOrder = new System.Collections.Generic.List<int>();
            var delegateOrder = new System.Collections.Generic.List<int>();
            var bus = new CqrsBus();

            bus.Subscribe(new OrderedEventHandler(handlerOrder, 1));
            _publishDelegateOrder = delegateOrder;
            bus.Subscribe<ScoreChangedEvent>(RecordDelegateCallStatic);

            bus.Publish(new ScoreChangedEvent(0));

            CollectionAssert.AreEqual(new[] { 1 }, handlerOrder);
            Assert.AreEqual(1, delegateOrder.Count, "delegate should be invoked once");
        }

        [Test]
        public void Publish_WithoutSubscribers_DoesNothing_DelegatePath()
        {
            var bus = new CqrsBus();

            Assert.DoesNotThrow(() => bus.Publish(new ScoreChangedEvent(1)));
        }

        [Test]
        public void Publish_HandlerAndDelegateBothThrow_AggregatesAllExceptions()
        {
            var bus = new CqrsBus();

            bus.Subscribe(new ThrowingEventHandler());
            bus.Subscribe<ScoreChangedEvent>(StaticThrowingHandler);

            var exception = Assert.Throws<AggregateException>(
                () => bus.Publish(new ScoreChangedEvent(0)));

            Assert.AreEqual(2, exception.InnerExceptions.Count,
                "both handler and delegate exceptions should be aggregated");
        }

        // ===== Delegate unsubscribe tests =====

        [Test]
        public void Unsubscribe_Delegate_RemovesSpecificDelegate()
        {
            _staticCounter = 0;
            var bus = new CqrsBus();

            bus.Subscribe<ScoreChangedEvent>(StaticIncrementHandler);
            bus.Subscribe<ScoreChangedEvent>(StaticIncrementHandler);
            bus.Unsubscribe<ScoreChangedEvent>(StaticIncrementHandler);

            bus.Publish(new ScoreChangedEvent(5));

            // 订阅两次，反注册一次（移除首个匹配），剩余一次
            Assert.AreEqual(5, _staticCounter);
        }

        [Test]
        public void Unsubscribe_Delegate_WhenNotSubscribed_IsIdempotent()
        {
            var bus = new CqrsBus();

            Assert.DoesNotThrow(
                () => bus.Unsubscribe<ScoreChangedEvent>(StaticIncrementHandler));
        }

        [Test]
        public void Unsubscribe_NullDelegate_ThrowsArgumentNullException()
        {
            var bus = new CqrsBus();

            Assert.Throws<ArgumentNullException>(
                () => bus.Unsubscribe<ScoreChangedEvent>((Action<ScoreChangedEvent>)null));
        }

        [Test]
        public void Bootstrap_SubscribeStaticDelegate_InvokesOnPublish()
        {
            _staticCounter = 0;
            var bootstrap = new CqrsBootstrap();

            bootstrap.Subscribe<ScoreChangedEvent>(StaticIncrementHandler);
            ICqrsRuntime runtime = bootstrap.Build();

            runtime.Publish(new ScoreChangedEvent(9));

            Assert.AreEqual(9, _staticCounter);
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
