using System;
using System.Text;
using Change.Framework.Network;
using Change.Runtime.Network;
using NUnit.Framework;

namespace Change.Runtime.Tests.Network
{
    public class MockRegistryGuardTests
    {
        private sealed class DummyDataProvider : IMockDataProvider
        {
            public byte[] GetTemplate(int cmdId)
            {
                return Encoding.UTF8.GetBytes("ok");
            }
        }

        private sealed class DummyValueFactory : IMockValueFactory
        {
            public bool NextBool()
            {
                return true;
            }

            public int NextInt(int minInclusive, int maxExclusive)
            {
                return minInclusive;
            }

            public float NextFloat(float minInclusive, float maxInclusive)
            {
                return minInclusive;
            }

            public string NextString(int length)
            {
                return new string('a', length);
            }

            public Guid NextGuid()
            {
                return Guid.Empty;
            }

            public DateTimeOffset NextTimeUtc()
            {
                return DateTimeOffset.UnixEpoch;
            }
        }

        private sealed class EchoHandler : IMockHandler
        {
            public int CmdId => 1001;

            public byte[] Handle(byte[] requestPayload, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
            {
                return requestPayload;
            }
        }

        [Test]
        public void Register_DuplicateCmdId_ThrowsDuplicateMockRegistrationException()
        {
            var registry = new MockRegistry();
            registry.Register(new EchoHandler());

            Assert.Throws<DuplicateMockRegistrationException>(() => registry.Register(new EchoHandler()));
        }

        [Test]
        public void Dispatch_WithoutHandler_ThrowsMockHandlerNotFoundException()
        {
            var registry = new MockRegistry();
            var dispatcher = new MockDispatcher(registry, new DummyDataProvider(), new DummyValueFactory());
            var request = new ProtocolEnvelope(9999, 1, Encoding.UTF8.GetBytes("x"));
            var context = new MockRequestContext("dev-default", 9999, 1);

            Assert.Throws<MockHandlerNotFoundException>(() => dispatcher.Dispatch(in request, in context));
        }
    }
}
