using System;
using GameScript.GasTemplate.Demo;

namespace GameScript.GasTemplate.Entry
{
    public sealed class GasTemplateRunner
    {
        public const string ScenarioBuilderAssemblyPath = "GameScript.GasTemplate.Demo.DemoScenarioBuilder";

        private readonly DemoScenarioBuilder _scenarioBuilder;

        public GasTemplateRunner()
            : this(new DemoScenarioBuilder())
        {
        }

        internal GasTemplateRunner(DemoScenarioBuilder scenarioBuilder)
        {
            _scenarioBuilder = scenarioBuilder ?? throw new ArgumentNullException(nameof(scenarioBuilder));
        }

        public BattleSimulationReport Run()
        {
            return _scenarioBuilder.RunSingleCastChain();
        }
    }
}
