using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Change.Runtime.Tests.EditMode.GameFlow
{
    public class GameFlowOrchestratorTests
    {
        [Test]
        public void Start_And_Cqrs_Result_Methods_Drive_Main_Flow_Happy_Path()
        {
            var orchestrator = CreateOrchestrator();

            Invoke(orchestrator, "Start");
            AssertMainState(orchestrator, "Boot");

            Invoke(orchestrator, "OnBootstrapCompleted");
            AssertMainState(orchestrator, "Login");

            Invoke(orchestrator, "OnLoginSucceeded", 1001);
            AssertMainState(orchestrator, "Lobby");
            AssertContextInt(orchestrator, "PlayerId", 1001);

            Invoke(orchestrator, "OnMatchRequested");
            AssertMainState(orchestrator, "Match");

            Invoke(orchestrator, "OnMatchFound", 2002);
            AssertMainState(orchestrator, "Battle");
            AssertContextInt(orchestrator, "MatchId", 2002);

            Invoke(orchestrator, "OnBattleSettlementConfirmed");
            AssertMainState(orchestrator, "Result");

            Invoke(orchestrator, "OnResultConfirmed");
            AssertMainState(orchestrator, "Lobby");
        }

        [Test]
        public void OnMatchFound_Ignores_Duplicate_MatchId()
        {
            var orchestrator = CreateOrchestrator();
            Invoke(orchestrator, "Start");
            Invoke(orchestrator, "OnBootstrapCompleted");
            Invoke(orchestrator, "OnLoginSucceeded", 9);
            Invoke(orchestrator, "OnMatchRequested");

            Invoke(orchestrator, "OnMatchFound", 77);
            AssertMainState(orchestrator, "Battle");
            AssertContextInt(orchestrator, "MatchId", 77);

            Invoke(orchestrator, "OnMatchFound", 77);
            AssertMainState(orchestrator, "Battle");
            AssertContextInt(orchestrator, "MatchId", 77);
        }

        [Test]
        public void GuardDispatch_Does_Not_Dispatch_Before_Start()
        {
            var orchestrator = CreateOrchestrator();

            Invoke(orchestrator, "OnBootstrapCompleted");
            AssertMainState(orchestrator, "Boot");
            AssertMachineStarted(orchestrator, Is.False);
        }

        [Test]
        public void GuardDispatch_Does_Not_Dispatch_When_Machine_Faulted()
        {
            var orchestrator = CreateOrchestratorWithFaultingBoot();
            Assert.Throws<TargetInvocationException>(() => Invoke(orchestrator, "Start"));
            AssertMachineFaulted(orchestrator, Is.True);

            Invoke(orchestrator, "OnBootstrapCompleted");
            AssertMainState(orchestrator, "Boot");
        }

        private static object CreateOrchestrator()
        {
            var orchestratorType = ResolveType("GameScript.GameFlow.Orchestration.GameFlowOrchestrator");
            var createForTests = orchestratorType.GetMethod("CreateForTests");
            if (createForTests != null)
            {
                return createForTests.Invoke(null, Array.Empty<object>())!;
            }

            return Activator.CreateInstance(orchestratorType)!;
        }

        private static object CreateOrchestratorWithFaultingBoot()
        {
            var orchestratorType = ResolveType("GameScript.GameFlow.Orchestration.GameFlowOrchestrator");
            var createForTests = orchestratorType.GetMethod("CreateForTestsWithFaultOnEnter");
            Assert.That(createForTests, Is.Not.Null, "GameFlowOrchestrator.CreateForTests() must exist for fault-injection tests.");

            var bootStateType = ResolveType("GameScript.GameFlow.GameFlowStateId");
            var faultingState = Enum.Parse(bootStateType, "Boot");
            return createForTests!.Invoke(null, new[] { faultingState })!;
        }

        private static void Invoke(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName);
            Assert.That(method, Is.Not.Null, $"{target.GetType().FullName}.{methodName}() must exist.");
            method!.Invoke(target, args);
        }

        private static void AssertMainState(object orchestrator, string expectedState)
        {
            var machine = ReadMainMachine(orchestrator);
            var state = machine.GetType().GetProperty("CurrentStateId")!.GetValue(machine);
            Assert.That(state!.ToString(), Is.EqualTo(expectedState));
        }

        private static void AssertMachineFaulted(object orchestrator, IResolveConstraint expected)
        {
            var machine = ReadMainMachine(orchestrator);
            var isFaulted = machine.GetType().GetProperty("IsFaulted")!.GetValue(machine);
            Assert.That(isFaulted, expected);
        }

        private static void AssertMachineStarted(object orchestrator, IResolveConstraint expected)
        {
            var machine = ReadMainMachine(orchestrator);
            var isStarted = machine.GetType().GetProperty("IsStarted")!.GetValue(machine);
            Assert.That(isStarted, expected);
        }

        private static void AssertContextInt(object orchestrator, string propertyName, int expected)
        {
            var context = orchestrator.GetType().GetProperty("Context")!.GetValue(orchestrator);
            var value = (int)context!.GetType().GetProperty(propertyName)!.GetValue(context)!;
            Assert.That(value, Is.EqualTo(expected));
        }

        private static object ReadMainMachine(object orchestrator)
        {
            var property = orchestrator.GetType().GetProperty("MainMachine");
            Assert.That(property, Is.Not.Null, $"{orchestrator.GetType().FullName}.MainMachine must exist.");
            return property!.GetValue(orchestrator)!;
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
