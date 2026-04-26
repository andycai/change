namespace Change.Framework.Network
{
    public interface ITransport
    {
        ProtocolEnvelope Send(in ProtocolEnvelope request);
    }
}
