using Change.Framework.UI;
using Change.Runtime.UI.Core;
using NUnit.Framework;

namespace Change.Runtime.UI.Tests
{
    [TestFixture]
    public class WindowRegistryTests
    {
        // Stub implementation for testing the IWindowRegistry contract.
        // Full implementation tests will be added when WindowRegistry is created in task 3.
        private sealed class StubWindowRegistry : Abstractions.IWindowRegistry
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
