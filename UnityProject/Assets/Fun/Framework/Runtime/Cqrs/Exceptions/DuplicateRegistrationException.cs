using System;

namespace Fun.Framework.Cqrs
{
    public sealed class DuplicateRegistrationException : InvalidOperationException
    {
        public DuplicateRegistrationException(string message)
            : base(message)
        {
        }
    }
}
