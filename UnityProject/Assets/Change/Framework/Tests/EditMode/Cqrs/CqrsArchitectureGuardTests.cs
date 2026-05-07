using System;
using System.Collections.Generic;
using System.Reflection;
using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class CqrsArchitectureGuardTests
    {
        private static readonly Type[] ForbiddenDispatchDependencies =
        {
            typeof(ICqrsRuntime),
            typeof(ICqrsBus)
        };

        [Test]
        public void DomainEventHandlers_MustNotInjectRuntimeOrCommandDispatchSurface()
        {
            var violations = new List<string>();

            foreach (var assembly in GetRelevantAssemblies())
            {
                foreach (var handlerType in GetConcreteDomainEventHandlerTypes(assembly))
                {
                    foreach (var constructor in handlerType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        foreach (var parameter in constructor.GetParameters())
                        {
                            if (!IsForbiddenDispatchDependency(parameter.ParameterType))
                            {
                                continue;
                            }

                            violations.Add(
                                $"{handlerType.FullName} injects {parameter.ParameterType.FullName} via {FormatConstructor(constructor)}");
                        }
                    }
                }
            }

            Assert.That(
                violations,
                Is.Empty,
                "Domain event handlers must stay on semantic boundary and cannot inject CQRS runtime/command dispatch surfaces.\n"
                + string.Join("\n", violations));
        }

        private static IEnumerable<Assembly> GetRelevantAssemblies()
        {
            // Framework assembly contains production handlers; this test assembly can contain sample/demo handlers.
            yield return typeof(ICqrsRuntime).Assembly;
            yield return typeof(CqrsArchitectureGuardTests).Assembly;
        }

        private static IEnumerable<Type> GetConcreteDomainEventHandlerTypes(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                if (!ImplementsDomainEventHandler(type))
                {
                    continue;
                }

                yield return type;
            }
        }

        private static bool ImplementsDomainEventHandler(Type type)
        {
            foreach (var @interface in type.GetInterfaces())
            {
                if (!@interface.IsGenericType)
                {
                    continue;
                }

                if (@interface.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsForbiddenDispatchDependency(Type dependencyType)
        {
            foreach (var forbiddenType in ForbiddenDispatchDependencies)
            {
                if (forbiddenType.IsAssignableFrom(dependencyType))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatConstructor(ConstructorInfo constructor)
        {
            var parameters = constructor.GetParameters();
            if (parameters.Length == 0)
            {
                return $"{constructor.Name}()";
            }

            var parameterSignatures = new string[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                parameterSignatures[i] = parameters[i].ParameterType.Name;
            }

            return $"{constructor.Name}({string.Join(", ", parameterSignatures)})";
        }
    }
}
