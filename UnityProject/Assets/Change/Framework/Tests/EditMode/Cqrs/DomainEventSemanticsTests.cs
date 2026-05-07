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
    }
}
