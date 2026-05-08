using System;
using System.Runtime.Serialization;

namespace Change.Framework.Cqrs
{
    [Serializable]
    public sealed class ContextNotRegisteredException : InvalidOperationException
    {
        public ContextNotRegisteredException(string message)
            : base(message)
        {
        }

        private ContextNotRegisteredException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
