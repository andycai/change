# Change Network Mock-First Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a mock-first network stack that lets GameScript finish business development without a server, while preserving a stable path to real WebSocket/TCP integration.

**Architecture:** Keep all network contracts in `Change.Framework.Network`, put runtime implementations in `Change.Runtime.Network`, and keep business usage in GameScript through `INetClient` only. Phase 1 routes all traffic to `LocalMockTransport`, with explicit `cmdId` handler registration, dataset selection at startup, fail-fast validation, and deterministic mock value generation.

**Tech Stack:** Unity 2022.3 LTS, C#, Change.Framework collections/patterns, Unity Test Framework (EditMode), protobuf payload model (`cmdId + payload`) with runtime codec adapters.

---

## Scope Check

This is one subsystem (network mock infrastructure) with layered responsibilities. It stays in one plan because all tasks feed a single end-to-end result: `GameScript -> INetClient -> Mock transport -> cmdId handler -> protobuf payload response`.

## File Structure (Create/Modify Map)

### Framework contracts and errors

- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/INetClient.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/ITransport.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/IMessageCodec.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/IRoutePolicy.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/IMockDispatcher.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/IMockHandler.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/IMockDataProvider.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/IMockValueFactory.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Models/ProtocolEnvelope.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Models/RouteTarget.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Models/MockRequestContext.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Exceptions/MockHandlerNotFoundException.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Exceptions/DuplicateMockRegistrationException.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Exceptions/DatasetNotFoundException.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Exceptions/MockDataInvalidException.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Exceptions/CodecOperationException.cs`
- Create: `UnityProject/Assets/Change/Framework/Tests/EditMode/Network/NetworkContractsTests.cs`

### Runtime network core and mock transport

- Create: `UnityProject/Assets/Change/Runtime/Network/Core/NetClient.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Core/TransportRouter.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Core/AllMockRoutePolicy.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/MockRegistry.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/MockDispatcher.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/LocalMockTransport.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Serialization/DelegateProtobufCodec.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Network/NetClientRoutingTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Network/MockRegistryGuardTests.cs`

### Runtime mock data layer

- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Data/IMockDataset.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Data/MockDatasetRegistry.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Data/MockDataValidator.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Data/DeterministicRandom.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Data/DefaultMockValueFactory.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Network/MockDataLayerTests.cs`

### Mock sample dataset and integration tests

- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Samples/DefaultMockDataset.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Samples/Handlers/LoginMockHandler.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Samples/Handlers/ProfileMockHandler.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Samples/Handlers/InventoryMockHandler.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Network/MockEndToEndTests.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/README.md`

---

### Task 1: Add framework network contracts and guard exceptions

**Files:**
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/INetClient.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/ITransport.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/IMessageCodec.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/IRoutePolicy.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/IMockDispatcher.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/IMockHandler.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/IMockDataProvider.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Abstractions/IMockValueFactory.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Models/ProtocolEnvelope.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Models/RouteTarget.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Models/MockRequestContext.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Exceptions/MockHandlerNotFoundException.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Exceptions/DuplicateMockRegistrationException.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Exceptions/DatasetNotFoundException.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Exceptions/MockDataInvalidException.cs`
- Create: `UnityProject/Assets/Change/Framework/Network/Exceptions/CodecOperationException.cs`
- Test: `UnityProject/Assets/Change/Framework/Tests/EditMode/Network/NetworkContractsTests.cs`

- [ ] **Step 1: Write failing contract tests**

```csharp
using System;
using Change.Framework.Network;
using NUnit.Framework;

namespace Change.Framework.Tests.Network
{
    public class NetworkContractsTests
    {
        [Test]
        public void ProtocolEnvelope_RejectsNegativeCmdId()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = new ProtocolEnvelope(-1, 1, Array.Empty<byte>()));
        }

        [Test]
        public void ProtocolEnvelope_RejectsNullPayload()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _ = new ProtocolEnvelope(1001, 1, null));
        }

        [Test]
        public void MockRequestContext_RejectsEmptyDatasetId()
        {
            Assert.Throws<ArgumentException>(() =>
                _ = new MockRequestContext("", 1001, 1));
        }

        [Test]
        public void RouteTarget_HasExpectedStableValues()
        {
            Assert.AreEqual(0, (int)RouteTarget.Mock);
            Assert.AreEqual(1, (int)RouteTarget.RealWebSocket);
            Assert.AreEqual(2, (int)RouteTarget.RealTcp);
        }

        [Test]
        public void Exceptions_AreInvalidOperationBased()
        {
            Assert.IsTrue(typeof(InvalidOperationException)
                .IsAssignableFrom(typeof(MockHandlerNotFoundException)));
            Assert.IsTrue(typeof(InvalidOperationException)
                .IsAssignableFrom(typeof(DuplicateMockRegistrationException)));
            Assert.IsTrue(typeof(InvalidOperationException)
                .IsAssignableFrom(typeof(MockDataInvalidException)));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.Network.NetworkContractsTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-network-contracts-${TS}.xml" \
  -logFile -
```

