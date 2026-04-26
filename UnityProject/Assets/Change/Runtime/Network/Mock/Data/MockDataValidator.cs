using System.Collections.Generic;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public static class MockDataValidator
    {
        public static void Validate(MockRegistry registry, IMockDataset dataset)
        {
            if (registry == null)
            {
                throw new MockDataInvalidException(-1, "Registry cannot be null.");
            }

            if (dataset == null)
            {
                throw new MockDataInvalidException(-1, "Dataset cannot be null.");
            }

            var supportedCmdIds = dataset.SupportedCmdIds;
            if (supportedCmdIds == null)
            {
                throw new MockDataInvalidException(-1, "Supported cmd ids cannot be null.");
            }

            var supportedLookup = new HashSet<int>();
            for (var i = 0; i < supportedCmdIds.Length; i++)
            {
                var cmdId = supportedCmdIds[i];
                if (!supportedLookup.Add(cmdId))
                {
                    throw new MockDataInvalidException(cmdId, $"Duplicate cmdId in dataset '{dataset.DatasetId}'.");
                }

                if (!registry.TryGet(cmdId, out _))
                {
                    throw new MockDataInvalidException(cmdId, "Mock handler missing.");
                }

                if (!dataset.TryGetTemplate(cmdId, out var payload) || payload == null)
                {
                    throw new MockDataInvalidException(cmdId, "Template payload missing.");
                }
            }

            registry.ForEachRegistered((cmdId, _) =>
            {
                if (!supportedLookup.Contains(cmdId))
                {
                    throw new MockDataInvalidException(cmdId, $"Registered handler cmdId not declared in dataset '{dataset.DatasetId}' supported cmd ids.");
                }

                if (!dataset.TryGetTemplate(cmdId, out var payload) || payload == null)
                {
                    throw new MockDataInvalidException(cmdId, "Template payload missing for registered handler.");
                }
            });
        }
    }
}
