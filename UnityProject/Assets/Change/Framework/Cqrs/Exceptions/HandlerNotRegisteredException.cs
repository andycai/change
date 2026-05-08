using System;
using System.Runtime.Serialization;

namespace Change.Framework.Cqrs
{
    [Serializable]
    public sealed class HandlerNotRegisteredException : InvalidOperationException
    {
        public HandlerNotRegisteredException(string message)
            : base(message)
        {
        }

        private HandlerNotRegisteredException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
