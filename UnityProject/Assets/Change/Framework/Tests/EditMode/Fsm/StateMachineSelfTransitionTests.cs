using System;
using NUnit.Framework;

namespace Change.Framework.Fsm.Tests
{
    public class StateMachineSelfTransitionTests
    {
        [Test]
        public void ChangeState_ToCurrentState_WhenAllowSelfTransitionIsTrue_ExecutesTransition()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>(allowSelfTransition: true);
            var stateA = new RecordingState(TestStateId.A);

            fsm.Register(stateA);
            fsm.Start(TestStateId.A);

            fsm.ChangeState(TestStateId.A);

            Assert.That(stateA.EnterCount, Is.EqualTo(2));
            Assert.That(stateA.ExitCount, Is.EqualTo(1));
        }

        [Test]
        public void Fire_TransitionToSelf_WhenAllowSelfTransitionIsTrue_ExecutesTransition()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>(allowSelfTransition: true);
            var stateA = new RecordingState(TestStateId.A)
            {
                OnEventHandler = _ => FsmResult<TestStateId>.TransitionTo(TestStateId.A)
            };

            fsm.Register(stateA);
            fsm.Start(TestStateId.A);

            fsm.Fire(TestEvent.Named("Self"));

            Assert.That(stateA.EnterCount, Is.EqualTo(2));
            Assert.That(stateA.ExitCount, Is.EqualTo(1));
        }

        [Test]
        public void FsmResult_NextStateId_ThrowsWhenNoTransition()
        {
            var result = FsmResult<TestStateId>.Handled();
            Assert.Throws<InvalidOperationException>(() => { var _ = result.NextStateId; });
        }
    }
}
