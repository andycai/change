namespace Change.Framework.Network
{
    public interface IMockDataProvider
    {
        byte[] GetTemplate(int cmdId);
    }
}
