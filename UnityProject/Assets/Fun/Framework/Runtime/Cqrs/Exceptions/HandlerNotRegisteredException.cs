using System;

namespace Fun.Framework.Cqrs
{
    public sealed class HandlerNotRegisteredException : InvalidOperationException
    {
        public HandlerNotRegisteredException(string message)
            : base(message)
        {
        }
    }
}
