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
            public void Reset(int seed) {}

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

            public int Handle(byte[] buffer, int offset, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
            {
                // In a real echo we'd need the request payload. 
                // IMockHandler.Handle doesn't pass requestPayload anymore for 0GC reasons (usually it's already in a buffer).
                // For this dummy test, just write something.
                buffer[offset] = 42;
                return 1;
            }
        }

        private sealed class RecordingHandler : IMockHandler
        {
            private readonly byte[] _responsePayload;

            public RecordingHandler(int cmdId, byte[] responsePayload)
            {
                CmdId = cmdId;
                _responsePayload = responsePayload;
            }

            public int CmdId { get; }

            public int CallCount { get; private set; }

            public MockRequestContext LastContext { get; private set; }

            public IMockDataProvider LastDataProvider { get; private set; }

            public IMockValueFactory LastValueFactory { get; private set; }

            public int Handle(byte[] buffer, int offset, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
            {
                CallCount++;
                LastContext = context;
                LastDataProvider = dataProvider;
                LastValueFactory = valueFactory;
                Array.Copy(_responsePayload, 0, buffer, offset, _responsePayload.Length);
                return _responsePayload.Length;
            }
        }

        [Test]
        public void Register_NullHandler_ThrowsArgumentNullException()
        {
            var registry = new MockRegistry();

            var exception = Assert.Throws<ArgumentNullException>(() => registry.Register(null));

            Assert.AreEqual("handler", exception.ParamName);
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
            var payload = Encoding.UTF8.GetBytes("x");
            var request = new ProtocolEnvelope(9999, 1, payload, 0, payload.Length);
            var context = new MockRequestContext("dev-default", 9999, 1);

            Assert.Throws<MockHandlerNotFoundException>(() => dispatcher.Dispatch(in request, in context));
        }

        [Test]
        public void Constructor_NullRegistry_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new MockDispatcher(null, new DummyDataProvider(), new DummyValueFactory()));

            Assert.AreEqual("registry", exception.ParamName);
        }

        [Test]
        public void Constructor_NullDataProvider_ThrowsArgumentNullException()
        {
            var registry = new MockRegistry();

            var exception = Assert.Throws<ArgumentNullException>(() => new MockDispatcher(registry, null, new DummyValueFactory()));

            Assert.AreEqual("dataProvider", exception.ParamName);
        }

        [Test]
        public void Constructor_NullValueFactory_ThrowsArgumentNullException()
        {
            var registry = new MockRegistry();

            var exception = Assert.Throws<ArgumentNullException>(() => new MockDispatcher(registry, new DummyDataProvider(), null));

            Assert.AreEqual("valueFactory", exception.ParamName);
        }

        [Test]
        public void Dispatch_WithRegisteredHandler_UsesMatchingHandlerAndReturnsExpectedEnvelope()
        {
            var registry = new MockRegistry();
            var dataProvider = new DummyDataProvider();
            var valueFactory = new DummyValueFactory();
            var targetResponsePayload = Encoding.UTF8.GetBytes("target-response");
            var otherHandler = new RecordingHandler(1002, Encoding.UTF8.GetBytes("other-response"));
            var targetHandler = new RecordingHandler(1001, targetResponsePayload);
            registry.Register(otherHandler);
            registry.Register(targetHandler);
            var dispatcher = new MockDispatcher(registry, dataProvider, valueFactory);
            var requestPayload = Encoding.UTF8.GetBytes("request");
            var request = new ProtocolEnvelope(1001, 42, requestPayload, 0, requestPayload.Length);
            var context = new MockRequestContext("dev-default", 1001, 42);

            var response = dispatcher.Dispatch(in request, in context);

            Assert.AreEqual(0, otherHandler.CallCount);
            Assert.AreEqual(1, targetHandler.CallCount);
            Assert.AreEqual("dev-default", targetHandler.LastContext.DatasetId);
            Assert.AreEqual(1001, targetHandler.LastContext.CmdId);
            Assert.AreEqual(42, targetHandler.LastContext.RequestId);
            Assert.AreSame(dataProvider, targetHandler.LastDataProvider);
            Assert.AreSame(valueFactory, targetHandler.LastValueFactory);
            Assert.AreEqual(1001, response.CmdId);
            Assert.AreEqual(42, response.RequestId);
            
            byte[] actualResponse = new byte[response.Length];
            Array.Copy(response.Payload, response.Offset, actualResponse, 0, response.Length);
            CollectionAssert.AreEqual(targetResponsePayload, actualResponse);
            
            // Clean up
            System.Buffers.ArrayPool<byte>.Shared.Return(response.Payload);
        }
    }
}