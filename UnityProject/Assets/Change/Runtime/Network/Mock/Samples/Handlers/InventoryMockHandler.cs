using System.Text;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class InventoryMockHandler : IMockHandler
    {
        public int CmdId => 1003;

        public int Handle(byte[] buffer, int offset, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
        {
            var template = Encoding.UTF8.GetString(dataProvider.GetTemplate(CmdId));
            var count = valueFactory.NextInt(1, 11);
            var response = $"inventory:{template}:items-{count}";
            return Encoding.UTF8.GetBytes(response, 0, response.Length, buffer, offset);
        }
    }
}