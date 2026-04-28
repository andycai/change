namespace Change.Framework.Network
{
    public interface IMockHandler
    {
        int CmdId { get; }

        int Handle(byte[] buffer, int offset, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory);
    }
}
