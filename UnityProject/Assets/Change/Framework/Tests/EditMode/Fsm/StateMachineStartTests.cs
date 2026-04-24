using System;
using NUnit.Framework;

namespace Fun.Framework.Fsm.Tests
{
    public class StateMachineStartTests
    {
        [Test]
        public void Start_EntersInitialState()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            var stateA = new RecordingState(TestStateId.A);

            fsm.Register(stateA);
            fsm.Start(TestStateId.A);

            Assert.That(fsm.IsStarted, Is.True);
            Assert.That(fsm.CurrentStateId, Is.EqualTo(TestStateId.A));
            Assert.That(stateA.EnterCount, Is.EqualTo(1));
        }

        [Test]
        public void Fire_BeforeStart_ThrowsInvalidOperationException()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            var stateA = new RecordingState(TestStateId.A);
            fsm.Register(stateA);

            Assert.Throws<InvalidOperationException>(() => fsm.Fire(TestEvent.Named("Tick")));
        }
    }
}
