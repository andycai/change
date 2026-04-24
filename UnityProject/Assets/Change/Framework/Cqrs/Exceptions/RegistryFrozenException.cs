using System;

namespace Change.Framework.Cqrs
{
    public sealed class RegistryFrozenException : InvalidOperationException
    {
        public RegistryFrozenException(string message)
            : base(message)
        {
        }
    }
}
