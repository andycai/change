using System;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class LocalMockTransport : ITransport
    {
        private readonly IMockDispatcher _dispatcher;
        private readonly string _datasetId;

        public LocalMockTransport(IMockDispatcher dispatcher, string datasetId)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            if (string.IsNullOrWhiteSpace(datasetId))
            {
                throw new ArgumentException("Dataset id is required.", nameof(datasetId));
            }

            _datasetId = datasetId;
        }

        public ProtocolEnvelope Send(in ProtocolEnvelope request)
        {
            var context = new MockRequestContext(_datasetId, request.CmdId, request.RequestId);
            return _dispatcher.Dispatch(in request, in context);
        }
    }
}
