using System;
using System.Buffers;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class NetClient : INetClient
    {
        private const int MaxPacketSize = 1024 * 1024; // 1MB limit
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
            
            // Rent buffer for request payload
            var requestBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024); // Start with 64KB
            try
            {
                int requestLength;
                try
                {
                    requestLength = _codec.Encode(request, requestBuffer, 0, cmdId);
                }
                catch (Exception ex)
                {
                    throw WrapCodecFailure("encode", cmdId, requestId, ex);
                }

                if (requestLength > MaxPacketSize)
                {
                    throw new InvalidOperationException($"Request packet size {requestLength} exceeds maximum limit of {MaxPacketSize}");
                }

                var requestEnvelope = new ProtocolEnvelope(cmdId, requestId, requestBuffer, 0, requestLength);
                var target = _routePolicy.Resolve(cmdId);
                var transport = _router.Resolve(target);
                
                // TODO: Add timeout logic around transport.Send if needed
                var responseEnvelope = transport.Send(in requestEnvelope);
                
                try
                {
                    if (responseEnvelope.Length > MaxPacketSize)
                    {
                        throw new InvalidOperationException($"Response packet size {responseEnvelope.Length} exceeds maximum limit of {MaxPacketSize}");
                    }

                    return _codec.Decode<TResponse>(responseEnvelope.Payload, responseEnvelope.Offset, responseEnvelope.Length, cmdId);
                }
                catch (Exception ex)
                {
                    throw WrapCodecFailure("decode", cmdId, requestId, ex);
                }
                finally
                {
                    // Return response buffer if it's from pool and NOT the request buffer we just sent.
                    // In real transports, response buffer is always separate.
                    // In some mock transports, they might echo the request buffer.
                    if (responseEnvelope.Payload != null && responseEnvelope.Payload != requestBuffer)
                    {
                        // Note: We assume transports that return pooled buffers use the same Shared pool.
                        // This is a common convention in this project for 0GC.
                        ArrayPool<byte>.Shared.Return(responseEnvelope.Payload);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(requestBuffer);
            }
        }

        private static CodecOperationException WrapCodecFailure(string operation, int cmdId, int requestId, Exception error)
        {
            if (error is CodecOperationException codecError && codecError.InnerException != null)
            {
                return new CodecOperationException(operation, cmdId, requestId, codecError.InnerException);
            }

            return new CodecOperationException(operation, cmdId, requestId, error);
        }
    }
}
