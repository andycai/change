using System;

namespace Change.Framework.Network
{
    public readonly struct ProtocolEnvelope
    {
        public ProtocolEnvelope(int cmdId, int requestId, byte[] payload, int offset, int length)
        {
            if (cmdId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cmdId));
            }

            if (requestId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestId));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            
            if (offset + length > payload.Length)
            {
                throw new ArgumentException("Offset and length exceed payload bounds.");
            }

            CmdId = cmdId;
            RequestId = requestId;
            Offset = offset;
            Length = length;
        }

        public int CmdId { get; }

        public int RequestId { get; }

        public byte[] Payload { get; }

        public int Offset { get; }

        public int Length { get; }
    }
}
