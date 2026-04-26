using System;

namespace Change.Framework.Network
{
    public sealed class DuplicateMockRegistrationException : InvalidOperationException
    {
        public DuplicateMockRegistrationException(int cmdId)
            : base($"Mock handler already registered for cmdId: {cmdId}.")
        {
        }
    }
}
