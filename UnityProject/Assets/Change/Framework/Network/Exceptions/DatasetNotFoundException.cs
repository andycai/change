using System;

namespace Change.Framework.Network
{
    public sealed class DatasetNotFoundException : InvalidOperationException
    {
        public DatasetNotFoundException(string datasetId)
            : base($"Mock dataset not found: {datasetId}.")
        {
        }
    }
}
