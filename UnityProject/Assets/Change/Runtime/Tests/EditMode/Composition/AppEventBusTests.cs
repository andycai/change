using System;
using Change.Runtime.App.Events;
using NUnit.Framework;

namespace Change.Runtime.Tests.Composition
{
    public sealed class AppEventBusTests
    {
        [Test]
        public void Publish_LocaleChanged_InvokesSubscriber()
        {
            var bus = new InProcessAppEventBus();
            AppLocaleChanged? received = null;
            IDisposable sub = bus.Subscribe<AppLocaleChanged>(e => received = e);

            bus.Publish(new AppLocaleChanged("zh-CN"));

            Assert.IsNotNull(received);
            Assert.IsTrue(received.HasValue);
            Assert.AreEqual("zh-CN", received.Value.CultureName);
            sub.Dispose();
        }
    }
}
