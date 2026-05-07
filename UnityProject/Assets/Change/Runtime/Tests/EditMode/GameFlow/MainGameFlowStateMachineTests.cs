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
