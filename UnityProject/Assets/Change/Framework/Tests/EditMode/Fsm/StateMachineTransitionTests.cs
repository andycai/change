using System.Collections.Generic;
using NUnit.Framework;

namespace Change.Framework.Fsm.Tests
{
    public class StateMachineTransitionTests
    {
        [Test]
        public void Fire_HandledOrIgnored_DoesNotTransition()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            var stateA = new RecordingState(TestStateId.A)
            {
                OnEventHandler = evt => evt.Name == "Handled"
                    ? FsmResult<TestStateId>.Handled()
                    : FsmResult<TestStateId>.Ignored()
            };

            fsm.Register(stateA);
            fsm.Start(TestStateId.A);

            fsm.Fire(TestEvent.Named("Handled"));
            fsm.Fire(TestEvent.Named("Ignored"));

            Assert.That(fsm.CurrentStateId, Is.EqualTo(TestStateId.A));
            Assert.That(stateA.ExitCount, Is.EqualTo(0));
        }

        [Test]
        public void Fire_Transition_ExecutesExitEnterThenPublishesStateChanged()
        {
            var trace = new List<string>();

            var fsm = new StateMachine<TestStateId, TestEvent>();
            var stateA = new RecordingState(TestStateId.A)
            {
                OnEventHandler = evt => evt.Name == "GoB"
                    ? FsmResult<TestStateId>.TransitionTo(TestStateId.B)
                    : FsmResult<TestStateId>.Ignored(),
                OnExitAction = _ => trace.Add("Exit:A")
            };
            var stateB = new RecordingState(TestStateId.B)
            {
                OnEnterAction = _ => trace.Add("Enter:B")
            };

            fsm.OnStateChanged += change =>
            {
                trace.Add($"Changed:{change.From}->{change.To}:{change.CauseEvent.Name}");
            };

            fsm.Register(stateA);
            fsm.Register(stateB);
            fsm.Start(TestStateId.A);
            trace.Clear();

            fsm.Fire(TestEvent.Named("GoB"));

            Assert.That(fsm.CurrentStateId, Is.EqualTo(TestStateId.B));
            Assert.That(trace, Is.EqualTo(new[]
            {
                "Exit:A",
                "Enter:B",
                "Changed:A->B:GoB"
            }));
        }

        [Test]
        public void ChangeState_ToCurrentState_IsNoOp()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            var stateA = new RecordingState(TestStateId.A);

            fsm.Register(stateA);
            fsm.Start(TestStateId.A);

            fsm.ChangeState(TestStateId.A);

            Assert.That(stateA.EnterCount, Is.EqualTo(1));
            Assert.That(stateA.ExitCount, Is.EqualTo(0));
        }
    }
}
