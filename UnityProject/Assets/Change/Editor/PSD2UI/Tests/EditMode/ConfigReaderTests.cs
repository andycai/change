using System.IO;
using System.Collections.Generic;
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

        [Test]
        public void ValidateConfig_MissingRequiredField_ReturnsFalse()
        {
            var reader = new ConfigReader();
            string invalidJsonPath = Path.Combine(TestDataPath, "invalid.json");
            var isValid = reader.ValidateConfig(invalidJsonPath, out List<string> errors);

            Assert.IsFalse(isValid);
            Assert.IsNotEmpty(errors);
            Assert.That(errors[0], Does.Contain("Name"));
        }
    }
}
