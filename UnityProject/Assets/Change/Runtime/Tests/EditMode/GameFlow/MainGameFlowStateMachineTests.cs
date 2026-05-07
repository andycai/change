using System;
using NUnit.Framework;

namespace Change.Runtime.Tests.EditMode.GameFlow
{
    public class MainGameFlowStateMachineTests
    {
        [Test]
        public void Contracts_Define_Expected_State_Event_And_Context_Shape()
        {
            AssertEnumMembers(
                "GameScript.GameFlow.GameFlowStateId",
                "Boot",
                "Login",
                "Lobby",
                "Match",
                "Battle",
                "Result");

            AssertEnumMembers(
                "GameScript.GameFlow.GameFlowEvent",
                "BootstrapCompleted",
                "LoginSucceeded",
                "MatchRequested",
                "MatchFound",
                "BattleFinished",
                "ConfirmResult");

            AssertEnumMembers(
                "GameScript.GameFlow.BattleFlow.BattleFlowStateId",
                "Loading",
                "Ready",
                "Playing",
                "Paused",
                "Settlement",
                "Exit");

            AssertEnumMembers(
                "GameScript.GameFlow.BattleFlow.BattleFlowEvent",
                "SceneLoaded",
                "CountdownFinished",
                "PauseRequested",
                "ResumeRequested",
                "BattleTimeUp",
                "WinLoseResolved",
                "SettlementConfirmed");

            var contextType = ResolveType("GameScript.GameFlow.Orchestration.GameFlowContext");
            Assert.That(contextType.GetProperty("PlayerId"), Is.Not.Null);
            Assert.That(contextType.GetProperty("MatchId"), Is.Not.Null);
            Assert.That(contextType.GetProperty("IsBattleActive"), Is.Not.Null);
        }

        private static void AssertEnumMembers(string fullTypeName, params string[] expectedMembers)
        {
            var type = ResolveType(fullTypeName);
            Assert.That(type.IsEnum, Is.True, $"{fullTypeName} must be an enum.");

            var names = Enum.GetNames(type);
            CollectionAssert.AreEquivalent(expectedMembers, names);
        }

        private static Type ResolveType(string fullTypeName)
        {
            var type = Type.GetType($"{fullTypeName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Type not found: {fullTypeName}");
            return type;
        }
    }
}
