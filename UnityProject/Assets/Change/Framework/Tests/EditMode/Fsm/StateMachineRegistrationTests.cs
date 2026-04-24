using System;
using NUnit.Framework;

namespace Change.Framework.Fsm.Tests
{
    public class StateMachineRegistrationTests
    {
        [Test]
        public void Register_DuplicateId_ThrowsInvalidOperationException()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();

            fsm.Register(new RecordingState(TestStateId.A));

            Assert.Throws<InvalidOperationException>(() => fsm.Register(new RecordingState(TestStateId.A)));
        }

        [Test]
        public void Start_UnknownInitialState_ThrowsInvalidOperationException()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            fsm.Register(new RecordingState(TestStateId.A));

            Assert.Throws<InvalidOperationException>(() => fsm.Start(TestStateId.Missing));
        }

        [Test]
        public void Start_Twice_ThrowsInvalidOperationException()
        {
            var fsm = new StateMachine<TestStateId, TestEvent>();
            fsm.Register(new RecordingState(TestStateId.A));

            fsm.Start(TestStateId.A);

            Assert.Throws<InvalidOperationException>(() => fsm.Start(TestStateId.A));
        }
    }
}
