using System;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class TransportRouter
    {
        private readonly ITransport _mockTransport;
        private readonly ITransport _webSocketTransport;
        private readonly ITransport _tcpTransport;

        public TransportRouter(ITransport mockTransport, ITransport webSocketTransport, ITransport tcpTransport)
        {
            _mockTransport = mockTransport ?? throw new ArgumentNullException(nameof(mockTransport));
            _webSocketTransport = webSocketTransport ?? throw new ArgumentNullException(nameof(webSocketTransport));
            _tcpTransport = tcpTransport ?? throw new ArgumentNullException(nameof(tcpTransport));
        }

        public ITransport Resolve(RouteTarget target)
        {
            switch (target)
            {
                case RouteTarget.Mock:
                    return _mockTransport;
                case RouteTarget.RealWebSocket:
                    return _webSocketTransport;
                case RouteTarget.RealTcp:
                    return _tcpTransport;
                default:
                    throw new MockDataInvalidException(-1, $"Unsupported route target: {target}");
            }
        }
    }
}
