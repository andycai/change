using Change.Framework.Gas;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public sealed class TargetResolverTests
    {
        private IAbilitySystem _source;
        private IAbilitySystem _ally;
        private IAbilitySystem _enemyA;
        private IAbilitySystem _enemyB;
        private IAbilitySystem[] _allEntities;

        [SetUp]
        public void SetUp()
        {
            _source = CreateSystem("hero-1", 1);
            _ally = CreateSystem("hero-2", 1);
            _enemyA = CreateSystem("enemy-1", 2);
            _enemyB = CreateSystem("enemy-2", 2);
            _allEntities = new[] { _source, _ally, _enemyA, _enemyB };
        }

        [Test]
        public void ResolveSelf_ReturnsSourceOnly()
        {
            var resolver = DefaultTargetResolvers.Create(TargetType.Self);

            var targets = resolver.Resolve(_source, _allEntities);

            Assert.AreEqual(1, targets.Length);
            Assert.AreSame(_source, targets[0]);
        }

        [Test]
        public void ResolveEnemy_ReturnsFirstEnemyByInputOrder()
        {
            var resolver = DefaultTargetResolvers.Create(TargetType.Enemy);

            var targets = resolver.Resolve(_source, _allEntities);

            Assert.AreEqual(1, targets.Length);
            Assert.AreSame(_enemyA, targets[0]);
        }

        [Test]
        public void ResolveAlly_ReturnsFirstAllyExcludingSelf()
        {
            var resolver = DefaultTargetResolvers.Create(TargetType.Ally);

            var targets = resolver.Resolve(_source, _allEntities);

            Assert.AreEqual(1, targets.Length);
            Assert.AreSame(_ally, targets[0]);
        }

        [Test]
        public void ResolveAllEnemies_ReturnsEveryEnemyInInputOrder()
        {
            var resolver = DefaultTargetResolvers.Create(TargetType.AllEnemies);

            var targets = resolver.Resolve(_source, _allEntities);

            CollectionAssert.AreEqual(new[] { _enemyA, _enemyB }, targets);
        }

        [Test]
        public void ResolveAllAllies_ReturnsAlliesExcludingSelfInInputOrder()
        {
            var resolver = DefaultTargetResolvers.Create(TargetType.AllAllies);

            var targets = resolver.Resolve(_source, _allEntities);

            CollectionAssert.AreEqual(new[] { _ally }, targets);
        }

        [Test]
        public void ResolveEnemy_WhenNoEnemyExists_ReturnsEmptyArray()
        {
            var resolver = DefaultTargetResolvers.Create(TargetType.Enemy);
            var allAllies = new[] { _source, _ally };

            var targets = resolver.Resolve(_source, allAllies);

            Assert.AreEqual(0, targets.Length);
        }

        [Test]
        public void ResolveAlly_WhenSameEntityIdButDifferentInstance_UsesTeamIdSemantics()
        {
            var resolver = DefaultTargetResolvers.Create(TargetType.Ally);
            var sameIdTeammate = CreateSystem("hero-1", 1);
            var allEntities = new[] { _source, sameIdTeammate, _enemyA };

            var targets = resolver.Resolve(_source, allEntities);

            Assert.AreEqual(1, targets.Length);
            Assert.AreSame(sameIdTeammate, targets[0]);
        }

        [Test]
        public void ResolveAllAllies_WhenSameEntityIdButDifferentInstance_UsesTeamIdSemantics()
        {
            var resolver = DefaultTargetResolvers.Create(TargetType.AllAllies);
            var sameIdTeammate = CreateSystem("hero-1", 1);
            var allEntities = new[] { _source, sameIdTeammate, _enemyA };

            var targets = resolver.Resolve(_source, allEntities);

            CollectionAssert.AreEqual(new[] { sameIdTeammate }, targets);
        }

        private static IAbilitySystem CreateSystem(string entityId, int teamId)
        {
            return new DefaultAbilitySystem(entityId, teamId, new DefaultAttributeSet(), new DefaultGameplayTagSet());
        }
    }
}
