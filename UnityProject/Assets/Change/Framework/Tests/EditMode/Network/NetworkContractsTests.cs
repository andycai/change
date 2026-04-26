using System;
using Change.Framework.Network;
using NUnit.Framework;

namespace Change.Framework.Tests.Network
{
    public class NetworkContractsTests
    {
        [Test]
        public void ProtocolEnvelope_RejectsNegativeCmdId()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new ProtocolEnvelope(-1, 1, Array.Empty<byte>()));
        }

        [Test]
        public void ProtocolEnvelope_RejectsNullPayload()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _ = new ProtocolEnvelope(1001, 1, null));
        }

        [Test]
        public void MockRequestContext_RejectsEmptyDatasetId()
        {
            Assert.Throws<ArgumentException>(() =>
                _ = new MockRequestContext(string.Empty, 1001, 1));
        }

        [Test]
        public void RouteTarget_HasExpectedStableValues()
        {
            Assert.AreEqual(0, (int)RouteTarget.Mock);
            Assert.AreEqual(1, (int)RouteTarget.RealWebSocket);
            Assert.AreEqual(2, (int)RouteTarget.RealTcp);
        }

        [Test]
        public void Exceptions_AreInvalidOperationBased()
        {
            Assert.IsTrue(typeof(InvalidOperationException)
                .IsAssignableFrom(typeof(MockHandlerNotFoundException)));
            Assert.IsTrue(typeof(InvalidOperationException)
                .IsAssignableFrom(typeof(DuplicateMockRegistrationException)));
            Assert.IsTrue(typeof(InvalidOperationException)
                .IsAssignableFrom(typeof(DatasetNotFoundException)));
            Assert.IsTrue(typeof(InvalidOperationException)
                .IsAssignableFrom(typeof(MockDataInvalidException)));
            Assert.IsTrue(typeof(InvalidOperationException)
                .IsAssignableFrom(typeof(CodecOperationException)));
        }
    }
}
