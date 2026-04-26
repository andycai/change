using System;
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
    }
}
