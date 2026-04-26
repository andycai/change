using System;

namespace Change.Framework.Network
{
    public sealed class MockHandlerNotFoundException : InvalidOperationException
    {
        public MockHandlerNotFoundException(int cmdId)
            : base($"Mock handler not found for cmdId: {cmdId}.")
        {
        }
    }
}
