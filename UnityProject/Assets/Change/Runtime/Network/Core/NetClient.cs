using System;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class NetClient : INetClient
    {
        private readonly IMessageCodec _codec;
        private readonly IRoutePolicy _routePolicy;
        private readonly TransportRouter _router;
        private readonly string _datasetId;
        private int _nextRequestId;

        public NetClient(IMessageCodec codec, IRoutePolicy routePolicy, TransportRouter router, string datasetId)
        {
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _routePolicy = routePolicy ?? throw new ArgumentNullException(nameof(routePolicy));
            _router = router ?? throw new ArgumentNullException(nameof(router));

            if (string.IsNullOrWhiteSpace(datasetId))
            {
                throw new ArgumentException("Dataset id is required.", nameof(datasetId));
            }

            _datasetId = datasetId;
        }

        public string DatasetId => _datasetId;

        public TResponse Send<TRequest, TResponse>(int cmdId, TRequest request)
        {
            if (cmdId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cmdId));
            }

            var requestId = ++_nextRequestId;
            var payload = _codec.Encode(request);
            var requestEnvelope = new ProtocolEnvelope(cmdId, requestId, payload);
            var target = _routePolicy.Resolve(cmdId);
            var transport = _router.Resolve(target);
            var responseEnvelope = transport.Send(in requestEnvelope);
            return _codec.Decode<TResponse>(responseEnvelope.Payload);
        }
    }
}
