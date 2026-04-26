using System;
using System.Text;
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
            codec.Register<string>(s => Encoding.UTF8.GetBytes(s), b => Encoding.UTF8.GetString(b));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                codec.Register<string>(s => Encoding.UTF8.GetBytes(s), b => Encoding.UTF8.GetString(b)));

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
    }
}
