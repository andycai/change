using System;

namespace Change.Framework.Network
{
    public sealed class MockDataInvalidException : InvalidOperationException
    {
        public MockDataInvalidException(int cmdId, string reason)
            : base($"Mock data invalid for cmdId {cmdId}: {reason}")
        {
        }
    }
}
