namespace GameScript.GasTemplate
{
    public sealed class BattleSimulationEvent
    {
        public BattleSimulationEvent(string step, float heroHealth, float enemyHealth)
        {
            Step = step;
            HeroHealth = heroHealth;
            EnemyHealth = enemyHealth;
        }

        public string Step { get; }
        public float HeroHealth { get; }
        public float EnemyHealth { get; }
    }
}
