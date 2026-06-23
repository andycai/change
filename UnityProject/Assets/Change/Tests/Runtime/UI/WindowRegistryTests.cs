using Change.Framework.UI;
using Change.Runtime.UI;
using NUnit.Framework;

namespace Change.Runtime.Tests.UI
{
    [TestFixture]
    public class WindowRegistryTests
    {
        // Stub implementation for testing the IWindowRegistry contract
        private sealed class StubWindowRegistry : IWindowRegistry
        {
            public void Register(WindowId id, string packageName, string componentName, string group, WindowLayer layer) { }
            public bool TryGetMetadata(WindowId id, out WindowMetadata metadata)
            {
                metadata = default;
                return false;
            }
        }

        [Test]
        public void Register_AcceptsValidParameters()
        {
            var registry = new StubWindowRegistry();
            var id = new WindowId("test_window");

            Assert.DoesNotThrow(() =>
                registry.Register(id, "TestPackage", "TestComponent", "Default", WindowLayer.Normal));
        }

        [Test]
        public void TryGetMetadata_ReturnsFalse_ForUnregisteredWindow()
        {
            var registry = new StubWindowRegistry();
            var id = new WindowId("unknown_window");

            var found = registry.TryGetMetadata(id, out var metadata);

            Assert.IsFalse(found);
            Assert.AreEqual(default(WindowMetadata), metadata);
        }
    }
}
