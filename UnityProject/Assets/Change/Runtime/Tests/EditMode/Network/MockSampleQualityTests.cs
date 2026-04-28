using System;
using System.Text;
using Change.Framework.Network;
using Change.Runtime.Network;
using NUnit.Framework;

namespace Change.Runtime.Tests.Network
{
    public class MockSampleQualityTests
    {
        [Test]
        public void Register_WithDuplicateType_ThrowsInvalidOperationException()
        {
            var codec = new DelegateProtobufCodec();
            codec.Register<string>(
                (s, b, o) => Encoding.UTF8.GetBytes(s, 0, s.Length, b, o),
                (b, o, l) => Encoding.UTF8.GetString(b, o, l));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                codec.Register<string>(
                    (s, b, o) => Encoding.UTF8.GetBytes(s, 0, s.Length, b, o),
                    (b, o, l) => Encoding.UTF8.GetString(b, o, l)));

            Assert.That(exception.Message, Does.Contain(typeof(string).FullName));
        }

        [Test]
        public void SupportedCmdIds_WhenCallerMutatesReturnedArray_DoesNotAffectSubsequentReads()
        {
            var dataset = new DefaultMockDataset("dev-default");
            var firstRead = dataset.SupportedCmdIds;
            firstRead[0] = -1;

            var secondRead = dataset.SupportedCmdIds;

            Assert.AreEqual(3, secondRead.Length);
            Assert.AreEqual(1001, secondRead[0]);
            Assert.AreEqual(1002, secondRead[1]);
            Assert.AreEqual(1003, secondRead[2]);
            Assert.AreNotSame(firstRead, secondRead);
        }

        [Test]
        public void DefaultFactory_DispatchDeterministicPerRequestContext_RegardlessOfDispatchOrder()
        {
            var registryA = CreateSampleRegistry();
            var registryB = CreateSampleRegistry();
            var dataset = new DefaultMockDataset("dev-default");
            var dispatcherA = new MockDispatcher(registryA, dataset, new DefaultMockValueFactory(new DeterministicRandom(123)));
            var dispatcherB = new MockDispatcher(registryB, dataset, new DefaultMockValueFactory(new DeterministicRandom(999)));

            var loginPayload = Encoding.UTF8.GetBytes("req-login");
            var profilePayload = Encoding.UTF8.GetBytes("req-profile");
            var loginRequest = new ProtocolEnvelope(1001, 41, loginPayload, 0, loginPayload.Length);
            var profileRequest = new ProtocolEnvelope(1002, 42, profilePayload, 0, profilePayload.Length);
            var loginContext = new MockRequestContext("dev-default", 1001, 41);
            var profileContext = new MockRequestContext("dev-default", 1002, 42);

            var loginFromAB = dispatcherA.Dispatch(in loginRequest, in loginContext);
            var profileFromAB = dispatcherA.Dispatch(in profileRequest, in profileContext);

            var profileFromBA = dispatcherB.Dispatch(in profileRequest, in profileContext);
            var loginFromBA = dispatcherB.Dispatch(in loginRequest, in loginContext);

            Assert.AreEqual(Encoding.UTF8.GetString(loginFromAB.Payload, loginFromAB.Offset, loginFromAB.Length), 
                Encoding.UTF8.GetString(loginFromBA.Payload, loginFromBA.Offset, loginFromBA.Length));
            Assert.AreEqual(Encoding.UTF8.GetString(profileFromAB.Payload, profileFromAB.Offset, profileFromAB.Length), 
                Encoding.UTF8.GetString(profileFromBA.Payload, profileFromBA.Offset, profileFromBA.Length));
                
            System.Buffers.ArrayPool<byte>.Shared.Return(loginFromAB.Payload);
            System.Buffers.ArrayPool<byte>.Shared.Return(profileFromAB.Payload);
            System.Buffers.ArrayPool<byte>.Shared.Return(loginFromBA.Payload);
            System.Buffers.ArrayPool<byte>.Shared.Return(profileFromBA.Payload);
        }

        private static MockRegistry CreateSampleRegistry()
        {
            var registry = new MockRegistry();
            registry.Register(new LoginMockHandler());
            registry.Register(new ProfileMockHandler());
            return registry;
        }
    }
}