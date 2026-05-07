using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Change.Runtime.Tests.EditMode.GameFlow
{
    public class GameFlowDemoDriverTests
    {
        [Test]
        public void RestartFlow_Resets_To_Boot_And_Clears_Battle_Subflow()
        {
            var driverType = ResolveType("GameScript.GameFlow.Entry.GameFlowDemoDriver");
            var gameObject = new GameObject("GameFlowDemoDriverTests");

            try
            {
                var driver = gameObject.AddComponent(driverType);
                Invoke(driver, "SimulateToBattle");
                AssertCurrentMainState(driver, "Battle");
                AssertCurrentBattleState(driver, "Loading");

                Invoke(driver, "RestartFlow");
                AssertCurrentMainState(driver, "Boot");
                AssertCurrentBattleState(driver, null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Triggers_LoginSuccess_And_MatchFound_Progress_To_Battle()
        {
            var driverType = ResolveType("GameScript.GameFlow.Entry.GameFlowDemoDriver");
            var gameObject = new GameObject("GameFlowDemoDriverTests");

            try
            {
                var driver = gameObject.AddComponent(driverType);
                Invoke(driver, "RestartFlow");
                Invoke(driver, "TriggerBootstrapCompleted");
                Invoke(driver, "TriggerLoginSuccess");
                AssertCurrentMainState(driver, "Lobby");

                Invoke(driver, "TriggerMatchRequested");
                Invoke(driver, "TriggerMatchFound");
                AssertCurrentMainState(driver, "Battle");
                AssertCurrentBattleState(driver, "Loading");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TriggerCountdownFinished_Advances_Battle_Subflow_To_Playing()
        {
            var driverType = ResolveType("GameScript.GameFlow.Entry.GameFlowDemoDriver");
            var gameObject = new GameObject("GameFlowDemoDriverTests");

            try
            {
                var driver = gameObject.AddComponent(driverType);
                Invoke(driver, "SimulateToBattle");
                AssertCurrentBattleState(driver, "Loading");

                Invoke(driver, "TriggerBattleSceneLoaded");
                AssertCurrentBattleState(driver, "Ready");

                Invoke(driver, "TriggerCountdownFinished");
                AssertCurrentMainState(driver, "Battle");
                AssertCurrentBattleState(driver, "Playing");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TriggerBattleSettlement_Then_ResultConfirm_Progresses_To_Lobby()
        {
            var driverType = ResolveType("GameScript.GameFlow.Entry.GameFlowDemoDriver");
            var gameObject = new GameObject("GameFlowDemoDriverTests");

            try
            {
                var driver = gameObject.AddComponent(driverType);
                Invoke(driver, "SimulateToBattle");
                Invoke(driver, "TriggerBattleSceneLoaded");
                Invoke(driver, "TriggerCountdownFinished");

                Invoke(driver, "TriggerBattleSettlement");
                AssertCurrentMainState(driver, "Result");
                AssertCurrentBattleState(driver, null);

                Invoke(driver, "TriggerResultConfirm");
                AssertCurrentMainState(driver, "Lobby");
                AssertCurrentBattleState(driver, null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static void AssertCurrentMainState(Component driver, string expected)
        {
            var property = driver.GetType().GetProperty("CurrentMainState", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"{driver.GetType().FullName}.CurrentMainState must exist.");
            var value = property!.GetValue(driver);
            Assert.That(value, Is.Not.Null);
            Assert.That(value!.ToString(), Is.EqualTo(expected));
        }

        private static void AssertCurrentBattleState(Component driver, string expectedOrNull)
        {
            var property = driver.GetType().GetProperty("CurrentBattleState", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"{driver.GetType().FullName}.CurrentBattleState must exist.");
            var value = property!.GetValue(driver);

            if (expectedOrNull == null)
            {
                Assert.That(value, Is.Null);
                return;
            }

            Assert.That(value, Is.Not.Null);
            Assert.That(value!.ToString(), Is.EqualTo(expectedOrNull));
        }

        private static void Invoke(Component target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{target.GetType().FullName}.{methodName}() must exist.");
            method!.Invoke(target, args);
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
