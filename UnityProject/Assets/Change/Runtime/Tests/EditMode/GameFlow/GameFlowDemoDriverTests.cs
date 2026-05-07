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
        public void SimulateToBattle_Reaches_Battle_State()
        {
            var driverType = ResolveType("GameScript.GameFlow.Entry.GameFlowDemoDriver");
            var gameObject = new GameObject("GameFlowDemoDriverTests");

            try
            {
                var driver = gameObject.AddComponent(driverType);
                Invoke(driver, "SimulateToBattle");
                AssertCurrentMainState(driver, "Battle");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void StartPath_Initializes_Orchestrator_And_Enters_Boot()
        {
            var driverType = ResolveType("GameScript.GameFlow.Entry.GameFlowDemoDriver");
            var gameObject = new GameObject("GameFlowDemoDriverTests");

            try
            {
                var driver = gameObject.AddComponent(driverType);
                Invoke(driver, "Initialize");
                AssertCurrentMainState(driver, "Boot");
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
