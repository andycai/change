using System.Text;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class ProfileMockHandler : IMockHandler
    {
        public int CmdId => 1002;

        public int Handle(byte[] buffer, int offset, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
        {
            var template = Encoding.UTF8.GetString(dataProvider.GetTemplate(CmdId));
            var level = valueFactory.NextInt(1, 61);
            var response = $"profile:{template}:lvl-{level}";
            return Encoding.UTF8.GetBytes(response, 0, response.Length, buffer, offset);
        }
    }
}