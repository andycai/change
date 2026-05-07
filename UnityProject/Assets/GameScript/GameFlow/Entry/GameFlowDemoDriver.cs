using GameScript.GameFlow.Orchestration;
using GameScript.GameFlow.BattleFlow;
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

        public BattleFlowStateId? CurrentBattleState =>
            _orchestrator?.Context.BattleMachine == null
                ? (BattleFlowStateId?)null
                : _orchestrator.Context.BattleMachine.CurrentStateId;

        private void Start()
        {
            RestartFlow();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                RestartFlow();
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                TriggerBootstrapCompleted();
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                TriggerLoginSuccess();
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                TriggerMatchRequested();
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                TriggerMatchFound();
            }

            if (Input.GetKeyDown(KeyCode.F10))
            {
                TriggerBattleSceneLoaded();
            }

            if (Input.GetKeyDown(KeyCode.F11))
            {
                TriggerCountdownFinished();
            }

            if (Input.GetKeyDown(KeyCode.F12))
            {
                TriggerBattleSettlement();
            }

            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                TriggerResultConfirm();
            }
        }

        public void RestartFlow()
        {
            _orchestrator = new GameFlowOrchestrator();
            _orchestrator.Start();
            LogState(nameof(RestartFlow));
        }

        public void Initialize()
        {
            EnsureInitialized();
            LogState(nameof(Initialize));
        }

        public void TriggerBootstrapCompleted()
        {
            EnsureInitialized();
            _orchestrator.OnBootstrapCompleted();
            LogState(nameof(TriggerBootstrapCompleted));
        }

        public void TriggerLoginSuccess()
        {
            EnsureInitialized();
            _orchestrator.OnLoginSucceeded(_demoPlayerId);
            LogState(nameof(TriggerLoginSuccess));
        }

        public void TriggerMatchRequested()
        {
            EnsureInitialized();
            _orchestrator.OnMatchRequested();
            LogState(nameof(TriggerMatchRequested));
        }

        public void TriggerMatchFound()
        {
            EnsureInitialized();
            _orchestrator.OnMatchFound(_demoMatchId);
            LogState(nameof(TriggerMatchFound));
        }

        public void TriggerBattleSceneLoaded()
        {
            EnsureInitialized();
            FireBattleEvent(BattleFlowEvent.SceneLoaded);
            LogState(nameof(TriggerBattleSceneLoaded));
        }

        public void TriggerCountdownFinished()
        {
            EnsureInitialized();
            FireBattleEvent(BattleFlowEvent.CountdownFinished);
            LogState(nameof(TriggerCountdownFinished));
        }

        public void TriggerBattleSettlement()
        {
            EnsureInitialized();
            FireBattleEvent(BattleFlowEvent.WinLoseResolved);
            FireBattleEvent(BattleFlowEvent.SettlementConfirmed);
            _orchestrator.OnBattleSettlementConfirmed();
            LogState(nameof(TriggerBattleSettlement));
        }

        public void TriggerResultConfirm()
        {
            EnsureInitialized();
            _orchestrator.OnResultConfirmed();
            LogState(nameof(TriggerResultConfirm));
        }

        public void SimulateToBattle()
        {
            TriggerBootstrapCompleted();
            TriggerLoginSuccess();
            TriggerMatchRequested();
            TriggerMatchFound();
        }

        public void SimulateRoundTripToLobby()
        {
            SimulateToBattle();
            TriggerBattleSceneLoaded();
            TriggerCountdownFinished();
            TriggerBattleSettlement();
            TriggerResultConfirm();
        }

        private void EnsureInitialized()
        {
            if (_orchestrator == null)
            {
                RestartFlow();
            }
        }

        private bool FireBattleEvent(BattleFlowEvent evt)
        {
            var battleMachine = _orchestrator.Context.BattleMachine;
            if (battleMachine == null)
            {
                return false;
            }

            battleMachine.Fire(evt);
            return true;
        }

        private void LogState(string actionName)
        {
            var battleState = CurrentBattleState.HasValue ? CurrentBattleState.Value.ToString() : "None";
            Debug.Log($"[GameFlowDemoDriver] Action={actionName}, MainState={CurrentMainState}, BattleState={battleState}");
        }
    }
}
