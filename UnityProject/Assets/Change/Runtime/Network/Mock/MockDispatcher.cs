using System;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class MockDispatcher : IMockDispatcher
    {
        private readonly MockRegistry _registry;
        private readonly IMockDataProvider _dataProvider;
        private readonly IMockValueFactory _valueFactory;

        public MockDispatcher(MockRegistry registry, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
            _valueFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
        }

        public ProtocolEnvelope Dispatch(in ProtocolEnvelope request, in MockRequestContext context)
        {
            if (!_registry.TryGet(request.CmdId, out var handler))
            {
                throw new MockHandlerNotFoundException(request.CmdId);
            }

            var responsePayload = handler.Handle(request.Payload, in context, _dataProvider, _valueFactory);
            return new ProtocolEnvelope(request.CmdId, request.RequestId, responsePayload);
        }
    }
}
