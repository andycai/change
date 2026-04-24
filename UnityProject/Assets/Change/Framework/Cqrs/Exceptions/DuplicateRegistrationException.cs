using System;

namespace Change.Framework.Cqrs
{
    public sealed class DuplicateRegistrationException : InvalidOperationException
    {
        public DuplicateRegistrationException(string message)
            : base(message)
        {
        }
    }
}
