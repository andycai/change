using System;
using System.Runtime.Serialization;

namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Thrown when a command/query type is configured for both Class handler registration
    /// and Struct self-handling (ISelfHandlingCommand / ISelfHandlingQuery&lt;TResult&gt;).
    /// A type must use exactly one mode.
    /// </summary>
    [Serializable]
    public sealed class ModeConflictException : InvalidOperationException
    {
        public ModeConflictException(string message)
            : base(message)
        {
        }

        private ModeConflictException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
