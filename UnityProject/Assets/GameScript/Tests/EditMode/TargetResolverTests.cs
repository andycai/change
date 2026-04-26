using Change.Framework.Skill;
using GameScript.Skill.Core;
using GameScript.Skill.Targeting;
using NUnit.Framework;

namespace GameScript.Tests
{
    public class TargetResolverTests
    {
        private AbilitySystem _source;
        private IAbilitySystem[] _allEntities;

        [SetUp]
        public void SetUp()
        {
            _source = new AbilitySystem("hero1");
            var enemy1 = new AbilitySystem("enemy1");
            var enemy2 = new AbilitySystem("enemy2");
            var ally1 = new AbilitySystem("hero2");
            _allEntities = new IAbilitySystem[] { _source, enemy1, enemy2, ally1 };
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
        public void AoETarget_AllEnemies_ReturnsAllEnemies()
        {
            var resolver = new AoETargetResolver(TargetType.AllEnemies);
            var targets = resolver.Resolve(_source, _allEntities);
            Assert.AreEqual(2, targets.Length);
        }
    }
}