Expected: FAIL because network contract types do not exist yet.

- [ ] **Step 3: Implement minimal contract layer**

`UnityProject/Assets/Change/Framework/Network/Abstractions/INetClient.cs`

```csharp
namespace Change.Framework.Network
{
    public interface INetClient
    {
        TResponse Send<TRequest, TResponse>(int cmdId, TRequest request);
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Abstractions/ITransport.cs`

```csharp
namespace Change.Framework.Network
{
    public interface ITransport
    {
        ProtocolEnvelope Send(in ProtocolEnvelope request);
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Abstractions/IMessageCodec.cs`

```csharp
namespace Change.Framework.Network
{
    public interface IMessageCodec
    {
        byte[] Encode<TMessage>(TMessage message);
        TMessage Decode<TMessage>(byte[] payload);
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Abstractions/IRoutePolicy.cs`

```csharp
namespace Change.Framework.Network
{
    public interface IRoutePolicy
    {
        RouteTarget Resolve(int cmdId);
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Abstractions/IMockDispatcher.cs`

```csharp
namespace Change.Framework.Network
{
    public interface IMockDispatcher
    {
        ProtocolEnvelope Dispatch(in ProtocolEnvelope request, in MockRequestContext context);
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Abstractions/IMockHandler.cs`

```csharp
namespace Change.Framework.Network
{
    public interface IMockHandler
    {
        int CmdId { get; }
        byte[] Handle(byte[] requestPayload, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory);
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Abstractions/IMockDataProvider.cs`

```csharp
namespace Change.Framework.Network
{
    public interface IMockDataProvider
    {
        byte[] GetTemplate(int cmdId);
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Abstractions/IMockValueFactory.cs`

```csharp
using System;

namespace Change.Framework.Network
{
    public interface IMockValueFactory
    {
        bool NextBool();
        int NextInt(int minInclusive, int maxExclusive);
        float NextFloat(float minInclusive, float maxInclusive);
        string NextString(int length);
        Guid NextGuid();
        DateTimeOffset NextTimeUtc();
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Models/ProtocolEnvelope.cs`

```csharp
using System;

namespace Change.Framework.Network
{
    public readonly struct ProtocolEnvelope
    {
        public ProtocolEnvelope(int cmdId, int requestId, byte[] payload)
        {
            if (cmdId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cmdId));
            }

            if (requestId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestId));
            }

            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            CmdId = cmdId;
            RequestId = requestId;
        }

        public int CmdId { get; }
        public int RequestId { get; }
        public byte[] Payload { get; }
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Models/RouteTarget.cs`

```csharp
namespace Change.Framework.Network
{
    public enum RouteTarget : byte
    {
        Mock = 0,
        RealWebSocket = 1,
        RealTcp = 2,
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Models/MockRequestContext.cs`

```csharp
using System;

namespace Change.Framework.Network
{
    public readonly struct MockRequestContext
    {
        public MockRequestContext(string datasetId, int cmdId, int requestId)
        {
            if (string.IsNullOrWhiteSpace(datasetId))
            {
                throw new ArgumentException("Dataset id cannot be empty.", nameof(datasetId));
            }

            DatasetId = datasetId;
            CmdId = cmdId;
            RequestId = requestId;
        }

        public string DatasetId { get; }
        public int CmdId { get; }
        public int RequestId { get; }
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Exceptions/MockHandlerNotFoundException.cs`

