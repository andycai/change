using Change.Framework.Cqrs;
using NUnit.Framework;

namespace Change.Framework.Tests
{
    public class RegistrationGuardTests
    {
        [Test]
        public void IsFrozen_ReturnsFalseBeforeFreezeAndTrueAfter()
        {
            var bus = new CqrsBus();

            Assert.IsFalse(bus.IsFrozen);


            Assert.IsTrue(bus.IsFrozen);
        }
    }
}
