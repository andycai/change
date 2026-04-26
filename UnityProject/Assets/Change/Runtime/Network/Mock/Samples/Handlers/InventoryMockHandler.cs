using System.Text;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class InventoryMockHandler : IMockHandler
    {
        public int CmdId => 1003;

        public byte[] Handle(byte[] requestPayload, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
        {
            var template = Encoding.UTF8.GetString(dataProvider.GetTemplate(CmdId));
            var count = valueFactory.NextInt(1, 11);
            return Encoding.UTF8.GetBytes($"inventory:{template}:items-{count}");
        }
    }
}
