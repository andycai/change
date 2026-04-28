using System.Text;
using Change.Framework.Network;
using Change.Runtime.Network;
using NUnit.Framework;

namespace Change.Runtime.Tests.Network
{
    public class MockEndToEndTests
    {
        [Test]
        public void EndToEnd_Login_Profile_Inventory_AllReturnResponses()
        {
            var dataset = new DefaultMockDataset("dev-default");
            var registry = new MockRegistry();
            registry.Register(new LoginMockHandler());
            registry.Register(new ProfileMockHandler());
            registry.Register(new InventoryMockHandler());

            var codec = new DelegateProtobufCodec();
            codec.Register<string>(
                (s, b, o) => Encoding.UTF8.GetBytes(s, 0, s.Length, b, o),
                (b, o, l) => Encoding.UTF8.GetString(b, o, l));

            var valueFactory = new DefaultMockValueFactory(new DeterministicRandom(123));
            var dispatcher = new MockDispatcher(registry, dataset, valueFactory);
            var transport = new LocalMockTransport(dispatcher, "dev-default");
            var router = new TransportRouter(transport, transport, transport);
            var client = new NetClient(codec, new AllMockRoutePolicy(), router, "dev-default");

            var login = client.Send<string, string>(1001, "req-login");
            var profile = client.Send<string, string>(1002, "req-profile");
            var inventory = client.Send<string, string>(1003, "req-inventory");

            Assert.That(login, Does.Contain("login"));
            Assert.That(profile, Does.Contain("profile"));
            Assert.That(inventory, Does.Contain("inventory"));
        }
    }
}