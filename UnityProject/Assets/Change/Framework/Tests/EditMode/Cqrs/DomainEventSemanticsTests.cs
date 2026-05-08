using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class DomainEventSemanticsTests
    {
        private readonly struct EnemyDefeatedDomainEvent : IDomainEvent
        {
            public EnemyDefeatedDomainEvent(int rewardGold)
            {
                RewardGold = rewardGold;
            }

            public int RewardGold { get; }
        }

        private readonly struct ItemPickedUpDomainEvent : IDomainEvent
        {
        }

        private sealed class DomainEventCounter
        {
            public int TotalRewardGold;
        }

        private sealed class EnemyDefeatedDomainEventHandler : IDomainEventHandler<EnemyDefeatedDomainEvent>
        {
            private readonly DomainEventCounter _counter;

            public EnemyDefeatedDomainEventHandler(DomainEventCounter counter)
            {
                _counter = counter;
            }

            public void Handle(in EnemyDefeatedDomainEvent domainEvent)
            {
                _counter.TotalRewardGold += domainEvent.RewardGold;
            }
        }

        private sealed class ItemPickedUpHandler : IEventHandler<ItemPickedUpDomainEvent>
        {
            public int InvocationCount;

            public void Handle(in ItemPickedUpDomainEvent @event)
            {
                InvocationCount++;
            }
        }

        [Test]
        public void PublishDomainEvent_ThroughRuntime_InvokesDomainEventHandler()
        {
            var counter = new DomainEventCounter();
            var bootstrap = new CqrsBootstrap();
            bootstrap.Subscribe(new EnemyDefeatedDomainEventHandler(counter));
            var runtime = bootstrap.Build();

            runtime.Publish(new EnemyDefeatedDomainEvent(15));

            Assert.AreEqual(15, counter.TotalRewardGold);
        }

        [Test]
        public void PublishDomainEvent_DoesNotFanOutToHandlersOfDifferentDomainEventType()
        {
            var counter = new DomainEventCounter();
            var unrelated = new ItemPickedUpHandler();

            var bootstrap = new CqrsBootstrap();
            bootstrap.Subscribe(new EnemyDefeatedDomainEventHandler(counter));
            bootstrap.Subscribe(unrelated);
            var runtime = bootstrap.Build();

            runtime.Publish(new EnemyDefeatedDomainEvent(7));

            Assert.AreEqual(7, counter.TotalRewardGold);
            Assert.AreEqual(0, unrelated.InvocationCount,
                "Publish must match subscribers strictly by closed generic type; unrelated handlers must not be invoked.");
        }
    }
}
