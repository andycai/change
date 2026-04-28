# Change.Runtime.Network

## Mock-First startup wiring (MVP)

```csharp
using Change.Framework.Network;
using Change.Runtime.Network;
using System.Text;

IMockDataset dataset = new DefaultMockDataset("dev-default");
IMockDataProvider dataProvider = (IMockDataProvider)dataset;

var registry = new MockRegistry();
registry.Register(new LoginMockHandler());
registry.Register(new ProfileMockHandler());
registry.Register(new InventoryMockHandler());

MockDataValidator.Validate(registry, dataset);

var random = new DeterministicRandom(seed: 20260426);
var valueFactory = new DefaultMockValueFactory(random);
var dispatcher = new MockDispatcher(registry, dataProvider, valueFactory);
var mockTransport = new LocalMockTransport(dispatcher, "dev-default");
var router = new TransportRouter(mockTransport, mockTransport, mockTransport);

var codec = new DelegateProtobufCodec();
codec.Register<string>(
    (s, b, o) => Encoding.UTF8.GetBytes(s, 0, s.Length, b, o),
    (b, o, l) => Encoding.UTF8.GetString(b, o, l));

INetClient netClient = new NetClient(codec, new AllMockRoutePolicy(), router, "dev-default");
```

`MockDispatcher` requires an `IMockDataProvider`. `DefaultMockDataset` satisfies this because it implements both `IMockDataset` and `IMockDataProvider`.

## Guardrails

- Missing `cmdId` handler throws `MockHandlerNotFoundException`.
- Duplicate registration throws `DuplicateMockRegistrationException`.
- Invalid dataset/template throws `MockDataInvalidException`.
