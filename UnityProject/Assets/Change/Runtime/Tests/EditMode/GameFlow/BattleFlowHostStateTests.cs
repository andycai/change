using System;
using NUnit.Framework;

namespace Change.Runtime.Tests.EditMode.GameFlow
{
    public class BattleFlowHostStateTests
    {
        [Test]
        public void BattleHostState_OnEnter_Starts_Battle_At_Loading_And_Activates_Context()
        {
            var contextType = ResolveType("GameScript.GameFlow.Orchestration.GameFlowContext");
            var context = Activator.CreateInstance(contextType);
            Assert.That(context, Is.Not.Null, "GameFlowContext instance must be creatable.");

            var factoryType = ResolveType("GameScript.GameFlow.MainGameFlowMachineFactory");
            var createMethod = factoryType.GetMethod("Create");
            Assert.That(createMethod, Is.Not.Null, "MainGameFlowMachineFactory.Create() must exist.");

            var machine = createMethod!.Invoke(null, new[] { context! });
            Assert.That(machine, Is.Not.Null, "MainGameFlowMachineFactory.Create(context) must return a machine instance.");

            StartAt(machine!, "Boot");
            FireEvent(machine!, "BootstrapCompleted");
            FireEvent(machine!, "LoginSucceeded");
            FireEvent(machine!, "MatchRequested");
            FireEvent(machine!, "MatchFound");

            AssertState(machine!, "Battle");
            Assert.That((bool)contextType.GetProperty("IsBattleActive")!.GetValue(context!)!, Is.True);

            var battleMachineProperty = contextType.GetProperty("BattleMachine");
            Assert.That(battleMachineProperty, Is.Not.Null, "GameFlowContext.BattleMachine must exist.");

            var battleMachine = battleMachineProperty!.GetValue(context!);
            Assert.That(battleMachine, Is.Not.Null, "Battle host must create and assign a battle machine.");

            var currentBattleState = battleMachine!.GetType().GetProperty("CurrentStateId")!.GetValue(battleMachine);
            Assert.That(currentBattleState!.ToString(), Is.EqualTo("Loading"));
        }

        [Test]
        public void BattleHostState_OnExit_Deactivates_Context_When_Battle_Finishes()
        {
            var contextType = ResolveType("GameScript.GameFlow.Orchestration.GameFlowContext");
            var context = Activator.CreateInstance(contextType);
            Assert.That(context, Is.Not.Null, "GameFlowContext instance must be creatable.");

            var machine = ResolveType("GameScript.GameFlow.MainGameFlowMachineFactory")
                .GetMethod("Create")!
                .Invoke(null, new[] { context! });
            Assert.That(machine, Is.Not.Null);

            StartAt(machine!, "Boot");
            FireEvent(machine!, "BootstrapCompleted");
            FireEvent(machine!, "LoginSucceeded");
            FireEvent(machine!, "MatchRequested");
            FireEvent(machine!, "MatchFound");
            Assert.That((bool)contextType.GetProperty("IsBattleActive")!.GetValue(context!)!, Is.True);

            FireEvent(machine!, "BattleFinished");
            AssertState(machine!, "Result");
            Assert.That((bool)contextType.GetProperty("IsBattleActive")!.GetValue(context!)!, Is.False);
        }

        private static void StartAt(object machine, string initialState)
        {
            var stateIdType = ResolveType("GameScript.GameFlow.GameFlowStateId");
            var startMethod = machine.GetType().GetMethod("Start");
            Assert.That(startMethod, Is.Not.Null, $"{machine.GetType().FullName}.Start must exist.");
            startMethod!.Invoke(machine, new[] { Enum.Parse(stateIdType, initialState) });
        }

        private static void FireEvent(object machine, string eventName)
        {
            var eventType = ResolveType("GameScript.GameFlow.GameFlowEvent");
            var fireMethod = machine.GetType().GetMethod("Fire");
            Assert.That(fireMethod, Is.Not.Null, $"{machine.GetType().FullName}.Fire must exist.");
            fireMethod!.Invoke(machine, new[] { Enum.Parse(eventType, eventName) });
        }

        private static void AssertState(object machine, string expectedStateName)
        {
            var property = machine.GetType().GetProperty("CurrentStateId");
            Assert.That(property, Is.Not.Null, $"{machine.GetType().FullName}.CurrentStateId must exist.");

            var value = property!.GetValue(machine);
            Assert.That(value, Is.Not.Null);
            Assert.That(value!.ToString(), Is.EqualTo(expectedStateName));
        }

        private static Type ResolveType(string fullTypeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                {
                    continue;
                }

                var type = assembly.GetType(fullTypeName, throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail($"Type not found: {fullTypeName}");
            throw new InvalidOperationException("Unreachable");
        }
    }
}
