using System;

namespace Change.Framework.Network
{
    public sealed class CodecOperationException : InvalidOperationException
    {
        public CodecOperationException(string operation, int cmdId, int requestId, Exception inner)
            : base($"Codec {operation} failed for cmdId {cmdId}, requestId {requestId}.", inner)
        {
        }
    }
}