```csharp
using System;

namespace Change.Framework.Network
{
    public sealed class MockHandlerNotFoundException : InvalidOperationException
    {
        public MockHandlerNotFoundException(int cmdId)
            : base($"Mock handler not found for cmdId: {cmdId}.")
        {
        }
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Exceptions/DuplicateMockRegistrationException.cs`

```csharp
using System;

namespace Change.Framework.Network
{
    public sealed class DuplicateMockRegistrationException : InvalidOperationException
    {
        public DuplicateMockRegistrationException(int cmdId)
            : base($"Mock handler already registered for cmdId: {cmdId}.")
        {
        }
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Exceptions/DatasetNotFoundException.cs`

```csharp
using System;

namespace Change.Framework.Network
{
    public sealed class DatasetNotFoundException : InvalidOperationException
    {
        public DatasetNotFoundException(string datasetId)
            : base($"Mock dataset not found: {datasetId}.")
        {
        }
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Exceptions/MockDataInvalidException.cs`

```csharp
using System;

namespace Change.Framework.Network
{
    public sealed class MockDataInvalidException : InvalidOperationException
    {
        public MockDataInvalidException(int cmdId, string reason)
            : base($"Mock data invalid for cmdId {cmdId}: {reason}")
        {
        }
    }
}
```

`UnityProject/Assets/Change/Framework/Network/Exceptions/CodecOperationException.cs`

```csharp
using System;

namespace Change.Framework.Network
{
    public sealed class CodecOperationException : InvalidOperationException
    {
        public CodecOperationException(string operation, int cmdId, int requestId, Exception inner)
            : base($"Codec {operation} failed for cmdId {cmdId}, requestId {requestId}.", inner)
        {
        }
    }
}
```

- [ ] **Step 4: Run tests and confirm pass**

Run the same command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Framework/Network \
        UnityProject/Assets/Change/Framework/Tests/EditMode/Network/NetworkContractsTests.cs
git commit -m "feat(framework): add network contracts and fail-fast exceptions"
```

---

### Task 2: Implement net client routing and local mock transport

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/Network/Core/NetClient.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Core/TransportRouter.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Core/AllMockRoutePolicy.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/LocalMockTransport.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Network/NetClientRoutingTests.cs`

- [ ] **Step 1: Write failing routing tests**

```csharp
using System;
using System.Text;
using Change.Framework.Network;
using Change.Runtime.Network;
using NUnit.Framework;

namespace Change.Runtime.Tests.Network
{
    public class NetClientRoutingTests
    {
        private sealed class TestCodec : IMessageCodec
        {
            public byte[] Encode<TMessage>(TMessage message) => Encoding.UTF8.GetBytes(message.ToString());
            public TMessage Decode<TMessage>(byte[] payload) => (TMessage)(object)Encoding.UTF8.GetString(payload);
        }

        private sealed class EchoTransport : ITransport
        {
            public ProtocolEnvelope Send(in ProtocolEnvelope request)
            {
                return new ProtocolEnvelope(request.CmdId, request.RequestId, request.Payload);
            }
        }

        [Test]
        public void Send_WithAllMockPolicy_UsesMockTransport()
        {
            var router = new TransportRouter(new EchoTransport(), new EchoTransport(), new EchoTransport());
            var client = new NetClient(new TestCodec(), new AllMockRoutePolicy(), router, "dev-default");

            var response = client.Send<string, string>(1001, "hello");

            Assert.AreEqual("hello", response);
        }

        [Test]
        public void Send_WithNegativeCmdId_ThrowsArgumentOutOfRangeException()
        {
            var router = new TransportRouter(new EchoTransport(), new EchoTransport(), new EchoTransport());
            var client = new NetClient(new TestCodec(), new AllMockRoutePolicy(), router, "dev-default");

            Assert.Throws<ArgumentOutOfRangeException>(() => client.Send<string, string>(-1, "x"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.Tests.Network.NetClientRoutingTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-network-routing-${TS}.xml" \
  -logFile -
```

Expected: FAIL (runtime network types not found).

- [ ] **Step 3: Implement minimal routing + local mock transport**

`UnityProject/Assets/Change/Runtime/Network/Core/TransportRouter.cs`

