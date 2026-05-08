using Change.Runtime.Gas;

namespace GameScript.GasTemplate.Demo
{
    public static class DemoEntities
    {
        public static AbilitySystem CreateHero()
        {
            var hero = new AbilitySystem("demo.hero", teamId: 1);
            hero.Attributes.SetBaseValue("HP", 120f);
            hero.Attributes.SetBaseValue("ATK", 100f);
            return hero;
        }

        public static AbilitySystem CreateEnemy()
        {
            var enemy = new AbilitySystem("demo.enemy", teamId: 2);
            enemy.Attributes.SetBaseValue("HP", 300f);
            return enemy;
        }
    }
}
