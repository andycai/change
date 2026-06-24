using System.IO;
using NUnit.Framework;
using UnityEngine;
using Change.Editor.PSD2UI;

namespace Change.Editor.PSD2UI.Tests
{
    public class ConfigReaderTests
    {
        private static readonly string TestDataPath = Path.Combine(
            Application.dataPath,
            "Change/Editor/PSD2UI/Tests/EditMode/TestData"
        );

        [Test]
        public void LoadFromJson_WithValidPath_ReturnsUINodeData()
        {
            var reader = new ConfigReader();
            string testJsonPath = Path.Combine(TestDataPath, "test.json");
            var result = reader.LoadFromJson(testJsonPath);
            Assert.IsNotNull(result);
        }
    }
}
