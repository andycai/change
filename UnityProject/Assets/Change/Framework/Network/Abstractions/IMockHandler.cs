namespace Change.Framework.Network
{
    public interface IMockHandler
    {
        int CmdId { get; }

        byte[] Handle(byte[] requestPayload, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory);
    }
}
