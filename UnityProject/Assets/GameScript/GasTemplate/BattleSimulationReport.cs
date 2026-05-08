using System;
using System.Collections.Generic;

namespace GameScript.GasTemplate
{
    public sealed class BattleSimulationReport
    {
        public BattleSimulationReport(float heroHealth, float enemyHealth, bool triggerEffectApplied, IReadOnlyList<BattleSimulationEvent> events)
        {
            HeroHealth = heroHealth;
            EnemyHealth = enemyHealth;
            TriggerEffectApplied = triggerEffectApplied;
            Events = events ?? Array.Empty<BattleSimulationEvent>();
        }

        public float HeroHealth { get; }
        public float EnemyHealth { get; }
        public bool TriggerEffectApplied { get; }
        public IReadOnlyList<BattleSimulationEvent> Events { get; }
    }
}
