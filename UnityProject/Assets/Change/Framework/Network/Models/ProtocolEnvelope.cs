using System;

namespace Change.Framework.Network
{
    public readonly struct ProtocolEnvelope
    {
        public ProtocolEnvelope(int cmdId, int requestId, byte[] payload)
        {
            if (cmdId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cmdId));
            }

            if (requestId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestId));
            }

            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            CmdId = cmdId;
            RequestId = requestId;
        }

        public int CmdId { get; }

        public int RequestId { get; }

        public byte[] Payload { get; }
    }
}
