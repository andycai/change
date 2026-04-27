using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Change.Runtime.UI.Tests
{
    public class NoReflectionBindingGuardTests
    {
        [Test]
        public void RuntimeUi_MustNotUseReflectionBindingApis()
        {
            var root = Path.Combine(Application.dataPath, "Change/Runtime/UI");
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            var content = files.Select(File.ReadAllText).ToArray();

            Assert.Greater(files.Length, 0, "Expected Runtime/UI to contain C# files for guard scanning.");
            Assert.IsFalse(content.Any(x => x.Contains("System.Reflection")));
            Assert.IsFalse(content.Any(x => x.Contains(".GetType(")));
            Assert.IsFalse(content.Any(x => x.Contains("Type.GetType(")));
            Assert.IsFalse(content.Any(x => x.Contains("PropertyInfo")));
        }
    }
}
