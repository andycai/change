using System;
using System.Collections.Generic;
using System.Text;
using Change.Framework.Network;
using Change.Runtime.Network;
using NUnit.Framework;

namespace Change.Runtime.Tests.Network
{
    public class NetClientRoutingTests
    {
        private sealed class TestCodec : IMessageCodec
        {
            public byte[] Encode<TMessage>(TMessage message)
            {
                return Encoding.UTF8.GetBytes(message?.ToString() ?? string.Empty);
            }

            public TMessage Decode<TMessage>(byte[] payload)
            {
                return (TMessage)(object)Encoding.UTF8.GetString(payload);
            }
        }

        private sealed class NamedEchoTransport : ITransport
        {
            private readonly string _name;

            public NamedEchoTransport(string name)
            {
                _name = name;
            }

            public int CallCount { get; private set; }

            public ProtocolEnvelope Send(in ProtocolEnvelope request)
            {
                CallCount++;
                var input = Encoding.UTF8.GetString(request.Payload);
                var output = Encoding.UTF8.GetBytes($"{_name}:{input}");
                return new ProtocolEnvelope(request.CmdId, request.RequestId, output);
            }
        }

        private sealed class CapturingTransport : ITransport
        {
            private readonly List<ProtocolEnvelope> _requests = new List<ProtocolEnvelope>();

            public IReadOnlyList<ProtocolEnvelope> Requests => _requests;

            public ProtocolEnvelope Send(in ProtocolEnvelope request)
            {
                _requests.Add(request);
                return new ProtocolEnvelope(request.CmdId, request.RequestId, request.Payload);
            }
        }

        private sealed class CapturingDispatcher : IMockDispatcher
        {
            public CapturingDispatcher(ProtocolEnvelope responseEnvelope)
            {
                ResponseEnvelope = responseEnvelope;
            }

            public int CallCount { get; private set; }

            public MockRequestContext LastContext { get; private set; }

            public ProtocolEnvelope ResponseEnvelope { get; }

            public ProtocolEnvelope Dispatch(in ProtocolEnvelope request, in MockRequestContext context)
            {
                CallCount++;
                LastContext = context;
                return ResponseEnvelope;
            }
        }

        private sealed class ThrowingCodec : IMessageCodec
        {
            private readonly Exception _encodeError;
            private readonly Exception _decodeError;

            public ThrowingCodec(Exception encodeError, Exception decodeError)
            {
                _encodeError = encodeError;
                _decodeError = decodeError;
            }

            public byte[] Encode<TMessage>(TMessage message)
            {
                if (_encodeError != null)
                {
                    throw _encodeError;
                }

                return Encoding.UTF8.GetBytes(message?.ToString() ?? string.Empty);
            }

            public TMessage Decode<TMessage>(byte[] payload)
            {
                if (_decodeError != null)
                {
                    throw _decodeError;
                }

                return (TMessage)(object)Encoding.UTF8.GetString(payload);
            }
        }

        private sealed class InvalidRoutePolicy : IRoutePolicy
        {
            public RouteTarget Resolve(int cmdId)
            {
                return (RouteTarget)255;
            }
        }

        [Test]
        public void Send_WithAllMockPolicy_UsesMockTransport()
        {
            var mock = new NamedEchoTransport("mock");
            var ws = new NamedEchoTransport("ws");
            var tcp = new NamedEchoTransport("tcp");
            var router = new TransportRouter(mock, ws, tcp);
            var client = new NetClient(new TestCodec(), new AllMockRoutePolicy(), router, "dev-default");

            var response = client.Send<string, string>(1001, "hello");

            Assert.AreEqual("mock:hello", response);
            Assert.AreEqual(1, mock.CallCount);
            Assert.AreEqual(0, ws.CallCount);
            Assert.AreEqual(0, tcp.CallCount);
        }

        [Test]
        public void Send_WithNegativeCmdId_ThrowsArgumentOutOfRangeException()
        {
            var router = new TransportRouter(new NamedEchoTransport("mock"), new NamedEchoTransport("ws"), new NamedEchoTransport("tcp"));
            var client = new NetClient(new TestCodec(), new AllMockRoutePolicy(), router, "dev-default");

            Assert.Throws<ArgumentOutOfRangeException>(() => client.Send<string, string>(-1, "x"));
        }

