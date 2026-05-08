using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;

namespace Change.Runtime.Tests.Gas
{
    public class AbilityTemplateRegistryContractTests
    {
        private const string RegistryTypeName = "GameScript.GasTemplate.AbilityTemplateRegistry";
        private const string BuildContextTypeName = "GameScript.GasTemplate.TemplateBuildContext";

        [Test]
        public void Register_DuplicateKey_ThrowsInvalidOperationException()
        {
            var registryType = RequireType(RegistryTypeName);
            var contextType = RequireType(BuildContextTypeName);
            var registry = Activator.CreateInstance(registryType);
            var registerMethod = RequireMethod(registryType, "Register", typeof(string), typeof(Func<,>).MakeGenericType(contextType, typeof(object)));
            var factory = CreateConstantFactory(contextType, "template-a");

            registerMethod.Invoke(registry, new object[] { "fireball", factory });

            var ex = Assert.Throws<TargetInvocationException>(() => registerMethod.Invoke(registry, new object[] { "fireball", factory }));
            Assert.IsNotNull(ex);
            Assert.IsNotNull(ex.InnerException);
            Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException);
            Assert.IsTrue(ex.InnerException.Message.Contains("fireball"));
        }

        [Test]
        public void Build_MissingKey_ThrowsInvalidOperationException()
        {
            var registryType = RequireType(RegistryTypeName);
            var contextType = RequireType(BuildContextTypeName);
            var registry = Activator.CreateInstance(registryType);
            var buildMethod = RequireMethod(registryType, "Build", typeof(string), contextType);
            var context = Activator.CreateInstance(contextType);

            var ex = Assert.Throws<TargetInvocationException>(() => buildMethod.Invoke(registry, new[] { (object)"missing", context }));
            Assert.IsNotNull(ex);
            Assert.IsNotNull(ex.InnerException);
            Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException);
            Assert.IsTrue(ex.InnerException.Message.Contains("missing"));
        }

        private static Delegate CreateConstantFactory(Type contextType, object value)
        {
            var parameter = Expression.Parameter(contextType, "context");
            var body = Expression.Constant(value, typeof(object));
            var delegateType = typeof(Func<,>).MakeGenericType(contextType, typeof(object));
            return Expression.Lambda(delegateType, body, parameter).Compile();
        }

        private static Type RequireType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(x => x.GetType(fullName, throwOnError: false))
                .FirstOrDefault(x => x != null);

            Assert.IsNotNull(type, $"Type `{fullName}` was not found in loaded assemblies.");
            return type;
        }

        private static System.Reflection.MethodInfo RequireMethod(Type owner, string name, params Type[] parameters)
        {
            var method = owner.GetMethod(name, parameters);
            Assert.IsNotNull(method, $"Method `{owner.FullName}.{name}` was not found.");
            return method;
        }
    }
}
