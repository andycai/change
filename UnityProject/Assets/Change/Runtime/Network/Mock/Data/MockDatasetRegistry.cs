using System;
using System.Collections.Generic;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class MockDatasetRegistry
    {
        private readonly Dictionary<string, IMockDataset> _datasets = new Dictionary<string, IMockDataset>(StringComparer.Ordinal);

        public void Register(IMockDataset dataset)
        {
            if (dataset == null)
            {
                throw new ArgumentNullException(nameof(dataset));
            }

            if (string.IsNullOrWhiteSpace(dataset.DatasetId))
            {
                throw new MockDataInvalidException(-1, "Dataset id is required.");
            }

            if (!_datasets.TryAdd(dataset.DatasetId, dataset))
            {
                throw new MockDataInvalidException(-1, $"Duplicate datasetId: {dataset.DatasetId}");
            }
        }

        public IMockDataset Resolve(string datasetId)
        {
            if (string.IsNullOrWhiteSpace(datasetId))
            {
                throw new ArgumentException("Dataset id is required.", nameof(datasetId));
            }

            if (!_datasets.TryGetValue(datasetId, out var dataset))
            {
                throw new DatasetNotFoundException(datasetId);
            }

            return dataset;
        }
    }
}
