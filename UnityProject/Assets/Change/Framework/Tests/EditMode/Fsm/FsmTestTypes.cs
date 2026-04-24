using System;
using Change.Framework.Fsm;

namespace Change.Framework.Fsm.Tests
{
    internal enum TestStateId
    {
        A,
        B,
        C,
        Missing
    }

    internal readonly struct TestEvent
    {
        public TestEvent(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public override string ToString()
        {
            return Name;
        }

        public static TestEvent Named(string name)
        {
            return new TestEvent(name);
        }
    }

    internal sealed class RecordingState : IFsmState<TestStateId, TestEvent>
    {
        public RecordingState(TestStateId id)
        {
            Id = id;
            OnEventHandler = _ => FsmResult<TestStateId>.Ignored();
        }

        public TestStateId Id { get; }

        public int EnterCount { get; private set; }

        public int ExitCount { get; private set; }

        public Func<TestEvent, FsmResult<TestStateId>> OnEventHandler { get; set; }

        public Action<StateChange<TestStateId, TestEvent>> OnEnterAction { get; set; }

        public Action<StateChange<TestStateId, TestEvent>> OnExitAction { get; set; }

        public Action<TestEvent> OnEventAction { get; set; }

        public void OnEnter(in StateChange<TestStateId, TestEvent> change)
        {
            EnterCount++;
            OnEnterAction?.Invoke(change);
        }

        public void OnExit(in StateChange<TestStateId, TestEvent> change)
        {
            ExitCount++;
            OnExitAction?.Invoke(change);
        }

        public FsmResult<TestStateId> OnEvent(in TestEvent evt)
        {
            OnEventAction?.Invoke(evt);
            return OnEventHandler.Invoke(evt);
        }
    }
}
