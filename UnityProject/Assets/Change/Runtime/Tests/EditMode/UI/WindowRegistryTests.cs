using System;
using Change.Framework.UI;
using Change.Runtime.UI.Core;
using NUnit.Framework;

namespace Change.Runtime.UI.Tests
{
    public class WindowRegistryTests
    {
        [Test]
        public void Register_AndTryGetMetadata_ReturnsTrue()
        {
            var registry = new WindowRegistry();
            var id = new WindowId("TestWindow");

            registry.Register(id, "TestPackage", "TestComponent", "TestGroup", WindowLayer.Normal);

            bool found = registry.TryGetMetadata(id, out var metadata);

            Assert.IsTrue(found);
            Assert.AreEqual("TestPackage", metadata.PackageName);
            Assert.AreEqual("TestComponent", metadata.ComponentName);
            Assert.AreEqual("TestGroup", metadata.Group);
            Assert.AreEqual(WindowLayer.Normal, metadata.Layer);
        }

        [Test]
        public void TryGetMetadata_NotRegistered_ReturnsFalse()
        {
            var registry = new WindowRegistry();
            var id = new WindowId("NonExistent");

            bool found = registry.TryGetMetadata(id, out var metadata);

            Assert.IsFalse(found);
        }

        [Test]
        public void Register_DuplicateId_ThrowsException()
        {
            var registry = new WindowRegistry();
            var id = new WindowId("TestWindow");

            registry.Register(id, "Pkg1", "Comp1", "Group1", WindowLayer.Normal);

            Assert.Throws<InvalidOperationException>(() =>
                registry.Register(id, "Pkg2", "Comp2", "Group2", WindowLayer.Popup));
        }

        [Test]
        public void Register_NullGroup_StoresNull()
        {
            var registry = new WindowRegistry();
            var id = new WindowId("OverlayWindow");

            registry.Register(id, "Pkg", "Comp", null, WindowLayer.Top);

            bool found = registry.TryGetMetadata(id, out var metadata);

            Assert.IsTrue(found);
            Assert.IsNull(metadata.Group);
        }
    }
}
