using System;
using System.Collections.Generic;
using System.Text;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class DefaultMockDataset : IMockDataset, IMockDataProvider
    {
        private static readonly int[] CmdIds = { 1001, 1002, 1003 };

        private readonly Dictionary<int, byte[]> _templates = new Dictionary<int, byte[]>
        {
            { 1001, Encoding.UTF8.GetBytes("login-template") },
            { 1002, Encoding.UTF8.GetBytes("profile-template") },
            { 1003, Encoding.UTF8.GetBytes("inventory-template") },
        };

        public DefaultMockDataset(string datasetId)
        {
            if (string.IsNullOrWhiteSpace(datasetId))
            {
                throw new ArgumentException("Dataset id is required.", nameof(datasetId));
            }

            DatasetId = datasetId;
        }

        public string DatasetId { get; }

        public int[] SupportedCmdIds => (int[])CmdIds.Clone();

        public bool TryGetTemplate(int cmdId, out byte[] payload)
        {
            return _templates.TryGetValue(cmdId, out payload);
        }

        public byte[] GetTemplate(int cmdId)
        {
            if (!_templates.TryGetValue(cmdId, out var payload))
            {
                throw new MockDataInvalidException(cmdId, "Template payload missing.");
            }

            return payload;
        }
    }
}
