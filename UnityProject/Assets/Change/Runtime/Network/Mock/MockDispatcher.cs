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
            
            // Re-using request payload buffer for response if possible, or using a temporary pool buffer.
            // For simplicity in this mock, we'll assume the buffer passed to Handle is large enough.
            // In a real system, we'd use a pooled buffer.
            var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(64 * 1024); // 64KB max for mock response
            try
            {
                var written = handler.Handle(buffer, 0, in context, _dataProvider, valueFactory);
                return new ProtocolEnvelope(request.CmdId, request.RequestId, buffer, 0, written);
            }
            finally
            {
                // Note: The caller of Dispatch needs to know when to return the buffer to the pool.
                // This is a trade-off. To be truly 0GC, ProtocolEnvelope might need to be disposable or 
                // the lifecycle managed more strictly. 
                // For now, I'll keep the buffer in the envelope and assume the consumer returns it.
                // Wait, if I return it here, the envelope becomes invalid.
                // I'll change ProtocolEnvelope to not "own" the buffer but just point to it.
            }
        }

        private IMockValueFactory ResolveValueFactory(in MockRequestContext context)
        {
            if (_useRequestScopedDefaultFactory)
            {
                _valueFactory.Reset(CreateSeed(in context));
            }

            return _valueFactory;
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
