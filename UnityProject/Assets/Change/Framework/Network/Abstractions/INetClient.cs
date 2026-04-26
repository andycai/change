namespace Change.Framework.Network
{
    public interface INetClient
    {
        TResponse Send<TRequest, TResponse>(int cmdId, TRequest request);
    }
}
