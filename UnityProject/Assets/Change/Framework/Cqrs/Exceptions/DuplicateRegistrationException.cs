using System;
using System.Runtime.Serialization;

namespace Change.Framework.Cqrs
{
    [Serializable]
    public sealed class DuplicateRegistrationException : InvalidOperationException
    {
        public DuplicateRegistrationException(string message)
            : base(message)
        {
        }

        private DuplicateRegistrationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
