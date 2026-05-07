using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Change.Runtime.Tests.EditMode.GameFlow
{
    public class MainGameFlowStateMachineTests
    {
        [Test]
        public void Contracts_Define_Expected_State_Event_And_Context_Shape()
        {
            AssertEnumDefinition(
                "GameScript.GameFlow.GameFlowStateId",
                "Boot",
                "Login",
                "Lobby",
                "Match",
                "Battle",
                "Result");

            AssertEnumDefinition(
                "GameScript.GameFlow.GameFlowEvent",
                "BootstrapCompleted",
                "LoginSucceeded",
                "MatchRequested",
                "MatchFound",
                "BattleFinished",
                "ConfirmResult");

            AssertEnumDefinition(
                "GameScript.GameFlow.BattleFlow.BattleFlowStateId",
                "Loading",
                "Ready",
                "Playing",
                "Paused",
                "Settlement",
                "Exit");

            AssertEnumDefinition(
                "GameScript.GameFlow.BattleFlow.BattleFlowEvent",
                "SceneLoaded",
                "CountdownFinished",
                "PauseRequested",
                "ResumeRequested",
                "BattleTimeUp",
                "WinLoseResolved",
                "SettlementConfirmed");

            var contextType = ResolveType("GameScript.GameFlow.Orchestration.GameFlowContext");
            AssertPropertyType(contextType, "PlayerId", typeof(int));
            AssertPropertyType(contextType, "MatchId", typeof(int));
            AssertPropertyType(contextType, "IsBattleActive", typeof(bool));
        }

        [Test]
        public void Create_Uses_Expected_Main_GameFlow_Transitions()
        {
            var machine = CreateMainMachine();

            AssertState(machine, "Boot");
            FireEvent(machine, "BootstrapCompleted");
            AssertState(machine, "Login");

            FireEvent(machine, "LoginSucceeded");
            AssertState(machine, "Lobby");

            FireEvent(machine, "MatchRequested");
            AssertState(machine, "Match");

            FireEvent(machine, "MatchFound");
            AssertState(machine, "Battle");

            FireEvent(machine, "BattleFinished");
            AssertState(machine, "Result");

            FireEvent(machine, "ConfirmResult");
            AssertState(machine, "Lobby");
        }

        [Test]
        public void Create_Throws_ArgumentNullException_When_Context_Is_Null()
        {
            var factoryType = ResolveType("GameScript.GameFlow.MainGameFlowMachineFactory");
            var createMethod = factoryType.GetMethod("Create");
            Assert.That(createMethod, Is.Not.Null, "MainGameFlowMachineFactory.Create() must exist.");

            var ex = Assert.Throws<TargetInvocationException>(() => createMethod!.Invoke(null, new object[] { null! }));
            Assert.That(ex!.InnerException, Is.TypeOf<ArgumentNullException>());
            Assert.That(((ArgumentNullException)ex.InnerException!).ParamName, Is.EqualTo("context"));
        }

        private static object CreateMainMachine()
        {
            var factoryType = ResolveType("GameScript.GameFlow.MainGameFlowMachineFactory");
            var stateIdType = ResolveType("GameScript.GameFlow.GameFlowStateId");
            var createMethod = factoryType.GetMethod("Create");
            Assert.That(createMethod, Is.Not.Null, "MainGameFlowMachineFactory.Create() must exist.");

            var contextType = ResolveType("GameScript.GameFlow.Orchestration.GameFlowContext");
            var context = Activator.CreateInstance(contextType);
            Assert.That(context, Is.Not.Null, "GameFlowContext instance must be creatable.");

            var machine = createMethod!.Invoke(null, new[] { context! });
            Assert.That(machine, Is.Not.Null, "MainGameFlowMachineFactory.Create(context) must return a machine instance.");

            var startMethod = machine!.GetType().GetMethod("Start");
            Assert.That(startMethod, Is.Not.Null, $"{machine.GetType().FullName}.Start must exist.");
            var bootState = Enum.Parse(stateIdType, "Boot");
            startMethod!.Invoke(machine, new[] { bootState });

            return machine!;
        }

        private static void FireEvent(object machine, string eventName)
        {
            var eventType = ResolveType("GameScript.GameFlow.GameFlowEvent");
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

        private static void AssertEnumDefinition(string fullTypeName, params string[] expectedMembers)
        {
            var type = ResolveType(fullTypeName);
            Assert.That(type.IsEnum, Is.True, $"{fullTypeName} must be an enum.");
            Assert.That(Enum.GetUnderlyingType(type), Is.EqualTo(typeof(int)), $"{fullTypeName} must use int as underlying enum type.");

            var names = Enum.GetNames(type);
            CollectionAssert.AreEqual(expectedMembers, names);

            for (var i = 0; i < expectedMembers.Length; i++)
            {
                var value = (int)Enum.Parse(type, expectedMembers[i]);
                Assert.That(value, Is.EqualTo(i), $"{fullTypeName}.{expectedMembers[i]} must map to {i}.");
            }
        }

        private static void AssertPropertyType(Type ownerType, string propertyName, Type expectedPropertyType)
        {
            var property = ownerType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, $"{ownerType.FullName}.{propertyName} is missing.");
            Assert.That(property!.PropertyType, Is.EqualTo(expectedPropertyType), $"{ownerType.FullName}.{propertyName} must be {expectedPropertyType.Name}.");
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
