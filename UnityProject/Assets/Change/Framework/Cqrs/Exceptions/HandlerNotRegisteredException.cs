using System;

namespace Change.Framework.Cqrs
{
    public sealed class HandlerNotRegisteredException : InvalidOperationException
    {
        public HandlerNotRegisteredException(string message)
            : base(message)
        {
        }
    }
}
