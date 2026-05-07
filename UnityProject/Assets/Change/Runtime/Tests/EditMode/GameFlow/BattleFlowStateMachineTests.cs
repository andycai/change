using System;
using System.Linq;
using NUnit.Framework;

namespace Change.Runtime.Tests.EditMode.GameFlow
{
    public class BattleFlowStateMachineTests
    {
        [Test]
        public void Create_Uses_Expected_Happy_Path_Transitions()
        {
            var machine = CreateBattleFlowMachine();

            AssertState(machine, "Loading");
            FireEvent(machine, "SceneLoaded");
            AssertState(machine, "Ready");

            FireEvent(machine, "CountdownFinished");
            AssertState(machine, "Playing");

            FireEvent(machine, "PauseRequested");
            AssertState(machine, "Paused");

            FireEvent(machine, "ResumeRequested");
            AssertState(machine, "Playing");

            FireEvent(machine, "BattleTimeUp");
            AssertState(machine, "Settlement");

            FireEvent(machine, "SettlementConfirmed");
            AssertState(machine, "Exit");
        }

        [Test]
        public void Playing_Goes_To_Settlement_When_WinLoseResolved()
        {
            var machine = CreateBattleFlowMachine();

            FireEvent(machine, "SceneLoaded");
            FireEvent(machine, "CountdownFinished");
            AssertState(machine, "Playing");

            FireEvent(machine, "WinLoseResolved");
            AssertState(machine, "Settlement");
        }

        [Test]
        public void Exit_Ignores_All_Events()
        {
            var machine = CreateBattleFlowMachine();

            FireEvent(machine, "SceneLoaded");
            FireEvent(machine, "CountdownFinished");
            FireEvent(machine, "BattleTimeUp");
            FireEvent(machine, "SettlementConfirmed");
            AssertState(machine, "Exit");

            FireEvent(machine, "SceneLoaded");
            FireEvent(machine, "CountdownFinished");
            FireEvent(machine, "PauseRequested");
            FireEvent(machine, "ResumeRequested");
            FireEvent(machine, "BattleTimeUp");
            FireEvent(machine, "WinLoseResolved");
            FireEvent(machine, "SettlementConfirmed");

            AssertState(machine, "Exit");
        }

        private static object CreateBattleFlowMachine()
        {
            var factoryType = ResolveType("GameScript.GameFlow.BattleFlow.BattleFlowMachineFactory");
            var createMethod = factoryType.GetMethod("Create");
            Assert.That(createMethod, Is.Not.Null, "BattleFlowMachineFactory.Create() must exist.");

            var machine = createMethod!.Invoke(null, Array.Empty<object>());
            Assert.That(machine, Is.Not.Null, "BattleFlowMachineFactory.Create() must return a machine instance.");
            return machine!;
        }

        private static void FireEvent(object machine, string eventName)
        {
            var eventType = ResolveType("GameScript.GameFlow.BattleFlow.BattleFlowEvent");
            var eventValue = Enum.Parse(eventType, eventName);

            var fireMethod = machine.GetType().GetMethod("Fire");
            Assert.That(fireMethod, Is.Not.Null, $"{machine.GetType().FullName}.Fire must exist.");
            fireMethod!.Invoke(machine, new[] { eventValue });
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
            var type = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .Select(assembly => assembly.GetType(fullTypeName, throwOnError: false))
                .FirstOrDefault(match => match != null);

            Assert.That(type, Is.Not.Null, $"Type not found: {fullTypeName}");
            return type!;
        }
    }
}
