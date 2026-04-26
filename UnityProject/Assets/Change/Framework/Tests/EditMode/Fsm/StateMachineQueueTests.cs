using System.Collections.Generic;
using NUnit.Framework;

namespace Change.Framework.Fsm.Tests
{
    public class StateMachineQueueTests
    {
        [Test]
        public void Fire_InsideOnEvent_IsQueuedAndProcessedInOrder()
        {
            var trace = new List<string>();
            var fsm = new StateMachine<TestStateId, TestEvent>();

            var stateA = new RecordingState(TestStateId.A);
            var stateB = new RecordingState(TestStateId.B)
            {
                OnEnterAction = _ => trace.Add("Enter:B")
            };

            stateA.OnEventHandler = evt =>
            {
                trace.Add($"OnEvent:{evt.Name}");
                if (evt.Name == "First")
                {
                    fsm.Fire(TestEvent.Named("Second"));
                    return FsmResult<TestStateId>.Handled();
                }

                if (evt.Name == "Second")
                {
                    return FsmResult<TestStateId>.TransitionTo(TestStateId.B);
                }

                return FsmResult<TestStateId>.Ignored();
            };
            stateA.OnExitAction = _ => trace.Add("Exit:A");

            fsm.Register(stateA);
            fsm.Register(stateB);
            fsm.Start(TestStateId.A);

            fsm.Fire(TestEvent.Named("First"));

            Assert.That(trace, Is.EqualTo(new[]
            {
                "OnEvent:First",
                "OnEvent:Second",
                "Exit:A",
                "Enter:B"
            }));
            Assert.That(fsm.CurrentStateId, Is.EqualTo(TestStateId.B));
        }

        [Test]
        public void ChangeState_InsideOnEnter_IsSerializedWithoutReentrancyCrash()
        {
            var trace = new List<string>();
            var fsm = new StateMachine<TestStateId, TestEvent>();

            var stateA = new RecordingState(TestStateId.A)
            {
                OnEnterAction = _ =>
                {
                    trace.Add("Enter:A");
                    fsm.ChangeState(TestStateId.B);
                },
                OnExitAction = _ => trace.Add("Exit:A")
            };
            var stateB = new RecordingState(TestStateId.B)
            {
                OnEnterAction = _ => trace.Add("Enter:B")
            };

            fsm.Register(stateA);
            fsm.Register(stateB);

            fsm.Start(TestStateId.A);

            Assert.That(trace, Is.EqualTo(new[]
            {
                "Enter:A",
                "Exit:A",
                "Enter:B"
            }));
            Assert.That(fsm.CurrentStateId, Is.EqualTo(TestStateId.B));
        }

        [Test]
        public void Fire_InsideOnEnter_IsQueuedUntilOnEnterReturns()
        {
            var trace = new List<string>();
            var fsm = new StateMachine<TestStateId, TestEvent>();

            var stateA = new RecordingState(TestStateId.A)
            {
                OnEnterAction = _ =>
                {
                    trace.Add("Enter:A");
                    fsm.Fire(TestEvent.Named("GoB"));
                    trace.Add("Enter:A:AfterFire");
                },
                OnEventHandler = evt =>
                {
                    trace.Add($"OnEvent:{evt.Name}");
                    return evt.Name == "GoB"
                        ? FsmResult<TestStateId>.TransitionTo(TestStateId.B)
                        : FsmResult<TestStateId>.Ignored();
                },
                OnExitAction = _ => trace.Add("Exit:A")
            };
            var stateB = new RecordingState(TestStateId.B)
            {
                OnEnterAction = _ => trace.Add("Enter:B")
            };

            fsm.Register(stateA);
            fsm.Register(stateB);

            fsm.Start(TestStateId.A);

            Assert.That(trace, Is.EqualTo(new[]
            {
                "Enter:A",
                "Enter:A:AfterFire",
                "OnEvent:GoB",
                "Exit:A",
                "Enter:B"
            }));
            Assert.That(fsm.CurrentStateId, Is.EqualTo(TestStateId.B));
        }

        [Test]
        public void ChangeState_InsideOnEvent_IsDeferredUntilOnEventReturns()
        {
            var trace = new List<string>();
            var fsm = new StateMachine<TestStateId, TestEvent>();

            var stateA = new RecordingState(TestStateId.A)
            {
                OnEventHandler = _ =>
                {
                    trace.Add("OnEvent:Begin");
                    fsm.ChangeState(TestStateId.B);
                    trace.Add("OnEvent:End");
                    return FsmResult<TestStateId>.Handled();
                },
                OnExitAction = _ => trace.Add("Exit:A")
            };
            var stateB = new RecordingState(TestStateId.B)
            {
                OnEnterAction = _ => trace.Add("Enter:B")
            };

            fsm.Register(stateA);
            fsm.Register(stateB);
            fsm.Start(TestStateId.A);

            fsm.Fire(TestEvent.Named("Tick"));

            Assert.That(trace, Is.EqualTo(new[]
            {
                "OnEvent:Begin",
                "OnEvent:End",
                "Exit:A",
                "Enter:B"
            }));
            Assert.That(fsm.CurrentStateId, Is.EqualTo(TestStateId.B));
        }
    }
}
