using System;
using Change.Framework.Fsm;
using GameScript.GameFlow.BattleFlow;
using GameScript.GameFlow.States;

namespace GameScript.GameFlow.Orchestration
{
    public sealed class GameFlowOrchestrator
    {
        private int? _lastMatchId;

        public GameFlowOrchestrator()
            : this(new GameFlowContext(), null)
        {
        }

        private GameFlowOrchestrator(GameFlowContext context, GameFlowStateId? faultOnEnterState)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            MainMachine = CreateMainMachine(Context, faultOnEnterState);
        }

        public GameFlowContext Context { get; }

        public StateMachine<GameFlowStateId, GameFlowEvent> MainMachine { get; }

        public static GameFlowOrchestrator CreateForTests()
        {
            return new GameFlowOrchestrator(new GameFlowContext(), null);
        }

        public static GameFlowOrchestrator CreateForTestsWithFaultOnEnter(GameFlowStateId stateId)
        {
            return new GameFlowOrchestrator(new GameFlowContext(), stateId);
        }

        public void Start()
        {
            if (MainMachine.IsStarted)
            {
                return;
            }

            MainMachine.Start(GameFlowStateId.Boot);
        }

        public void OnBootstrapCompleted()
        {
            Dispatch(GameFlowEvent.BootstrapCompleted);
        }

        public void OnLoginSucceeded(int playerId)
        {
            if (!CanDispatch())
            {
                return;
            }

            Context.PlayerId = playerId;
            Dispatch(GameFlowEvent.LoginSucceeded);
        }

        public void OnMatchRequested()
        {
            Dispatch(GameFlowEvent.MatchRequested);
        }

        public void OnMatchFound(int matchId)
        {
            if (!CanDispatch())
            {
                return;
            }

            if (_lastMatchId.HasValue && _lastMatchId.Value == matchId)
            {
                return;
            }

            Context.MatchId = matchId;
            _lastMatchId = matchId;
            Dispatch(GameFlowEvent.MatchFound);
        }

        public void OnBattleSettlementConfirmed()
        {
            if (!CanDispatch() || MainMachine.CurrentStateId != GameFlowStateId.Battle)
            {
                return;
            }

            if (Context.BattleMachine == null || Context.BattleMachine.CurrentStateId != BattleFlowStateId.Exit)
            {
                return;
            }

            Dispatch(GameFlowEvent.BattleFinished);
        }

        public void OnResultConfirmed()
        {
            _lastMatchId = null;
            Dispatch(GameFlowEvent.ConfirmResult);
        }

        private void Dispatch(GameFlowEvent evt)
        {
            if (!CanDispatch())
            {
                return;
            }

            MainMachine.Fire(evt);
        }

        private bool CanDispatch()
        {
            return MainMachine.IsStarted && !MainMachine.IsFaulted;
        }

        private static StateMachine<GameFlowStateId, GameFlowEvent> CreateMainMachine(GameFlowContext context, GameFlowStateId? faultOnEnterState)
        {
            var machine = new StateMachine<GameFlowStateId, GameFlowEvent>();
            machine.Register(WrapForFaultInjection(new BootState(), faultOnEnterState));
            machine.Register(WrapForFaultInjection(new LoginState(), faultOnEnterState));
            machine.Register(WrapForFaultInjection(new LobbyState(), faultOnEnterState));
            machine.Register(WrapForFaultInjection(new MatchState(), faultOnEnterState));
            machine.Register(WrapForFaultInjection(new BattleHostState(context), faultOnEnterState));
            machine.Register(WrapForFaultInjection(new ResultState(), faultOnEnterState));
            return machine;
        }

        private static IFsmState<GameFlowStateId, GameFlowEvent> WrapForFaultInjection(
            IFsmState<GameFlowStateId, GameFlowEvent> inner,
            GameFlowStateId? faultOnEnterState)
        {
            if (!faultOnEnterState.HasValue || inner.Id != faultOnEnterState.Value)
            {
                return inner;
            }

            return new FaultOnEnterState(inner);
        }

        private sealed class FaultOnEnterState : IFsmState<GameFlowStateId, GameFlowEvent>
        {
            private readonly IFsmState<GameFlowStateId, GameFlowEvent> _inner;

            public FaultOnEnterState(IFsmState<GameFlowStateId, GameFlowEvent> inner)
            {
                _inner = inner;
            }

            public GameFlowStateId Id => _inner.Id;

            public void OnEnter(in StateChange<GameFlowStateId, GameFlowEvent> change)
            {
                throw new InvalidOperationException($"Injected fault entering state '{Id}'.");
            }

            public void OnExit(in StateChange<GameFlowStateId, GameFlowEvent> change)
            {
                _inner.OnExit(in change);
            }

            public FsmResult<GameFlowStateId> OnEvent(in GameFlowEvent evt)
            {
                return _inner.OnEvent(in evt);
            }
        }
    }
}
