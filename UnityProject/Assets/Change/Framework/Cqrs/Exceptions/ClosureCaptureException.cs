using System;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Thrown when subscribing a delegate (<see cref="Action{T}"/>) that captures
    /// variables (i.e. <see cref="Delegate.Target"/> is non-null). Capturing delegates
    /// allocate a closure object on the heap, violating the 0-GC dispatch guarantee.
    /// Only static methods or non-capturing lambdas may be subscribed.
    /// </summary>
    public sealed class ClosureCaptureException : InvalidOperationException
    {
        public ClosureCaptureException(string message)
            : base(message)
        {
        }
    }
}
