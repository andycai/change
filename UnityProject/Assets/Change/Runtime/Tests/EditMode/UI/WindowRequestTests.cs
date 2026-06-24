using Change.Framework.UI;
using Change.Runtime.UI;
using NUnit.Framework;

namespace Change.Runtime.UI.Tests
{
    public class WindowRequestTests
    {
        [Test]
        public void Equals_SameIdAndContext_ReturnsTrue()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions(WindowLayer.Normal, allowMultipleInstances: true, instanceId: 1);
            var context = new { ItemId = 123 };

            var req1 = new WindowRequest(id, options, "Group", context);
            var req2 = new WindowRequest(id, options, "Group", context);

            Assert.IsTrue(req1.Equals(req2));
            Assert.AreEqual(req1.GetHashCode(), req2.GetHashCode());
        }

        [Test]
        public void Equals_SameIdDifferentContext_ReturnsFalse()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions(WindowLayer.Normal, allowMultipleInstances: true, instanceId: 1);

            var req1 = new WindowRequest(id, options, "Group", new { ItemId = 123 });
            var req2 = new WindowRequest(id, options, "Group", new { ItemId = 456 });

            Assert.IsFalse(req1.Equals(req2));
        }

        [Test]
        public void Equals_ContextNull_HandlesCorrectly()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions(WindowLayer.Normal, allowMultipleInstances: true, instanceId: 1);

            var req1 = new WindowRequest(id, options, "Group", null);
            var req2 = new WindowRequest(id, options, "Group", null);

            Assert.IsTrue(req1.Equals(req2));
            Assert.AreEqual(req1.GetHashCode(), req2.GetHashCode());
        }

        [Test]
        public void Equals_OneContextNullOneNot_ReturnsFalse()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions(WindowLayer.Normal, allowMultipleInstances: true, instanceId: 1);

            var req1 = new WindowRequest(id, options, "Group", null);
            var req2 = new WindowRequest(id, options, "Group", new { ItemId = 123 });

            Assert.IsFalse(req1.Equals(req2));
        }

        [Test]
        public void Equals_DisallowMultipleInstances_IgnoresContext()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions(WindowLayer.Normal, allowMultipleInstances: false);

            var req1 = new WindowRequest(id, options, "Group", new { ItemId = 123 });
            var req2 = new WindowRequest(id, options, "Group", new { ItemId = 456 });

            Assert.IsTrue(req1.Equals(req2));
        }

        [Test]
        public void Equals_GroupDoesNotParticipate()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions(WindowLayer.Normal, allowMultipleInstances: true, instanceId: 1);

            var req1 = new WindowRequest(id, options, "Group1", 123);
            var req2 = new WindowRequest(id, options, "Group2", 123);

            Assert.IsTrue(req1.Equals(req2));
        }

        [Test]
        public void GetHashCode_ValueTypeContext_WorksCorrectly()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions(WindowLayer.Normal, allowMultipleInstances: true, instanceId: 1);

            var req1 = new WindowRequest(id, options, "Group", 123);
            var req2 = new WindowRequest(id, options, "Group", 123);

            Assert.AreEqual(req1.GetHashCode(), req2.GetHashCode());
        }

        [Test]
        public void GetHashCode_StringContext_WorksCorrectly()
        {
            var id = new WindowId("Test");
            var options = new WindowOpenOptions(WindowLayer.Normal, allowMultipleInstances: true, instanceId: 1);

            var req1 = new WindowRequest(id, options, "Group", "context-string");
            var req2 = new WindowRequest(id, options, "Group", "context-string");

            Assert.AreEqual(req1.GetHashCode(), req2.GetHashCode());
        }
    }
}
