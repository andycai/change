namespace Change.Framework.Network
{
    public interface IMockDispatcher
    {
        ProtocolEnvelope Dispatch(in ProtocolEnvelope request, in MockRequestContext context);
    }
}
