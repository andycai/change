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

            for (var i = 0; i < supportedCmdIds.Length; i++)
            {
                var cmdId = supportedCmdIds[i];
                if (!dataset.TryGetTemplate(cmdId, out var payload) || payload == null)
                {
                    throw new MockDataInvalidException(cmdId, "Template payload missing.");
                }
            }
        }
    }
}
