using System;
using NUnit.Framework;

namespace Change.Framework.Fsm.Tests
{
    public class StateMachineExceptionTests
    {
        [Test]
        public void ChangeState_UnknownTarget_ThrowsInvalidOperationException()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            fsm.Register(new RecordingState(TestStateId.A));
            fsm.Start(TestStateId.A);

            var ex = Assert.Throws<InvalidOperationException>(() => fsm.ChangeState(TestStateId.Missing));
            Assert.That(ex.Message, Does.Contain("Target state"));
            Assert.That(ex.Message, Does.Contain("Missing"));
        }

        [Test]
        public void Fire_WhenOnExitThrows_WrapsExceptionWithTransitionContext()
        {
            var stateA = new RecordingState(TestStateId.A)
            {
                OnEventHandler = _ => FsmResult<TestStateId>.TransitionTo(TestStateId.B),
                OnExitAction = _ => throw new Exception("exit crash")
            };
            var stateB = new RecordingState(TestStateId.B);

            var fsm = new StateMachine<TestStateId, TestEvent>();
            fsm.Register(stateA);
            fsm.Register(stateB);
            fsm.Start(TestStateId.A);

            var ex = Assert.Throws<InvalidOperationException>(() => fsm.Fire(TestEvent.Named("GoB")));
            Assert.That(ex.Message, Does.Contain("OnExit"));
            Assert.That(ex.Message, Does.Contain("A"));
            Assert.That(ex.Message, Does.Contain("B"));
            Assert.That(ex.InnerException, Is.Not.Null);
            Assert.That(ex.InnerException.Message, Is.EqualTo("exit crash"));
        }

        [Test]
        public void Fire_WhenOnEnterThrows_WrapsExceptionWithTransitionContext()
        {
            var stateA = new RecordingState(TestStateId.A)
            {
                OnEventHandler = _ => FsmResult<TestStateId>.TransitionTo(TestStateId.B)
            };
            var stateB = new RecordingState(TestStateId.B)
            {
                OnEnterAction = _ => throw new Exception("enter crash")
            };

            var fsm = new StateMachine<TestStateId, TestEvent>();
            fsm.Register(stateA);
            fsm.Register(stateB);
            fsm.Start(TestStateId.A);

            var ex = Assert.Throws<InvalidOperationException>(() => fsm.Fire(TestEvent.Named("GoB")));
            Assert.That(ex.Message, Does.Contain("OnEnter"));
            Assert.That(ex.Message, Does.Contain("A"));
            Assert.That(ex.Message, Does.Contain("B"));
            Assert.That(ex.InnerException, Is.Not.Null);
            Assert.That(ex.InnerException.Message, Is.EqualTo("enter crash"));
        }
    }
}