```csharp
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class TransportRouter
    {
        private readonly ITransport _mockTransport;
        private readonly ITransport _webSocketTransport;
        private readonly ITransport _tcpTransport;

        public TransportRouter(ITransport mockTransport, ITransport webSocketTransport, ITransport tcpTransport)
        {
            _mockTransport = mockTransport;
            _webSocketTransport = webSocketTransport;
            _tcpTransport = tcpTransport;
        }

        public ITransport Resolve(RouteTarget target)
        {
            switch (target)
            {
                case RouteTarget.Mock:
                    return _mockTransport;
                case RouteTarget.RealWebSocket:
                    return _webSocketTransport;
                case RouteTarget.RealTcp:
                    return _tcpTransport;
                default:
                    throw new MockDataInvalidException(-1, $"Unsupported route target: {target}");
            }
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/Network/Core/AllMockRoutePolicy.cs`

```csharp
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class AllMockRoutePolicy : IRoutePolicy
    {
        public RouteTarget Resolve(int cmdId)
        {
            return RouteTarget.Mock;
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/Network/Core/NetClient.cs`

```csharp
using System;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class NetClient : INetClient
    {
        private readonly IMessageCodec _codec;
        private readonly IRoutePolicy _routePolicy;
        private readonly TransportRouter _router;
        private readonly string _datasetId;
        private int _nextRequestId;

        public NetClient(IMessageCodec codec, IRoutePolicy routePolicy, TransportRouter router, string datasetId)
        {
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _routePolicy = routePolicy ?? throw new ArgumentNullException(nameof(routePolicy));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _datasetId = string.IsNullOrWhiteSpace(datasetId) ? throw new ArgumentException("Dataset id is required.", nameof(datasetId)) : datasetId;
        }

        public TResponse Send<TRequest, TResponse>(int cmdId, TRequest request)
        {
            if (cmdId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cmdId));
            }

            var requestId = ++_nextRequestId;
            var payload = _codec.Encode(request);
            var envelope = new ProtocolEnvelope(cmdId, requestId, payload);
            var route = _routePolicy.Resolve(cmdId);
            var transport = _router.Resolve(route);
            var responseEnvelope = transport.Send(in envelope);
            return _codec.Decode<TResponse>(responseEnvelope.Payload);
        }

        public string DatasetId => _datasetId;
    }
}
```

`UnityProject/Assets/Change/Runtime/Network/Mock/LocalMockTransport.cs`

```csharp
using System;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class LocalMockTransport : ITransport
    {
        private readonly IMockDispatcher _dispatcher;
        private readonly string _datasetId;

        public LocalMockTransport(IMockDispatcher dispatcher, string datasetId)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _datasetId = string.IsNullOrWhiteSpace(datasetId) ? throw new ArgumentException("Dataset id is required.", nameof(datasetId)) : datasetId;
        }

        public ProtocolEnvelope Send(in ProtocolEnvelope request)
        {
            var context = new MockRequestContext(_datasetId, request.CmdId, request.RequestId);
            return _dispatcher.Dispatch(in request, in context);
        }
    }
}
```

- [ ] **Step 4: Run tests and confirm pass**

Run the same command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Network/Core \
        UnityProject/Assets/Change/Runtime/Network/Mock/LocalMockTransport.cs \
        UnityProject/Assets/Change/Runtime/Tests/EditMode/Network/NetClientRoutingTests.cs
git commit -m "feat(runtime): add net client routing and local mock transport"
```

---

### Task 3: Implement mock registry and dispatcher fail-fast behavior

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/MockRegistry.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/MockDispatcher.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Network/MockRegistryGuardTests.cs`

- [ ] **Step 1: Write failing registry guard tests**

