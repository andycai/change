using System;
using System.Collections.Generic;
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
            registry.Register(new DummyHandler(1001));
            var dataset = new TestDataset("dev-default", new[] { 1001 }, new Dictionary<int, byte[]>());

            Assert.Throws<MockDataInvalidException>(() => MockDataValidator.Validate(registry, dataset));
        }

        [Test]
        public void Validator_MissingRegisteredHandler_ThrowsMockDataInvalidException()
        {
            var registry = new MockRegistry();
            var dataset = new TestDataset("dev-default", new[] { 1001 }, new Dictionary<int, byte[]>
            {
                { 1001, new byte[] { 1 } },
            });

            Assert.Throws<MockDataInvalidException>(() => MockDataValidator.Validate(registry, dataset));
        }

        [Test]
        public void Validator_HandlerAndTemplateExist_DoesNotThrow()
        {
            var registry = new MockRegistry();
            registry.Register(new DummyHandler(1001));
            var dataset = new TestDataset("dev-default", new[] { 1001 }, new Dictionary<int, byte[]>
            {
                { 1001, new byte[] { 1 } },
            });

            Assert.DoesNotThrow(() => MockDataValidator.Validate(registry, dataset));
        }

        [Test]
        public void DeterministicRandom_NextFloat_WhenMaxLessThanMin_ThrowsArgumentOutOfRangeException()
        {
            var random = new DeterministicRandom(42);

            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextFloat(1.0f, 0.5f));
        }

        private sealed class DummyHandler : IMockHandler
        {
            public DummyHandler(int cmdId)
            {
                CmdId = cmdId;
            }

            public int CmdId { get; }

            public byte[] Handle(byte[] requestPayload, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
            {
                return Array.Empty<byte>();
            }
        }

        private sealed class TestDataset : IMockDataset
        {
            private readonly int[] _supportedCmdIds;
            private readonly Dictionary<int, byte[]> _templates;

            public TestDataset(string datasetId, int[] supportedCmdIds, Dictionary<int, byte[]> templates)
            {
                DatasetId = datasetId;
                _supportedCmdIds = supportedCmdIds;
                _templates = templates;
            }

            public string DatasetId { get; }

            public int[] SupportedCmdIds => _supportedCmdIds;

            public bool TryGetTemplate(int cmdId, out byte[] payload)
            {
                return _templates.TryGetValue(cmdId, out payload);
            }
        }
    }
}
