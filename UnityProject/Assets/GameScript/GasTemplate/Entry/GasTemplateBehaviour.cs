using UnityEngine;

namespace GameScript.GasTemplate.Entry
{
    public sealed class GasTemplateBehaviour : MonoBehaviour
    {
        public const string ScenarioBuilderAssemblyPath = GasTemplateRunner.ScenarioBuilderAssemblyPath;

        private GasTemplateRunner _runner;
        private bool _hasCompleted;

        public BattleSimulationReport LastReport { get; private set; }

        private void Start()
        {
            _runner = new GasTemplateRunner();
        }

        private void Update()
        {
            if (_hasCompleted)
            {
                return;
            }

            LastReport = _runner.Run();
            _hasCompleted = true;
        }
    }
}