```csharp
using System;
using System.Text;
using Change.Framework.Network;
using Change.Runtime.Network;
using NUnit.Framework;

namespace Change.Runtime.Tests.Network
{
    public class MockRegistryGuardTests
    {
        private sealed class DummyDataProvider : IMockDataProvider
        {
            public byte[] GetTemplate(int cmdId) => Encoding.UTF8.GetBytes("ok");
        }

        private sealed class DummyValueFactory : IMockValueFactory
        {
            public bool NextBool() => true;
            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
            public float NextFloat(float minInclusive, float maxInclusive) => minInclusive;
            public string NextString(int length) => new string('a', length);
            public Guid NextGuid() => Guid.Empty;
            public DateTimeOffset NextTimeUtc() => DateTimeOffset.UnixEpoch;
        }

        private sealed class EchoHandler : IMockHandler
        {
            public int CmdId => 1001;

            public byte[] Handle(byte[] requestPayload, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
            {
                return requestPayload;
            }
        }

        [Test]
        public void Register_DuplicateCmdId_ThrowsDuplicateMockRegistrationException()
        {
            var registry = new MockRegistry();
            registry.Register(new EchoHandler());

            Assert.Throws<DuplicateMockRegistrationException>(() => registry.Register(new EchoHandler()));
        }

        [Test]
        public void Dispatch_WithoutHandler_ThrowsMockHandlerNotFoundException()
        {
            var registry = new MockRegistry();
            var dispatcher = new MockDispatcher(registry, new DummyDataProvider(), new DummyValueFactory());
            var request = new ProtocolEnvelope(9999, 1, Encoding.UTF8.GetBytes("x"));
            var context = new MockRequestContext("dev-default", 9999, 1);

            Assert.Throws<MockHandlerNotFoundException>(() => dispatcher.Dispatch(in request, in context));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.Tests.Network.MockRegistryGuardTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-network-registry-${TS}.xml" \
  -logFile -
```

Expected: FAIL.

- [ ] **Step 3: Implement registry and dispatcher**

`UnityProject/Assets/Change/Runtime/Network/Mock/MockRegistry.cs`

```csharp
using System;
using Change.Framework.Collections;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class MockRegistry
    {
        private readonly FastDictionary<int, IMockHandler> _handlers = new();

        public void Register(IMockHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!_handlers.TryAdd(handler.CmdId, handler))
            {
                throw new DuplicateMockRegistrationException(handler.CmdId);
            }
        }

        public bool TryGet(int cmdId, out IMockHandler handler)
        {
            return _handlers.TryGetValue(cmdId, out handler);
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/Network/Mock/MockDispatcher.cs`

```csharp
using System;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class MockDispatcher : IMockDispatcher
    {
        private readonly MockRegistry _registry;
        private readonly IMockDataProvider _dataProvider;
        private readonly IMockValueFactory _valueFactory;

        public MockDispatcher(MockRegistry registry, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
            _valueFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
        }

        public ProtocolEnvelope Dispatch(in ProtocolEnvelope request, in MockRequestContext context)
        {
            if (!_registry.TryGet(request.CmdId, out var handler))
            {
                throw new MockHandlerNotFoundException(request.CmdId);
            }

            var responsePayload = handler.Handle(request.Payload, in context, _dataProvider, _valueFactory);
            return new ProtocolEnvelope(request.CmdId, request.RequestId, responsePayload);
        }
    }
}
```

- [ ] **Step 4: Run tests and confirm pass**

Run the same command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Network/Mock/MockRegistry.cs \
        UnityProject/Assets/Change/Runtime/Network/Mock/MockDispatcher.cs \
        UnityProject/Assets/Change/Runtime/Tests/EditMode/Network/MockRegistryGuardTests.cs
git commit -m "feat(runtime): add mock registry and dispatcher guards"
```

---

### Task 4: Build mock data layer with deterministic generation

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Data/IMockDataset.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Data/MockDatasetRegistry.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Data/MockDataValidator.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Data/DeterministicRandom.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Data/DefaultMockValueFactory.cs`
- Test: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Network/MockDataLayerTests.cs`

- [ ] **Step 1: Write failing data-layer tests**

```csharp
using System;
using Change.Framework.Network;
using Change.Runtime.Network;
using NUnit.Framework;

namespace Change.Runtime.Tests.Network
{
    public class MockDataLayerTests
    {
        [Test]
        public void DeterministicRandom_WithSameSeed_ReturnsSameSequence()
        {
            var a = new DeterministicRandom(42);
            var b = new DeterministicRandom(42);

            Assert.AreEqual(a.NextInt(0, 1000), b.NextInt(0, 1000));
            Assert.AreEqual(a.NextInt(0, 1000), b.NextInt(0, 1000));
        }

        [Test]
        public void DatasetRegistry_UnknownDataset_ThrowsDatasetNotFoundException()
        {
            var registry = new MockDatasetRegistry();
            Assert.Throws<DatasetNotFoundException>(() => registry.Resolve("missing"));
        }

