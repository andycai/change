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
            Assert.That(ex.Message, Does.Contain("GoB"));
            Assert.That(ex.Message, Does.Contain("sequence="));
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
            Assert.That(ex.Message, Does.Contain("GoB"));
            Assert.That(ex.Message, Does.Contain("sequence="));
            Assert.That(ex.InnerException, Is.Not.Null);
            Assert.That(ex.InnerException.Message, Is.EqualTo("enter crash"));
        }

        [Test]
        public void Start_WhenInitialOnEnterThrows_ResetsMachineToNotStartedStateAndMarksAsFaulted()
        {
            var enterAttempts = 0;
            var stateA = new RecordingState(TestStateId.A)
            {
                OnEnterAction = _ =>
                {
                    enterAttempts++;
                    if (enterAttempts == 1)
                    {
                        throw new Exception("initial enter crash");
                    }
                }
            };

            var fsm = new StateMachine<TestStateId, TestEvent>();
            fsm.Register(stateA);

            var ex = Assert.Throws<InvalidOperationException>(() => fsm.Start(TestStateId.A));
            Assert.That(ex.Message, Does.Contain("reset and marked as faulted"));
            Assert.That(fsm.IsStarted, Is.False);
            Assert.That(fsm.IsFaulted, Is.True);
            
            // Should not be able to fire events while faulted
            Assert.Throws<InvalidOperationException>(() => fsm.Fire(TestEvent.Named("Tick")));

            // Should be able to restart and reset faulted state
            Assert.DoesNotThrow(() => fsm.Start(TestStateId.A));
            Assert.That(fsm.IsStarted, Is.True);
            Assert.That(fsm.IsFaulted, Is.False);
            Assert.That(fsm.CurrentStateId, Is.EqualTo(TestStateId.A));
            Assert.That(stateA.EnterCount, Is.EqualTo(2));
        }

        [Test]
        public void Fire_WhenCallbackThrows_MarksAsFaultedAndBlocksFurtherOperations()
        {
            var stateA = new RecordingState(TestStateId.A)
            {
                OnEventHandler = _ => throw new Exception("callback crash")
            };

            var fsm = new StateMachine<TestStateId, TestEvent>();
            fsm.Register(stateA);
            fsm.Start(TestStateId.A);

            Assert.Throws<InvalidOperationException>(() => fsm.Fire(TestEvent.Named("Tick")));
            Assert.That(fsm.IsFaulted, Is.True);

            // Subsequent operations should throw due to IsFaulted
            var ex = Assert.Throws<InvalidOperationException>(() => fsm.Fire(TestEvent.Named("Tick2")));
            Assert.That(ex.Message, Does.Contain("faulted state"));
        }
    }
}
