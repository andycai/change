namespace Change.Runtime.ContentStreaming
{
    public interface INetworkStateProvider
    {
        NetworkType Current { get; }
    }
}
