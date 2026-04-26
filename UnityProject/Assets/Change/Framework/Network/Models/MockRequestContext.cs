using System;

namespace Change.Framework.Network
{
    public readonly struct MockRequestContext
    {
        public MockRequestContext(string datasetId, int cmdId, int requestId)
        {
            if (string.IsNullOrWhiteSpace(datasetId))
            {
                throw new ArgumentException("Dataset id cannot be empty.", nameof(datasetId));
            }

            DatasetId = datasetId;
            CmdId = cmdId;
            RequestId = requestId;
        }

        public string DatasetId { get; }

        public int CmdId { get; }

        public int RequestId { get; }
    }
}
