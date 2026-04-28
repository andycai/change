using System.Text;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class LoginMockHandler : IMockHandler
    {
        public int CmdId => 1001;

        public int Handle(byte[] buffer, int offset, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
        {
            var template = Encoding.UTF8.GetString(dataProvider.GetTemplate(CmdId));
            var suffix = valueFactory.NextString(4);
            var responseString = $"login:{template}:{suffix}";
            
            return Encoding.UTF8.GetBytes(responseString, 0, responseString.Length, buffer, offset);
        }
    }
}
