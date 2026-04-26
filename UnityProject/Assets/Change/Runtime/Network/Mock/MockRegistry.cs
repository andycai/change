using System;
using Change.Framework.Collections;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class MockRegistry
    {
        private readonly FastDictionary<int, IMockHandler> _handlers = new FastDictionary<int, IMockHandler>();

        public void Register(IMockHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!_handlers.TryAdd(handler.CmdId, handler))
            {
                throw new DuplicateMockRegistrationException(handler.CmdId);
            }
        }

        public bool TryGet(int cmdId, out IMockHandler handler)
        {
            return _handlers.TryGetValue(cmdId, out handler);
        }

        public void ForEachRegistered(Action<int, IMockHandler> visitor)
        {
            if (visitor == null)
            {
                throw new ArgumentNullException(nameof(visitor));
            }

            _handlers.ForEach(visitor);
        }
    }
}
