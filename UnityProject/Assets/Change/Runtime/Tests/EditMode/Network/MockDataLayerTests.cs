using Change.Framework.Network;
using Change.Runtime.Network;
using NUnit.Framework;

namespace Change.Runtime.Tests.Network
{
    public class MockDataLayerTests
    {
        [Test]
        public void DeterministicRandom_WithSameSeed_ReturnsSameSequence()
        {
            var first = new DeterministicRandom(42);
            var second = new DeterministicRandom(42);

            Assert.AreEqual(first.NextInt(0, 1000), second.NextInt(0, 1000));
            Assert.AreEqual(first.NextInt(0, 1000), second.NextInt(0, 1000));
        }

        [Test]
        public void DatasetRegistry_UnknownDataset_ThrowsDatasetNotFoundException()
        {
            var registry = new MockDatasetRegistry();

            Assert.Throws<DatasetNotFoundException>(() => registry.Resolve("missing"));
        }

        [Test]
        public void Validator_MissingTemplate_ThrowsMockDataInvalidException()
        {
            var registry = new MockRegistry();
            var dataset = new EmptyDataset("dev-default");

            Assert.Throws<MockDataInvalidException>(() => MockDataValidator.Validate(registry, dataset));
        }

        private sealed class EmptyDataset : IMockDataset
        {
            public EmptyDataset(string datasetId)
            {
                DatasetId = datasetId;
            }

            public string DatasetId { get; }

            public int[] SupportedCmdIds => new[] { 1001 };

            public bool TryGetTemplate(int cmdId, out byte[] payload)
            {
                payload = null;
                return false;
            }
        }
    }
}