        [Test]
        public void LocalMockTransport_Send_InvokesDispatcherOnce_WithExpectedContext_AndPropagatesEnvelope()
        {
            var responsePayload = Encoding.UTF8.GetBytes("mock-response");
            var responseEnvelope = new ProtocolEnvelope(2001, 88, responsePayload);
            var dispatcher = new CapturingDispatcher(responseEnvelope);
            var transport = new LocalMockTransport(dispatcher, "dev-default");
            var requestEnvelope = new ProtocolEnvelope(1001, 42, Encoding.UTF8.GetBytes("request"));

            var actualResponse = transport.Send(in requestEnvelope);

            Assert.AreEqual(1, dispatcher.CallCount);
            Assert.AreEqual("dev-default", dispatcher.LastContext.DatasetId);
            Assert.AreEqual(1001, dispatcher.LastContext.CmdId);
            Assert.AreEqual(42, dispatcher.LastContext.RequestId);
            Assert.AreEqual(responseEnvelope.CmdId, actualResponse.CmdId);
            Assert.AreEqual(responseEnvelope.RequestId, actualResponse.RequestId);
            Assert.AreSame(responseEnvelope.Payload, actualResponse.Payload);
        }

        [Test]
        public void Resolve_WithRealWebSocketTarget_ReturnsWebSocketTransport()
        {
            var mock = new NamedEchoTransport("mock");
            var ws = new NamedEchoTransport("ws");
            var tcp = new NamedEchoTransport("tcp");
            var router = new TransportRouter(mock, ws, tcp);

            var resolved = router.Resolve(RouteTarget.RealWebSocket);

            Assert.AreSame(ws, resolved);
        }

        [Test]
        public void Resolve_WithRealTcpTarget_ReturnsTcpTransport()
        {
            var mock = new NamedEchoTransport("mock");
            var ws = new NamedEchoTransport("ws");
            var tcp = new NamedEchoTransport("tcp");
            var router = new TransportRouter(mock, ws, tcp);

            var resolved = router.Resolve(RouteTarget.RealTcp);

            Assert.AreSame(tcp, resolved);
        }

        [Test]
        public void Resolve_WithUnsupportedTarget_ThrowsUnsupportedRouteException()
        {
            var router = new TransportRouter(new NamedEchoTransport("mock"), new NamedEchoTransport("ws"), new NamedEchoTransport("tcp"));

            Assert.Throws<UnsupportedRouteException>(() => router.Resolve((RouteTarget)255));
        }

        [Test]
        public void Send_MultipleCalls_AssignsIncrementingRequestIds()
        {
            var capturingTransport = new CapturingTransport();
            var router = new TransportRouter(capturingTransport, new NamedEchoTransport("ws"), new NamedEchoTransport("tcp"));
            var client = new NetClient(new TestCodec(), new AllMockRoutePolicy(), router, "dev-default");

            client.Send<string, string>(1001, "first");
            client.Send<string, string>(1002, "second");

            Assert.AreEqual(2, capturingTransport.Requests.Count);
            Assert.AreEqual(1, capturingTransport.Requests[0].RequestId);
            Assert.AreEqual(2, capturingTransport.Requests[1].RequestId);
            Assert.AreEqual(1001, capturingTransport.Requests[0].CmdId);
            Assert.AreEqual(1002, capturingTransport.Requests[1].CmdId);
        }

        [Test]
        public void Send_WithUnsupportedRouteTarget_ThrowsUnsupportedRouteException()
        {
            var router = new TransportRouter(new NamedEchoTransport("mock"), new NamedEchoTransport("ws"), new NamedEchoTransport("tcp"));
            var client = new NetClient(new TestCodec(), new InvalidRoutePolicy(), router, "dev-default");

            Assert.Throws<UnsupportedRouteException>(() => client.Send<string, string>(1001, "x"));
        }

        [Test]
        public void Send_WhenEncodeFails_WrapsCodecOperationExceptionWithCmdAndRequestContext()
        {
            var router = new TransportRouter(new NamedEchoTransport("mock"), new NamedEchoTransport("ws"), new NamedEchoTransport("tcp"));
            var inner = new InvalidOperationException("encode boom");
            var client = new NetClient(new ThrowingCodec(inner, null), new AllMockRoutePolicy(), router, "dev-default");

            var exception = Assert.Throws<CodecOperationException>(() => client.Send<string, string>(4123, "x"));

            Assert.That(exception.Message, Does.Contain("cmdId 4123"));
            Assert.That(exception.Message, Does.Contain("requestId 1"));
            Assert.AreSame(inner, exception.InnerException);
        }

        [Test]
        public void Send_WhenDecodeFails_WrapsCodecOperationExceptionWithCmdAndRequestContext()
        {
            var router = new TransportRouter(new NamedEchoTransport("mock"), new NamedEchoTransport("ws"), new NamedEchoTransport("tcp"));
            var inner = new FormatException("decode boom");
            var client = new NetClient(new ThrowingCodec(null, inner), new AllMockRoutePolicy(), router, "dev-default");

            var exception = Assert.Throws<CodecOperationException>(() => client.Send<string, string>(5123, "x"));

            Assert.That(exception.Message, Does.Contain("cmdId 5123"));
            Assert.That(exception.Message, Does.Contain("requestId 1"));
            Assert.AreSame(inner, exception.InnerException);
        }
    }
}
