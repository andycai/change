using Change.Framework.Skill;
using Change.Runtime.Skill;
using Change.Runtime.Skill.Targeting;
using NUnit.Framework;

namespace Change.Runtime.Tests
{
    public class TargetResolverTests
    {
        private AbilitySystem _source;
        private IAbilitySystem[] _allEntities;

        [SetUp]
        public void SetUp()
        {
            // Team 0 = heroes, Team 1 = enemies
            _source = new AbilitySystem("hero1", teamId: 0);
            var ally1 = new AbilitySystem("hero2", teamId: 0);
            var enemy1 = new AbilitySystem("enemy1", teamId: 1);
            var enemy2 = new AbilitySystem("enemy2", teamId: 1);
            _allEntities = new IAbilitySystem[] { _source, ally1, enemy1, enemy2 };
        }

        [Test]
        public void SelfTarget_ReturnsSource()
        {
            var resolver = new SelfTargetResolver();
            var targets = resolver.Resolve(_source, _allEntities);
            Assert.AreEqual(1, targets.Length);
            Assert.AreEqual("hero1", targets[0].EntityId);
        }

        [Test]
        public void EnemyTarget_ReturnsCorrectCount()
        {
            var resolver = new EnemyTargetResolver(count: 2);
            var targets = resolver.Resolve(_source, _allEntities);
            Assert.AreEqual(2, targets.Length);
        }

        [Test]
        public void AoETarget_AllEnemies_ReturnsAllEnemyEntities()
        {
            var resolver = new AoETargetResolver(TargetType.AllEnemies);
            var targets = resolver.Resolve(_source, _allEntities);
            Assert.AreEqual(2, targets.Length);
        }
    }
}
