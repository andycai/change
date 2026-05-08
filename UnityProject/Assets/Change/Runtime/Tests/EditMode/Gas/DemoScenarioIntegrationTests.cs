using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Change.Runtime.Tests.Gas
{
    public class DemoScenarioIntegrationTests
    {
        private const string ScenarioBuilderTypeName = "GameScript.GasTemplate.Demo.DemoScenarioBuilder";
        private const string ReportTypeName = "GameScript.GasTemplate.BattleSimulationReport";
        private const string RunnerTypeName = "GameScript.GasTemplate.Entry.GasTemplateRunner";
        private const string BehaviourTypeName = "GameScript.GasTemplate.Entry.GasTemplateBehaviour";

        [Test]
        public void FullCastChain_DeterministicDamageBurnAndTrigger()
        {
            var builderType = RequireType(ScenarioBuilderTypeName);
            var reportType = RequireType(ReportTypeName);
            var builder = Activator.CreateInstance(builderType);
            var runMethod = builderType.GetMethod("RunSingleCastChain");

            Assert.IsNotNull(runMethod, $"{ScenarioBuilderTypeName}.RunSingleCastChain was not found.");

            var report = runMethod.Invoke(builder, null);
            Assert.IsNotNull(report);
            Assert.AreEqual(reportType, report.GetType());

            Assert.AreEqual(135f, ReadFloat(report, "HeroHealth"));
            Assert.AreEqual(180f, ReadFloat(report, "EnemyHealth"));
            Assert.IsTrue(ReadBool(report, "TriggerEffectApplied"));

            var events = ReadEvents(report, "Events");
            Assert.AreEqual(3, events.Length);
            Assert.AreEqual("cast.damage", ReadString(events[0], "Step"));
            Assert.AreEqual("trigger.heal", ReadString(events[1], "Step"));
            Assert.AreEqual("modifier.burn.tick", ReadString(events[2], "Step"));
        }

        [Test]
        public void RunnerAndBehaviour_UseSameScenarioBuilderAssemblyPath()
        {
            var runnerType = RequireType(RunnerTypeName);
            var behaviourType = RequireType(BehaviourTypeName);

            var runnerPath = ReadStringMember(runnerType, "ScenarioBuilderAssemblyPath");
            var behaviourPath = ReadStringMember(behaviourType, "ScenarioBuilderAssemblyPath");

            Assert.AreEqual(ScenarioBuilderTypeName, runnerPath);
            Assert.AreEqual(runnerPath, behaviourPath);
        }

        [Test]
        public void Runner_Run_ReturnsDeterministicBattleSimulationReport()
        {
            var runnerType = RequireType(RunnerTypeName);
            var reportType = RequireType(ReportTypeName);
            var runner = Activator.CreateInstance(runnerType);
            var runMethod = runnerType.GetMethod("Run");

            Assert.IsNotNull(runMethod, $"{RunnerTypeName}.Run was not found.");

            var first = runMethod.Invoke(runner, null);
            var second = runMethod.Invoke(runner, null);

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreEqual(reportType, first.GetType());
            Assert.AreEqual(reportType, second.GetType());

            Assert.AreEqual(ReadFloat(first, "HeroHealth"), ReadFloat(second, "HeroHealth"));
            Assert.AreEqual(ReadFloat(first, "EnemyHealth"), ReadFloat(second, "EnemyHealth"));
            Assert.AreEqual(ReadBool(first, "TriggerEffectApplied"), ReadBool(second, "TriggerEffectApplied"));

            var firstEvents = ReadEvents(first, "Events");
            var secondEvents = ReadEvents(second, "Events");
            Assert.AreEqual(firstEvents.Length, secondEvents.Length);

            for (var i = 0; i < firstEvents.Length; i++)
            {
                Assert.AreEqual(ReadString(firstEvents[i], "Step"), ReadString(secondEvents[i], "Step"));
                Assert.AreEqual(ReadFloat(firstEvents[i], "HeroHealth"), ReadFloat(secondEvents[i], "HeroHealth"));
                Assert.AreEqual(ReadFloat(firstEvents[i], "EnemyHealth"), ReadFloat(secondEvents[i], "EnemyHealth"));
            }
        }

        [Test]
        public void Behaviour_StartAndUpdate_DriveRunnerAndStoreLastReport()
        {
            var behaviourType = RequireType(BehaviourTypeName);
            var gameObject = new GameObject("gas-template-behaviour-test");

            try
            {
                var behaviour = gameObject.AddComponent(behaviourType);
                InvokeMethod(behaviour, "Start");
                var beforeUpdateReport = ReadMember(behaviour, "LastReport");
                Assert.IsNull(beforeUpdateReport, "Behaviour should remain lightweight in Start and defer scenario execution to Update.");

                InvokeMethod(behaviour, "Update");

                var report = ReadMember(behaviour, "LastReport");
                Assert.IsNotNull(report);
                Assert.AreEqual(135f, ReadFloat(report, "HeroHealth"));
                Assert.AreEqual(180f, ReadFloat(report, "EnemyHealth"));

                var events = ReadEvents(report, "Events");
                Assert.AreEqual(3, events.Length);

                // Ensure Update drives scenario once; subsequent updates do not mutate finished report.
                InvokeMethod(behaviour, "Update");
                var reportAfterSecondUpdate = ReadMember(behaviour, "LastReport");
                Assert.AreSame(report, reportAfterSecondUpdate);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static object[] ReadEvents(object instance, string propertyName)
        {
            var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
            Assert.IsNotNull(value, $"Property `{propertyName}` was null.");
            return ((IEnumerable)value).Cast<object>().ToArray();
        }

        private static float ReadFloat(object instance, string propertyName)
        {
            var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
            Assert.IsNotNull(value, $"Property `{propertyName}` was null.");
            return Convert.ToSingle(value);
        }

        private static bool ReadBool(object instance, string propertyName)
        {
            var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
            Assert.IsNotNull(value, $"Property `{propertyName}` was null.");
            return (bool)value;
        }

        private static string ReadString(object instance, string propertyName)
        {
            var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
            Assert.IsNotNull(value, $"Property `{propertyName}` was null.");
            return (string)value;
        }

        private static string ReadStringMember(Type type, string memberName)
        {
            var value = ReadStaticMember(type, memberName);
            Assert.IsNotNull(value, $"Member `{type.FullName}.{memberName}` was null.");
            return (string)value;
        }

        private static object ReadMember(object instance, string memberName)
        {
            var type = instance.GetType();
            var property = type.GetProperty(memberName);
            if (property != null)
            {
                return property.GetValue(instance);
            }

            var field = type.GetField(memberName);
            if (field != null)
            {
                return field.GetValue(instance);
            }

            Assert.Fail($"Member `{type.FullName}.{memberName}` was not found.");
            return null;
        }

        private static object ReadStaticMember(Type type, string memberName)
        {
            var property = type.GetProperty(memberName);
            if (property != null)
            {
                return property.GetValue(null);
            }

            var field = type.GetField(memberName);
            if (field != null)
            {
                return field.GetValue(null);
            }

            Assert.Fail($"Static member `{type.FullName}.{memberName}` was not found.");
            return null;
        }

        private static void InvokeMethod(object instance, string methodName)
        {
            var method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"{instance.GetType().FullName}.{methodName} was not found.");
            method.Invoke(instance, null);
        }

        private static Type RequireType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(x => x.GetType(fullName, throwOnError: false))
                .FirstOrDefault(x => x != null);

            Assert.IsNotNull(type, $"Type `{fullName}` was not found in loaded assemblies.");
            return type;
        }
    }
}
