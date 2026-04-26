namespace Change.Runtime.Network
{
    public interface IMockDataset
    {
        string DatasetId { get; }

        int[] SupportedCmdIds { get; }

        bool TryGetTemplate(int cmdId, out byte[] payload);
    }
}
