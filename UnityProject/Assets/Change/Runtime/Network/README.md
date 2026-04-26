# Change.Runtime.Network

## Mock-First startup wiring (MVP)

```csharp
using System.Text;

var dataset = new DefaultMockDataset("dev-default");
var registry = new MockRegistry();
registry.Register(new LoginMockHandler());
registry.Register(new ProfileMockHandler());
registry.Register(new InventoryMockHandler());

MockDataValidator.Validate(registry, dataset);

var random = new DeterministicRandom(seed: 20260426);
var valueFactory = new DefaultMockValueFactory(random);
var dispatcher = new MockDispatcher(registry, dataset, valueFactory);
var mockTransport = new LocalMockTransport(dispatcher, "dev-default");
var router = new TransportRouter(mockTransport, mockTransport, mockTransport);

var codec = new DelegateProtobufCodec();
codec.Register<string>(s => Encoding.UTF8.GetBytes(s), b => Encoding.UTF8.GetString(b));

INetClient netClient = new NetClient(codec, new AllMockRoutePolicy(), router, "dev-default");
```

## Guardrails

- Missing `cmdId` handler throws `MockHandlerNotFoundException`.
- Duplicate registration throws `DuplicateMockRegistrationException`.
- Invalid dataset/template throws `MockDataInvalidException`.
