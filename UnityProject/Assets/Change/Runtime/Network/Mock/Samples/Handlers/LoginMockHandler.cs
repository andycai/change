using System.Text;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class LoginMockHandler : IMockHandler
    {
        public int CmdId => 1001;

        public byte[] Handle(byte[] requestPayload, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
        {
            var template = Encoding.UTF8.GetString(dataProvider.GetTemplate(CmdId));
            var suffix = valueFactory.NextString(4);
            return Encoding.UTF8.GetBytes($"login:{template}:{suffix}");
        }
    }
}
