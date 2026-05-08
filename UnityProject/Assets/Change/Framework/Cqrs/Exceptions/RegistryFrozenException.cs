using System;
using System.Runtime.Serialization;

namespace Change.Framework.Cqrs
{
    [Serializable]
    public sealed class RegistryFrozenException : InvalidOperationException
    {
        public RegistryFrozenException(string message)
            : base(message)
        {
        }

        private RegistryFrozenException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
