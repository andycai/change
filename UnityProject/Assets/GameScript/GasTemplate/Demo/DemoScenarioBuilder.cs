using System.Collections.Generic;
using Change.Framework.Gas;
using Change.Runtime.Gas;
using Change.Runtime.Gas.Effects;

namespace GameScript.GasTemplate.Demo
{
    public sealed class DemoScenarioBuilder
    {
        public BattleSimulationReport RunSingleCastChain()
        {
            var hero = DemoEntities.CreateHero();
            var enemy = DemoEntities.CreateEnemy();
            var triggerEngine = DemoTriggers.CreateEngine();
            hero.AddTrigger(DemoTriggers.CreateOnDealDamageHealTrigger());

            var ability = BuildAbility();
            hero.AddAbility(ability);

            ability.Activate(hero, new IAbilitySystem[] { enemy });
            hero.Tick(1f);

            var events = new List<BattleSimulationEvent>(3)
            {
                new BattleSimulationEvent("cast.damage", hero.Attributes.GetCurrentValue("HP"), enemy.Attributes.GetCurrentValue("HP"))
            };

            triggerEngine.DispatchEvent(
                TriggerEventType.OnDealDamage,
                hero,
                hero,
                new IAbilitySystem[] { hero, enemy });

            events.Add(new BattleSimulationEvent("trigger.heal", hero.Attributes.GetCurrentValue("HP"), enemy.Attributes.GetCurrentValue("HP")));

            enemy.Tick(1f);
            events.Add(new BattleSimulationEvent("modifier.burn.tick", hero.Attributes.GetCurrentValue("HP"), enemy.Attributes.GetCurrentValue("HP")));

            return new BattleSimulationReport(
                heroHealth: hero.Attributes.GetCurrentValue("HP"),
                enemyHealth: enemy.Attributes.GetCurrentValue("HP"),
                triggerEffectApplied: hero.Attributes.GetCurrentValue("HP") > 120f,
                events: events);
        }

        private static GameplayAbility BuildAbility()
        {
            var config = new AbilityConfig
            {
                Id = "demo.fireblast",
                Type = AbilityType.Active,
                Activation = ActivationType.Manual,
                CastTime = 1f,
                Effects = new IGameplayEffect[]
                {
                    DemoEffects.CastDamage(),
                    new ApplyModifierEffect(() => DemoModifiers.CreateBurnModifier())
                }
            };
            return new GameplayAbility(config);
        }
    }
}