        [Test]
        public void Validator_MissingTemplate_ThrowsMockDataInvalidException()
        {
            var registry = new MockRegistry();
            var dataset = new EmptyDataset("dev-default");

            Assert.Throws<MockDataInvalidException>(() => MockDataValidator.Validate(registry, dataset));
        }

        private sealed class EmptyDataset : IMockDataset
        {
            public EmptyDataset(string datasetId)
            {
                DatasetId = datasetId;
            }

            public string DatasetId { get; }

            public bool TryGetTemplate(int cmdId, out byte[] payload)
            {
                payload = null;
                return false;
            }

            public int[] SupportedCmdIds => new[] { 1001 };
        }
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.Tests.Network.MockDataLayerTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-network-data-${TS}.xml" \
  -logFile -
```

Expected: FAIL.

- [ ] **Step 3: Implement deterministic data layer**

`UnityProject/Assets/Change/Runtime/Network/Mock/Data/IMockDataset.cs`

```csharp
namespace Change.Runtime.Network
{
    public interface IMockDataset
    {
        string DatasetId { get; }
        int[] SupportedCmdIds { get; }
        bool TryGetTemplate(int cmdId, out byte[] payload);
    }
}
```

`UnityProject/Assets/Change/Runtime/Network/Mock/Data/MockDatasetRegistry.cs`

```csharp
using System;
using System.Collections.Generic;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class MockDatasetRegistry
    {
        private readonly Dictionary<string, IMockDataset> _datasets = new(StringComparer.Ordinal);

        public void Register(IMockDataset dataset)
        {
            if (dataset == null)
            {
                throw new ArgumentNullException(nameof(dataset));
            }

            if (!_datasets.TryAdd(dataset.DatasetId, dataset))
            {
                throw new MockDataInvalidException(-1, $"Duplicate datasetId: {dataset.DatasetId}");
            }
        }

        public IMockDataset Resolve(string datasetId)
        {
            if (!_datasets.TryGetValue(datasetId, out var dataset))
            {
                throw new DatasetNotFoundException(datasetId);
            }

            return dataset;
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/Network/Mock/Data/DeterministicRandom.cs`

```csharp
using System;

namespace Change.Runtime.Network
{
    public sealed class DeterministicRandom
    {
        private readonly Random _random;

        public DeterministicRandom(int seed)
        {
            _random = new Random(seed);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }

        public float NextFloat(float minInclusive, float maxInclusive)
        {
            var unit = (float)_random.NextDouble();
            return minInclusive + (maxInclusive - minInclusive) * unit;
        }

        public bool NextBool()
        {
            return _random.Next(0, 2) == 0;
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/Network/Mock/Data/DefaultMockValueFactory.cs`

```csharp
using System;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class DefaultMockValueFactory : IMockValueFactory
    {
        private readonly DeterministicRandom _random;

        public DefaultMockValueFactory(DeterministicRandom random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public bool NextBool() => _random.NextBool();

        public int NextInt(int minInclusive, int maxExclusive) => _random.NextInt(minInclusive, maxExclusive);

        public float NextFloat(float minInclusive, float maxInclusive) => _random.NextFloat(minInclusive, maxInclusive);

        public string NextString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var buffer = new char[length];

            for (var i = 0; i < length; i++)
            {
                buffer[i] = chars[_random.NextInt(0, chars.Length)];
            }

            return new string(buffer);
        }

        public Guid NextGuid()
        {
            var bytes = new byte[16];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)_random.NextInt(0, 256);
            }

            return new Guid(bytes);
        }

        public DateTimeOffset NextTimeUtc()
        {
            var seconds = _random.NextInt(0, 7 * 24 * 60 * 60);
            return DateTimeOffset.UnixEpoch.AddSeconds(seconds);
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/Network/Mock/Data/MockDataValidator.cs`

```csharp
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public static class MockDataValidator
    {
        public static void Validate(MockRegistry registry, IMockDataset dataset)
        {
            if (registry == null)
            {
                throw new MockDataInvalidException(-1, "Registry cannot be null.");
            }

            if (dataset == null)
            {
                throw new MockDataInvalidException(-1, "Dataset cannot be null.");
            }

            foreach (var cmdId in dataset.SupportedCmdIds)
            {
                if (!dataset.TryGetTemplate(cmdId, out var payload) || payload == null)
                {
                    throw new MockDataInvalidException(cmdId, "Template payload missing.");
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run tests and confirm pass**

Run the same command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Network/Mock/Data \
        UnityProject/Assets/Change/Runtime/Tests/EditMode/Network/MockDataLayerTests.cs
git commit -m "feat(runtime): add deterministic mock data layer"
```

---

### Task 5: Add cmdId sample handlers, dataset, codec adapter, and end-to-end tests

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/Network/Serialization/DelegateProtobufCodec.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Samples/DefaultMockDataset.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Samples/Handlers/LoginMockHandler.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Samples/Handlers/ProfileMockHandler.cs`
- Create: `UnityProject/Assets/Change/Runtime/Network/Mock/Samples/Handlers/InventoryMockHandler.cs`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Network/MockEndToEndTests.cs`

- [ ] **Step 1: Write failing end-to-end tests for 3 cmdIds**

```csharp
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
            codec.Register<string>(s => Encoding.UTF8.GetBytes(s), b => Encoding.UTF8.GetString(b));

            var valueFactory = new DefaultMockValueFactory(new DeterministicRandom(123));
            var dispatcher = new MockDispatcher(registry, dataset, valueFactory);
            var mockTransport = new LocalMockTransport(dispatcher, "dev-default");
            var router = new TransportRouter(mockTransport, mockTransport, mockTransport);
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
```

- [ ] **Step 2: Run test to verify failure**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.Tests.Network.MockEndToEndTests" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-network-e2e-${TS}.xml" \
  -logFile -
```

Expected: FAIL.

- [ ] **Step 3: Implement sample dataset, handlers, and codec adapter**

`UnityProject/Assets/Change/Runtime/Network/Serialization/DelegateProtobufCodec.cs`

```csharp
using System;
using System.Collections.Generic;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class DelegateProtobufCodec : IMessageCodec
    {
        private readonly Dictionary<Type, Delegate> _encoders = new();
        private readonly Dictionary<Type, Delegate> _decoders = new();

        public void Register<T>(Func<T, byte[]> encode, Func<byte[], T> decode)
        {
            _encoders[typeof(T)] = encode;
            _decoders[typeof(T)] = decode;
        }

        public byte[] Encode<TMessage>(TMessage message)
        {
            if (!_encoders.TryGetValue(typeof(TMessage), out var encoder))
            {
                throw new CodecOperationException("encode", -1, -1, new InvalidOperationException($"No encoder for {typeof(TMessage).FullName}"));
            }

            return ((Func<TMessage, byte[]>)encoder).Invoke(message);
        }

        public TMessage Decode<TMessage>(byte[] payload)
        {
            if (!_decoders.TryGetValue(typeof(TMessage), out var decoder))
            {
                throw new CodecOperationException("decode", -1, -1, new InvalidOperationException($"No decoder for {typeof(TMessage).FullName}"));
            }

            return ((Func<byte[], TMessage>)decoder).Invoke(payload);
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/Network/Mock/Samples/DefaultMockDataset.cs`

```csharp
using System.Collections.Generic;
using System.Text;

namespace Change.Runtime.Network
{
    public sealed class DefaultMockDataset : IMockDataset, Change.Framework.Network.IMockDataProvider
    {
        private readonly Dictionary<int, byte[]> _templates;

        public DefaultMockDataset(string datasetId)
        {
            DatasetId = datasetId;
            _templates = new Dictionary<int, byte[]>
            {
                { 1001, Encoding.UTF8.GetBytes("login-template") },
                { 1002, Encoding.UTF8.GetBytes("profile-template") },
                { 1003, Encoding.UTF8.GetBytes("inventory-template") },
            };
        }

        public string DatasetId { get; }

        public int[] SupportedCmdIds => new[] { 1001, 1002, 1003 };

        public bool TryGetTemplate(int cmdId, out byte[] payload)
        {
            return _templates.TryGetValue(cmdId, out payload);
        }

        public byte[] GetTemplate(int cmdId)
        {
            return _templates[cmdId];
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/Network/Mock/Samples/Handlers/LoginMockHandler.cs`

```csharp
using System.Text;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class LoginMockHandler : IMockHandler
    {
        public int CmdId => 1001;

        public byte[] Handle(byte[] requestPayload, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
        {
            var suffix = valueFactory.NextString(4);
            return Encoding.UTF8.GetBytes($"login:{suffix}");
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/Network/Mock/Samples/Handlers/ProfileMockHandler.cs`

```csharp
using System.Text;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class ProfileMockHandler : IMockHandler
    {
        public int CmdId => 1002;

        public byte[] Handle(byte[] requestPayload, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
        {
            var level = valueFactory.NextInt(1, 61);
            return Encoding.UTF8.GetBytes($"profile:lvl-{level}");
        }
    }
}
```

`UnityProject/Assets/Change/Runtime/Network/Mock/Samples/Handlers/InventoryMockHandler.cs`

```csharp
using System.Text;
using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class InventoryMockHandler : IMockHandler
    {
        public int CmdId => 1003;

        public byte[] Handle(byte[] requestPayload, in MockRequestContext context, IMockDataProvider dataProvider, IMockValueFactory valueFactory)
        {
            var count = valueFactory.NextInt(1, 11);
            return Encoding.UTF8.GetBytes($"inventory:items-{count}");
        }
    }
}
```

- [ ] **Step 4: Run tests and confirm pass**

Run the same command from Step 2.

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Network/Serialization/DelegateProtobufCodec.cs \
        UnityProject/Assets/Change/Runtime/Network/Mock/Samples \
        UnityProject/Assets/Change/Runtime/Tests/EditMode/Network/MockEndToEndTests.cs
git commit -m "feat(runtime): add mock sample dataset and cmdId handlers"
```

---

### Task 6: Add startup wiring docs and run full editmode network test suite

**Files:**
- Create: `UnityProject/Assets/Change/Runtime/Network/README.md`
- Modify: `UnityProject/Assets/Change/Runtime/Tests/EditMode/Change.Runtime.EditModeTests.asmdef` (if network folder compile scope requires include)

- [ ] **Step 1: Write README and startup wiring example**

````markdown
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
````

- [ ] **Step 2: Ensure test asmdef references are correct**

Replace file content only if it differs from this exact JSON:

```json
{
  "name": "Change.Runtime.EditModeTests",
  "rootNamespace": "Change.Runtime",
  "references": [
    "Change.Runtime",
    "Change.Framework"
  ],
  "optionalUnityReferences": [
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "excludePlatforms": []
}
```

- [ ] **Step 3: Run full runtime network editmode tests**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Runtime.Tests.Network" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-network-runtime-${TS}.xml" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 4: Run framework network editmode tests**

Run:

```bash
TS="$(date +%Y%m%d-%H%M%S)"
UNITY_BIN="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
"$UNITY_BIN" -batchmode -nographics \
  -projectPath "$(pwd)/UnityProject" \
  -runTests -testPlatform EditMode \
  -testFilter "Change.Framework.Tests.Network" \
  -testResults "$(pwd)/UnityProject/TestResults/editmode-network-framework-${TS}.xml" \
  -logFile -
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/Network/README.md \
        UnityProject/Assets/Change/Runtime/Tests/EditMode/Change.Runtime.EditModeTests.asmdef
git commit -m "docs(runtime): document mock-first network startup wiring"
```

---

## Spec Coverage Matrix

- Mock-first local transport only (phase 1): **Task 2, Task 3**
- `cmdId` protocol envelope and route model: **Task 1, Task 2**
- Dataset selection and fail-fast validation: **Task 4, Task 6**
- Mock data generation layer (`mind-mock` style, deterministic): **Task 4, Task 5**
- 2~3 cmd sample chains: **Task 5**
- No real WS/TCP implementation in phase 1: **Task 2 (`AllMockRoutePolicy`)**

## Placeholder Scan

- Verified: no `TBD`, `TODO`, or unresolved “implement later” markers.
- Verified: each code-writing step includes concrete file content.

## Type Consistency Check

- `INetClient`, `ITransport`, `IMessageCodec`, `IMockHandler`, `IMockDataProvider`, and `IMockValueFactory` signatures are consistent across all tasks.
- `ProtocolEnvelope` (`CmdId`, `RequestId`, `Payload`) naming remains stable in contracts, runtime code, and tests.
- `datasetId` startup injection path is consistent across plan tasks and README sample.
