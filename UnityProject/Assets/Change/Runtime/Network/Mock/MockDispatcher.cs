using System;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class MockDispatcher : IMockDispatcher
    {
        private readonly MockRegistry _registry;
        private readonly IMockDataProvider _dataProvider;
        private readonly IMockValueFactory _valueFactory;
        private readonly bool _useRequestScopedDefaultFactory;

        public MockDispatcher(MockRegistry registry, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
            _valueFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
            _useRequestScopedDefaultFactory = valueFactory is DefaultMockValueFactory;
        }

        public ProtocolEnvelope Dispatch(in ProtocolEnvelope request, in MockRequestContext context)
        {
            if (!_registry.TryGet(request.CmdId, out var handler))
            {
                throw new MockHandlerNotFoundException(request.CmdId);
            }

            var valueFactory = ResolveValueFactory(in context);
            var responsePayload = handler.Handle(request.Payload, in context, _dataProvider, valueFactory);
            return new ProtocolEnvelope(request.CmdId, request.RequestId, responsePayload);
        }

        private IMockValueFactory ResolveValueFactory(in MockRequestContext context)
        {
            if (!_useRequestScopedDefaultFactory)
            {
                return _valueFactory;
            }

            return new DefaultMockValueFactory(new DeterministicRandom(CreateSeed(in context)));
        }

        private static int CreateSeed(in MockRequestContext context)
        {
            unchecked
            {
                const int offset = unchecked((int)2166136261);
                const int prime = 16777619;

                var hash = offset;
                var datasetId = context.DatasetId;
                for (var i = 0; i < datasetId.Length; i++)
                {
                    hash ^= datasetId[i];
                    hash *= prime;
                }

                hash ^= context.CmdId;
                hash *= prime;
                hash ^= context.RequestId;
                hash *= prime;
                return hash;
            }
        }
    }
}
