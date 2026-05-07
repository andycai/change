using GameScript.GameFlow.Orchestration;
using UnityEngine;

namespace GameScript.GameFlow.Entry
{
    public sealed class GameFlowDemoDriver : MonoBehaviour
    {
        [SerializeField] private int _demoPlayerId = 1001;
        [SerializeField] private int _demoMatchId = 2002;

        private GameFlowOrchestrator _orchestrator;

        public GameFlowStateId CurrentMainState =>
            _orchestrator == null ? GameFlowStateId.Boot : _orchestrator.MainMachine.CurrentStateId;

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                Initialize();
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                SimulateToBattle();
            }
        }

        public void Initialize()
        {
            if (_orchestrator != null)
            {
                return;
            }

            _orchestrator = new GameFlowOrchestrator();
            _orchestrator.Start();
        }

        public void SimulateToBattle()
        {
            Initialize();
            _orchestrator.OnBootstrapCompleted();
            _orchestrator.OnLoginSucceeded(_demoPlayerId);
            _orchestrator.OnMatchRequested();
            _orchestrator.OnMatchFound(_demoMatchId);
        }
    }
}
