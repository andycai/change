using NUnit.Framework;
using Change.Editor.PSD2UI;

namespace Change.Editor.PSD2UI.Tests
{
    public class ConfigReaderTests
    {
        [Test]
        public void LoadFromJson_WithValidPath_ReturnsUINodeData()
        {
            var reader = new ConfigReader();
            var result = reader.LoadFromJson("test.json");
            Assert.IsNotNull(result);
        }
    }
}
