using System;
using System.Linq;
using System.Reflection;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Runtime.UI.Tests
{
    public class GameScriptSampleContractTests
    {
        private const string InventoryWindowPresenterTypeName = "GameScript.UI.Inventory.InventoryWindowPresenter";
        private const string OpenInventoryUseCaseTypeName = "GameScript.UI.Inventory.OpenInventoryUseCase";
        private const string OpenInventoryUseCaseContractTypeName = "GameScript.UI.Inventory.IOpenInventoryUseCase";

        [Test]
        public void InventoryWindowPresenter_TypeExists_InLoadedAssemblies()
        {
            var type = ResolveType(InventoryWindowPresenterTypeName);
            Assert.IsNotNull(type, $"Type `{InventoryWindowPresenterTypeName}` was not found in loaded assemblies.");
        }

        [Test]
        public void OpenInventoryUseCase_TypeExists_InLoadedAssemblies()
        {
            var type = ResolveType(OpenInventoryUseCaseTypeName);
            Assert.IsNotNull(type, $"Type `{OpenInventoryUseCaseTypeName}` was not found in loaded assemblies.");
        }

        [Test]
        public void InventoryWindowPresenter_DependsOnOpenInventoryUseCase_AndNotDirectlyOnBus()
        {
            var presenterType = RequireType(InventoryWindowPresenterTypeName);
            var useCaseContractType = RequireType(OpenInventoryUseCaseContractTypeName);
            var constructorParameterTypes = presenterType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(x => x.GetParameters())
                .Select(x => x.ParameterType)
                .ToArray();
            var fieldTypes = presenterType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(x => x.FieldType)
                .ToArray();

            Assert.IsTrue(
                constructorParameterTypes.Any(x => useCaseContractType.IsAssignableFrom(x)),
                $"Type `{InventoryWindowPresenterTypeName}` must depend on `{OpenInventoryUseCaseContractTypeName}`.");
            Assert.IsFalse(
                constructorParameterTypes.Any(x => x == typeof(ICqrsBus)),
                $"Type `{InventoryWindowPresenterTypeName}` must not depend directly on `{typeof(ICqrsBus).FullName}` constructor parameters.");
            Assert.IsFalse(
                fieldTypes.Any(x => x == typeof(ICqrsBus)),
                $"Type `{InventoryWindowPresenterTypeName}` must not depend directly on `{typeof(ICqrsBus).FullName}` fields.");
        }

        [Test]
        public void OpenInventoryUseCase_DependsOnCqrsBus()
        {
            var useCaseType = RequireType(OpenInventoryUseCaseTypeName);
            var constructorParameterTypes = useCaseType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(x => x.GetParameters())
                .Select(x => x.ParameterType)
                .ToArray();

            Assert.IsTrue(
                constructorParameterTypes.Any(x => x == typeof(ICqrsBus)),
                $"Type `{OpenInventoryUseCaseTypeName}` must depend on `{typeof(ICqrsBus).FullName}`.");
        }

        private static Type ResolveType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(x => x.GetType(fullName, throwOnError: false))
                .FirstOrDefault(x => x != null);
        }

        private static Type RequireType(string fullName)
        {
            var type = ResolveType(fullName);
            Assert.IsNotNull(type, $"Type `{fullName}` was not found in loaded assemblies.");
            return type;
        }
    }
}
