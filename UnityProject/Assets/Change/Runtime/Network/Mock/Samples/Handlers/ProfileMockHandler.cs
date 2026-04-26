using System.Text;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class ProfileMockHandler : IMockHandler
    {
        public int CmdId => 1002;

        public byte[] Handle(byte[] requestPayload, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
        {
            var template = Encoding.UTF8.GetString(dataProvider.GetTemplate(CmdId));
            var level = valueFactory.NextInt(1, 61);
            return Encoding.UTF8.GetBytes($"profile:{template}:lvl-{level}");
        }
    }
}
